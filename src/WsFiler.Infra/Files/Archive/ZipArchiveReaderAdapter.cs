using WsFiler.Core.Files;

namespace WsFiler.Infra.Files.Archive;

internal sealed class ZipArchiveReaderAdapter : IArchiveReader
{
    public bool CanListDirectory(ArchivePath path) =>
        ZipArchiveDirectoryReader.CanListDirectory(path);

    public IReadOnlyList<FileSystemItem> ListDirectory(ArchivePath path) =>
        ZipArchiveDirectoryReader.ListDirectory(path);

    public Stream OpenRead(ArchivePath path) =>
        ZipArchiveDirectoryReader.OpenRead(path);

    public Task ExtractToDirectoryAsync(
        ArchivePath sourcePath,
        string destinationDirectory,
        Func<string, string, bool, Task<FileConflictAction>> resolveConflictAsync,
        CancellationToken cancellationToken) =>
        ZipArchiveDirectoryReader.ExtractToDirectoryAsync(
            sourcePath, destinationDirectory, resolveConflictAsync, cancellationToken);
}
