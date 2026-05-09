using WsFiler.Core.Files;

namespace WsFiler.Infra.Files;

public sealed class LocalFileSystemProvider : IFileSystemProvider
{
    public Task<IReadOnlyList<FileSystemItem>> ListDirectoryAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var directory = new DirectoryInfo(path);

        if (!directory.Exists)
        {
            return Task.FromResult<IReadOnlyList<FileSystemItem>>([]);
        }

        var items = directory
            .EnumerateFileSystemInfos()
            .Select(ToItem)
            .OrderByDescending(item => item.ItemType == FileSystemItemType.Directory)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<FileSystemItem>>(items);
    }

    public async Task CopyAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectory,
        Func<FileConflictInfo, Task<FileConflictDecision>> resolveConflictAsync,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destinationDirectory);
        var conflictScope = new ConflictDecisionScope(resolveConflictAsync);

        foreach (var sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(sourcePath));

            if (Directory.Exists(sourcePath))
            {
                await CopyDirectoryAsync(sourcePath, destinationPath, conflictScope, cancellationToken);
            }
            else if (File.Exists(sourcePath))
            {
                if (await ShouldWriteFileAsync(sourcePath, destinationPath, conflictScope, cancellationToken))
                {
                    File.Copy(sourcePath, destinationPath, overwrite: true);
                }
            }
        }
    }

    public async Task MoveAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectory,
        Func<FileConflictInfo, Task<FileConflictDecision>> resolveConflictAsync,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destinationDirectory);
        var conflictScope = new ConflictDecisionScope(resolveConflictAsync);

        foreach (var sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(sourcePath));

            if (Directory.Exists(sourcePath))
            {
                var action = await ResolveDestinationConflictAsync(sourcePath, destinationPath, isDirectory: true, conflictScope, cancellationToken);
                if (action == FileConflictAction.Cancel)
                {
                    break;
                }

                if (action == FileConflictAction.Skip)
                {
                    continue;
                }

                if (Directory.Exists(destinationPath))
                {
                    Directory.Delete(destinationPath, recursive: true);
                }

                Directory.Move(sourcePath, destinationPath);
            }
            else if (File.Exists(sourcePath))
            {
                var action = await ResolveDestinationConflictAsync(sourcePath, destinationPath, isDirectory: false, conflictScope, cancellationToken);
                if (action == FileConflictAction.Cancel)
                {
                    break;
                }

                if (action == FileConflictAction.Skip)
                {
                    continue;
                }

                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }

                File.Move(sourcePath, destinationPath);
            }
        }
    }

    public Task DeleteAsync(
        IReadOnlyList<string> targetPaths,
        CancellationToken cancellationToken = default)
    {
        foreach (var targetPath in targetPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Directory.Exists(targetPath))
            {
                Directory.Delete(targetPath, recursive: true);
            }
            else if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }

        return Task.CompletedTask;
    }

    public Task RenameAsync(
        string sourcePath,
        string newName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new IOException("Name is required.");
        }

        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new IOException("Name contains invalid characters.");
        }

        var parent = Path.GetDirectoryName(sourcePath)
            ?? throw new IOException("Parent directory was not found.");
        var destinationPath = Path.Combine(parent, newName);

        if (Directory.Exists(destinationPath) || File.Exists(destinationPath))
        {
            throw new IOException("Destination already exists.");
        }

        if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, destinationPath);
        }
        else if (File.Exists(sourcePath))
        {
            File.Move(sourcePath, destinationPath);
        }

        return Task.CompletedTask;
    }

    private static async Task CopyDirectoryAsync(
        string sourceDirectory,
        string destinationDirectory,
        ConflictDecisionScope conflictScope,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(filePath));

            if (await ShouldWriteFileAsync(filePath, destinationPath, conflictScope, cancellationToken))
            {
                File.Copy(filePath, destinationPath, overwrite: true);
            }
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(directoryPath));
            await CopyDirectoryAsync(directoryPath, destinationPath, conflictScope, cancellationToken);
        }
    }

    private static async Task<bool> ShouldWriteFileAsync(
        string sourcePath,
        string destinationPath,
        ConflictDecisionScope conflictScope,
        CancellationToken cancellationToken)
    {
        var action = await ResolveDestinationConflictAsync(sourcePath, destinationPath, isDirectory: false, conflictScope, cancellationToken);
        if (action == FileConflictAction.Cancel)
        {
            throw new OperationCanceledException();
        }

        return action == FileConflictAction.Overwrite;
    }

    private static async Task<FileConflictAction> ResolveDestinationConflictAsync(
        string sourcePath,
        string destinationPath,
        bool isDirectory,
        ConflictDecisionScope conflictScope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var exists = isDirectory
            ? Directory.Exists(destinationPath)
            : File.Exists(destinationPath);

        if (!exists)
        {
            return FileConflictAction.Overwrite;
        }

        return await conflictScope.ResolveAsync(new FileConflictInfo(
            sourcePath,
            destinationPath,
            Path.GetFileName(sourcePath),
            isDirectory));
    }

    private sealed class ConflictDecisionScope(Func<FileConflictInfo, Task<FileConflictDecision>> resolveConflictAsync)
    {
        private FileConflictAction? actionForAll;

        public async Task<FileConflictAction> ResolveAsync(FileConflictInfo conflict)
        {
            if (actionForAll is { } action)
            {
                return action;
            }

            var decision = await resolveConflictAsync(conflict);
            if (decision.ApplyToAll && decision.Action != FileConflictAction.Cancel)
            {
                actionForAll = decision.Action;
            }

            return decision.Action;
        }
    }

    public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(path);
        return Task.CompletedTask;
    }

    public Task CreateFileAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var _ = File.Create(path);
        return Task.CompletedTask;
    }

    public Task<FileAttributes> GetAttributesAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.GetAttributes(path));
    }

    public Task SetAttributesAsync(string path, FileAttributes attributes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.SetAttributes(path, attributes);
        return Task.CompletedTask;
    }

    private static FileSystemItem ToItem(FileSystemInfo info)
    {
        var attributes = info.Attributes;
        var isDirectory = attributes.HasFlag(FileAttributes.Directory);
        var isSymbolicLink = attributes.HasFlag(FileAttributes.ReparsePoint);
        var type = isSymbolicLink
            ? FileSystemItemType.SymbolicLink
            : isDirectory
                ? FileSystemItemType.Directory
                : FileSystemItemType.File;

        var size = info is FileInfo fileInfo ? fileInfo.Length : (long?)null;

        return new FileSystemItem(
            info.Name,
            info.FullName,
            type,
            size,
            info.LastWriteTime,
            isDirectory ? "" : info.Extension.TrimStart('.'),
            attributes.HasFlag(FileAttributes.Hidden),
            attributes.HasFlag(FileAttributes.ReadOnly),
            attributes.HasFlag(FileAttributes.System));
    }
}
