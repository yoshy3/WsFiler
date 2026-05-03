namespace WsFiler.Core.Files;

public interface IFileSystemProvider
{
    Task<IReadOnlyList<FileSystemItem>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default);

    Task CopyAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectory,
        CancellationToken cancellationToken = default);

    Task MoveAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectory,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        IReadOnlyList<string> targetPaths,
        CancellationToken cancellationToken = default);

    Task RenameAsync(
        string sourcePath,
        string newName,
        CancellationToken cancellationToken = default);
}
