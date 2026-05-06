namespace WsFiler.Core.Files;

public interface IFileSystemProvider
{
    Task<IReadOnlyList<FileSystemItem>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default);

    Task CopyAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectory,
        Func<FileConflictInfo, Task<FileConflictDecision>> resolveConflictAsync,
        CancellationToken cancellationToken = default);

    Task MoveAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectory,
        Func<FileConflictInfo, Task<FileConflictDecision>> resolveConflictAsync,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        IReadOnlyList<string> targetPaths,
        CancellationToken cancellationToken = default);

    Task RenameAsync(
        string sourcePath,
        string newName,
        CancellationToken cancellationToken = default);

    Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default);

    Task CreateFileAsync(string path, CancellationToken cancellationToken = default);

    Task<FileAttributes> GetAttributesAsync(string path, CancellationToken cancellationToken = default);

    Task SetAttributesAsync(string path, FileAttributes attributes, CancellationToken cancellationToken = default);
}
