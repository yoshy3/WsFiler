using WsFiler.Core.Files;

namespace WsFiler.Infra.Files.Archive;

internal sealed class SingleFileCompressionReader : IArchiveReader
{
    private readonly Func<Stream, Stream> openDecompressionStream;
    private readonly string strippedExtension;

    public SingleFileCompressionReader(Func<Stream, Stream> openDecompressionStream, string strippedExtension)
    {
        this.openDecompressionStream = openDecompressionStream;
        this.strippedExtension = strippedExtension;
    }

    public bool CanListDirectory(ArchivePath path) => path.IsRoot;

    public IReadOnlyList<FileSystemItem> ListDirectory(ArchivePath path)
    {
        if (!path.IsRoot)
        {
            return [];
        }

        var name = GetInnerFileName(path.ArchiveFilePath);
        var fileInfo = new FileInfo(path.ArchiveFilePath);
        var lastWrite = fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.MinValue;

        return [new FileSystemItem(
            name,
            path.Combine(name),
            FileSystemItemType.File,
            Size: null,
            lastWrite,
            GetExtensionWithoutDot(name),
            IsHidden: false,
            IsReadOnly: true)];
    }

    public Stream OpenRead(ArchivePath path)
    {
        var expected = GetInnerFileName(path.ArchiveFilePath);
        var normalized = path.EntryPath.Replace('\\', '/').Trim('/');
        if (!string.Equals(normalized, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new FileNotFoundException("Archive entry was not found.", path.EntryPath);
        }

        using var source = File.OpenRead(path.ArchiveFilePath);
        using var decompressed = openDecompressionStream(source);
        var memory = new MemoryStream();
        decompressed.CopyTo(memory);
        memory.Position = 0;
        return memory;
    }

    public async Task ExtractToDirectoryAsync(
        ArchivePath sourcePath,
        string destinationDirectory,
        Func<string, string, bool, Task<FileConflictAction>> resolveConflictAsync,
        CancellationToken cancellationToken)
    {
        var innerName = GetInnerFileName(sourcePath.ArchiveFilePath);

        string destinationPath;
        if (sourcePath.IsRoot)
        {
            var rootDestination = Path.Combine(destinationDirectory, sourcePath.GetFileName());
            Directory.CreateDirectory(rootDestination);
            destinationPath = Path.Combine(rootDestination, innerName);
        }
        else
        {
            destinationPath = Path.Combine(destinationDirectory, innerName);
            Directory.CreateDirectory(destinationDirectory);
        }

        var action = await resolveConflictAsync(innerName, destinationPath, false);
        if (action == FileConflictAction.Cancel)
        {
            throw new OperationCanceledException();
        }

        if (action == FileConflictAction.Skip)
        {
            return;
        }

        await using var source = File.OpenRead(sourcePath.ArchiveFilePath);
        await using var decompressed = openDecompressionStream(source);
        await using var destination = File.Create(destinationPath);
        await decompressed.CopyToAsync(destination, cancellationToken);
    }

    private string GetInnerFileName(string archiveFilePath)
    {
        var fileName = Path.GetFileName(archiveFilePath);
        if (fileName.EndsWith(strippedExtension, StringComparison.OrdinalIgnoreCase))
        {
            return fileName[..^strippedExtension.Length];
        }

        return fileName;
    }

    private static string GetExtensionWithoutDot(string name)
    {
        var lastDot = name.LastIndexOf('.');
        return lastDot <= 0 ? string.Empty : name[(lastDot + 1)..];
    }
}
