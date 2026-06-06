using System.Diagnostics;
using System.Globalization;
using WsFiler.Core.Files;
using WsFiler.Infra.Files.Archive;

namespace WsFiler.Infra.Files;

public sealed class LocalFileSystemProvider : IFileSystemProvider
{
    public Task<IReadOnlyList<FileSystemItem>> ListDirectoryAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ArchivePath.TryParse(path, out var archivePath))
        {
            return Task.FromResult(ArchiveReaderFactory.GetReader(archivePath.ArchiveFilePath).ListDirectory(archivePath));
        }

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

    public Task<bool> CanListDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ArchivePath.TryParse(path, out var archivePath))
        {
            return Task.FromResult(ArchiveReaderFactory.GetReader(archivePath.ArchiveFilePath).CanListDirectory(archivePath));
        }

        return Task.FromResult(Directory.Exists(path));
    }

    public string? GetParentPath(string path)
    {
        return ArchivePath.TryParse(path, out var archivePath)
            ? archivePath.GetParentPath()
            : Directory.GetParent(path)?.FullName;
    }

    public string GetFileName(string path)
    {
        return ArchivePath.TryParse(path, out var archivePath)
            ? archivePath.GetFileName()
            : Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ArchivePath.TryParse(path, out var archivePath) && !archivePath.IsRoot)
        {
            return Task.FromResult(ArchiveReaderFactory.GetReader(archivePath.ArchiveFilePath).OpenRead(archivePath));
        }

        return Task.FromResult<Stream>(File.OpenRead(path));
    }

    public async Task CopyAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectory,
        Func<FileConflictInfo, Task<FileConflictDecision>> resolveConflictAsync,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfArchiveDestination(destinationDirectory);
        Directory.CreateDirectory(destinationDirectory);
        var conflictScope = new ConflictDecisionScope(resolveConflictAsync);
        var completedItems = 0;
        var totalItems = TryCountCopyItems(sourcePaths, cancellationToken);

        void ReportCurrent(string path)
        {
            progress?.Report(new FileOperationProgress(path, completedItems, totalItems));
        }

        void ReportCompleted(string path)
        {
            ReportCompletedCount(path, completedCount: 1);
        }

        void ReportCompletedCount(string path, int completedCount)
        {
            completedItems += completedCount;
            progress?.Report(new FileOperationProgress(path, completedItems, totalItems));
        }

        foreach (var sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(sourcePath));
            ReportCurrent(sourcePath);

            if (ArchivePath.TryParse(sourcePath, out var archivePath) && !archivePath.IsRoot)
            {
                await ArchiveReaderFactory.GetReader(archivePath.ArchiveFilePath).ExtractToDirectoryAsync(
                    archivePath,
                    destinationDirectory,
                    async (archiveEntryPath, resolvedDestinationPath, isDirectory) =>
                        await ResolveDestinationConflictAsync(
                            archiveEntryPath,
                            resolvedDestinationPath,
                            isDirectory,
                            conflictScope,
                            cancellationToken),
                    cancellationToken);
                ReportCompleted(sourcePath);
            }
            else if (Directory.Exists(sourcePath))
            {
                await CopyDirectoryAsync(
                    sourcePath,
                    destinationPath,
                    conflictScope,
                    ReportCurrent,
                    ReportCompleted,
                    cancellationToken);
            }
            else if (File.Exists(sourcePath))
            {
                ReportCurrent(sourcePath);
                if (await ShouldWriteFileAsync(sourcePath, destinationPath, conflictScope, cancellationToken))
                {
                    await CopyFileAsync(sourcePath, destinationPath, cancellationToken);
                }

                ReportCompleted(sourcePath);
            }
        }
    }

    public async Task MoveAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectory,
        Func<FileConflictInfo, Task<FileConflictDecision>> resolveConflictAsync,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfArchiveMutation(sourcePaths, destinationDirectory);
        Directory.CreateDirectory(destinationDirectory);
        var conflictScope = new ConflictDecisionScope(resolveConflictAsync);
        var completedItems = 0;
        var totalItems = TryCountCopyItems(sourcePaths, cancellationToken);

        void ReportCurrent(string path)
        {
            progress?.Report(new FileOperationProgress(path, completedItems, totalItems));
        }

        void ReportCompleted(string path)
        {
            ReportCompletedCount(path, completedCount: 1);
        }

        void ReportCompletedCount(string path, int completedCount)
        {
            completedItems += completedCount;
            progress?.Report(new FileOperationProgress(path, completedItems, totalItems));
        }

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

                if (IsCrossVolumeMove(sourcePath, destinationPath))
                {
                    await CopyDirectoryAsync(
                        sourcePath,
                        destinationPath,
                        conflictScope,
                        ReportCurrent,
                        ReportCompleted,
                        cancellationToken);
                    await DeleteDirectoryAsync(
                        sourcePath,
                        new DeleteConfirmationScope(confirmDeleteAsync: null),
                        cancellationToken);
                }
                else
                {
                    var completedCount = TryCountCopyItems([sourcePath], cancellationToken) ?? 1;
                    Directory.Move(sourcePath, destinationPath);
                    ReportCompletedCount(sourcePath, completedCount);
                }
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

                if (IsCrossVolumeMove(sourcePath, destinationPath))
                {
                    ReportCurrent(sourcePath);
                    await CopyFileAsync(sourcePath, destinationPath, cancellationToken);
                    ReportCompleted(sourcePath);
                    await DeleteFileAsync(
                        sourcePath,
                        new DeleteConfirmationScope(confirmDeleteAsync: null),
                        cancellationToken);
                }
                else
                {
                    File.Move(sourcePath, destinationPath);
                    ReportCompleted(sourcePath);
                }
            }
        }
    }

    private static bool IsCrossVolumeMove(string sourcePath, string destinationPath)
    {
        var sourceRoot = Path.GetPathRoot(Path.GetFullPath(sourcePath));
        var destinationRoot = Path.GetPathRoot(Path.GetFullPath(destinationPath));
        return !string.Equals(sourceRoot, destinationRoot, OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal);
    }

    public async Task DeleteAsync(
        IReadOnlyList<string> targetPaths,
        Func<FileDeleteConfirmationInfo, Task<FileDeleteConfirmationDecision>>? confirmDeleteAsync = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfArchiveMutation(targetPaths);
        var confirmationScope = new DeleteConfirmationScope(confirmDeleteAsync);

        foreach (var targetPath in targetPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Directory.Exists(targetPath))
            {
                await DeleteDirectoryAsync(targetPath, confirmationScope, cancellationToken);
            }
            else if (File.Exists(targetPath))
            {
                await DeleteFileAsync(targetPath, confirmationScope, cancellationToken);
            }
        }
    }

    private static async Task<bool> DeleteDirectoryAsync(
        string path,
        DeleteConfirmationScope confirmationScope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!await ShouldDeleteReadOnlyItemAsync(path, isDirectory: true, confirmationScope, cancellationToken))
        {
            return false;
        }

        var allChildrenDeleted = true;
        foreach (var file in Directory.EnumerateFiles(path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            allChildrenDeleted &= await DeleteFileAsync(file, confirmationScope, cancellationToken);
        }

        foreach (var directory in Directory.EnumerateDirectories(path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            allChildrenDeleted &= await DeleteDirectoryAsync(directory, confirmationScope, cancellationToken);
        }

        if (!allChildrenDeleted)
        {
            return false;
        }

        ClearReadOnlyAttribute(path);
        Directory.Delete(path);
        return true;
    }

    private static async Task<bool> DeleteFileAsync(
        string path,
        DeleteConfirmationScope confirmationScope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!await ShouldDeleteReadOnlyItemAsync(path, isDirectory: false, confirmationScope, cancellationToken))
        {
            return false;
        }

        ClearReadOnlyAttribute(path);
        File.Delete(path);
        return true;
    }

    private static async Task<bool> ShouldDeleteReadOnlyItemAsync(
        string path,
        bool isDirectory,
        DeleteConfirmationScope confirmationScope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly))
        {
            return true;
        }

        var action = await confirmationScope.ResolveAsync(new FileDeleteConfirmationInfo(
            path,
            Path.GetFileName(path),
            isDirectory,
            IsReadOnly: true));
        return action switch
        {
            FileDeleteConfirmationAction.Delete => true,
            FileDeleteConfirmationAction.Skip => false,
            FileDeleteConfirmationAction.Cancel => throw new OperationCanceledException(),
            _ => false,
        };
    }

    private static void ClearReadOnlyAttribute(string path)
    {
        var attributes = File.GetAttributes(path);
        if (attributes.HasFlag(FileAttributes.ReadOnly))
        {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
    }

    public Task RenameAsync(
        string sourcePath,
        string newName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfArchiveMutation([sourcePath]);

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
        Action<string> reportCurrent,
        Action<string> reportCompleted,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(filePath));
            reportCurrent(filePath);

            if (await ShouldWriteFileAsync(filePath, destinationPath, conflictScope, cancellationToken))
            {
                await CopyFileAsync(filePath, destinationPath, cancellationToken);
            }

            reportCompleted(filePath);
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(directoryPath));
            await CopyDirectoryAsync(
                directoryPath,
                destinationPath,
                conflictScope,
                reportCurrent,
                reportCompleted,
                cancellationToken);
        }
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await source.CopyToAsync(destination, cancellationToken);
    }

    private static int? TryCountCopyItems(
        IReadOnlyList<string> sourcePaths,
        CancellationToken cancellationToken)
    {
        try
        {
            var total = 0;
            foreach (var sourcePath in sourcePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ArchivePath.TryParse(sourcePath, out var archivePath) && !archivePath.IsRoot)
                {
                    return null;
                }

                if (File.Exists(sourcePath))
                {
                    total++;
                }
                else if (Directory.Exists(sourcePath))
                {
                    total += Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories).Count();
                }
            }

            return total;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
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

    private sealed class DeleteConfirmationScope(
        Func<FileDeleteConfirmationInfo, Task<FileDeleteConfirmationDecision>>? confirmDeleteAsync)
    {
        private FileDeleteConfirmationAction? actionForAll;

        public async Task<FileDeleteConfirmationAction> ResolveAsync(FileDeleteConfirmationInfo info)
        {
            if (actionForAll is { } action)
            {
                return action;
            }

            if (confirmDeleteAsync is null)
            {
                return FileDeleteConfirmationAction.Delete;
            }

            var decision = await confirmDeleteAsync(info);
            if (decision.ApplyToAll && decision.Action != FileDeleteConfirmationAction.Cancel)
            {
                actionForAll = decision.Action;
            }

            return decision.Action;
        }
    }

    public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfArchiveMutation([path]);
        Directory.CreateDirectory(path);
        return Task.CompletedTask;
    }

    public Task CreateFileAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfArchiveMutation([path]);
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
        ThrowIfArchiveMutation([path]);
        File.SetAttributes(path, attributes);
        return Task.CompletedTask;
    }

    public Task<UnixFileMode> GetUnixFileModeAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Unix file mode is not supported on Windows.");
        }

        return Task.FromResult(File.GetUnixFileMode(path));
    }

    public Task SetUnixFileModeAsync(string path, UnixFileMode mode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfArchiveMutation([path]);
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Unix file mode is not supported on Windows.");
        }

        File.SetUnixFileMode(path, mode);
        return Task.CompletedTask;
    }

    public async Task<bool> CanSetUnixFileModeAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        var currentUserId = await GetCurrentUnixUserIdAsync(cancellationToken);
        if (currentUserId == 0)
        {
            return true;
        }

        var ownerUserId = await GetOwnerUnixUserIdAsync(path, cancellationToken);
        return currentUserId == ownerUserId;
    }

    private static async Task<uint> GetCurrentUnixUserIdAsync(CancellationToken cancellationToken)
    {
        var output = await RunProcessAsync("id", ["-u"], cancellationToken);
        return uint.Parse(output, CultureInfo.InvariantCulture);
    }

    private static async Task<uint> GetOwnerUnixUserIdAsync(string path, CancellationToken cancellationToken)
    {
        var arguments = OperatingSystem.IsMacOS()
            ? new[] { "-f", "%u", path }
            : new[] { "-c", "%u", path };
        var output = await RunProcessAsync("stat", arguments, cancellationToken);
        return uint.Parse(output, CultureInfo.InvariantCulture);
    }

    private static async Task<string> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        if (process.ExitCode == 0)
        {
            return output.Trim();
        }

        var error = await errorTask;
        throw new IOException(error.Trim());
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
            isDirectory ? "" : GetExtensionWithoutDot(info.Name),
            attributes.HasFlag(FileAttributes.Hidden),
            attributes.HasFlag(FileAttributes.ReadOnly),
            attributes.HasFlag(FileAttributes.System));
    }

    private static string GetExtensionWithoutDot(string name)
    {
        var lastDot = name.LastIndexOf('.');
        if (lastDot <= 0)
        {
            return string.Empty;
        }
        return name[(lastDot + 1)..];
    }

    private static void ThrowIfArchiveMutation(
        IReadOnlyList<string> paths,
        string? destinationDirectory = null)
    {
        if ((destinationDirectory is not null && ArchivePath.TryParse(destinationDirectory, out _)) ||
            paths.Any(path => ArchivePath.TryParse(path, out var archivePath) && !archivePath.IsRoot))
        {
            throw new IOException("Archive contents are read-only.");
        }
    }

    private static void ThrowIfArchiveDestination(string destinationDirectory)
    {
        if (ArchivePath.TryParse(destinationDirectory, out _))
        {
            throw new IOException("Archive contents are read-only.");
        }
    }
}
