using WsFiler.Core.Files;

namespace WsFiler.Infra.Files.Archive;

internal interface IArchiveReader
{
    bool CanListDirectory(ArchivePath path);

    IReadOnlyList<FileSystemItem> ListDirectory(ArchivePath path);

    Stream OpenRead(ArchivePath path);

    Task ExtractToDirectoryAsync(
        ArchivePath sourcePath,
        string destinationDirectory,
        Func<string, string, bool, Task<FileConflictAction>> resolveConflictAsync,
        CancellationToken cancellationToken);
}
