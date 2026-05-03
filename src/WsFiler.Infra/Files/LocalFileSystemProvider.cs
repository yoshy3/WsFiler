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

    public Task CopyAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(sourcePath));

            if (Directory.Exists(sourcePath))
            {
                CopyDirectory(sourcePath, destinationPath, cancellationToken);
            }
            else if (File.Exists(sourcePath))
            {
                if (!File.Exists(destinationPath))
                {
                    File.Copy(sourcePath, destinationPath, overwrite: false);
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task MoveAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(sourcePath));

            if (Directory.Exists(sourcePath))
            {
                Directory.Move(sourcePath, destinationPath);
            }
            else if (File.Exists(sourcePath))
            {
                File.Move(sourcePath, destinationPath);
            }
        }

        return Task.CompletedTask;
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

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(filePath));

            if (!File.Exists(destinationPath))
            {
                File.Copy(filePath, destinationPath, overwrite: false);
            }
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(directoryPath));
            CopyDirectory(directoryPath, destinationPath, cancellationToken);
        }
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
            attributes.HasFlag(FileAttributes.ReadOnly));
    }
}
