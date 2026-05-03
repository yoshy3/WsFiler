namespace WsFiler.Core.Files;

public interface IFileSystemProvider
{
    Task<IReadOnlyList<FileSystemItem>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default);
}
