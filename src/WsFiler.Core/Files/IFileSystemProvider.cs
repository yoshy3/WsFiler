namespace WsFiler.Core.Files;

public interface IFileSystemProvider
{
    Task<IReadOnlyList<FileSystemItem>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default);

    Task<bool> CanListDirectoryAsync(string path, CancellationToken cancellationToken = default);

    string? GetParentPath(string path);

    string GetFileName(string path);

    Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default);

    Task CopyAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectory,
        Func<FileConflictInfo, Task<FileConflictDecision>> resolveConflictAsync,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task MoveAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectory,
        Func<FileConflictInfo, Task<FileConflictDecision>> resolveConflictAsync,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        IReadOnlyList<string> targetPaths,
        Func<FileDeleteConfirmationInfo, Task<FileDeleteConfirmationDecision>>? confirmDeleteAsync = null,
        CancellationToken cancellationToken = default);

    Task RenameAsync(
        string sourcePath,
        string newName,
        CancellationToken cancellationToken = default);

    Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default);

    Task CreateFileAsync(string path, CancellationToken cancellationToken = default);

    Task<FileAttributes> GetAttributesAsync(string path, CancellationToken cancellationToken = default);

    Task SetAttributesAsync(string path, FileAttributes attributes, CancellationToken cancellationToken = default);

    Task<UnixFileMode> GetUnixFileModeAsync(string path, CancellationToken cancellationToken = default);

    Task SetUnixFileModeAsync(string path, UnixFileMode mode, CancellationToken cancellationToken = default);

    Task<bool> CanSetUnixFileModeAsync(string path, CancellationToken cancellationToken = default);
}
