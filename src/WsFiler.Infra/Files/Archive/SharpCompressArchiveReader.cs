using SharpCompress.Archives;
using SharpCompress.Common;
using WsFiler.Core.Files;

namespace WsFiler.Infra.Files.Archive;

internal sealed class SharpCompressArchiveReader : IArchiveReader
{
    private readonly Func<string, IArchive> openArchive;

    public SharpCompressArchiveReader(Func<string, IArchive> openArchive)
    {
        this.openArchive = openArchive;
    }

    public bool CanListDirectory(ArchivePath path)
    {
        if (path.IsRoot)
        {
            return true;
        }

        using var archive = openArchive(path.ArchiveFilePath);
        var normalized = NormalizeEntryName(path.EntryPath);
        var prefix = normalized + "/";

        var exact = archive.Entries.FirstOrDefault(entry =>
            string.Equals(NormalizeEntryName(entry.Key ?? string.Empty), normalized, StringComparison.OrdinalIgnoreCase));
        if (exact is not null && !exact.IsDirectory)
        {
            return false;
        }

        return archive.Entries.Any(entry =>
            NormalizeEntryName(entry.Key ?? string.Empty)
                .StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<FileSystemItem> ListDirectory(ArchivePath path)
    {
        var prefix = string.IsNullOrEmpty(path.EntryPath)
            ? string.Empty
            : path.EntryPath.TrimEnd('/').Replace('\\', '/') + "/";
        var itemsByName = new Dictionary<string, FileSystemItem>(StringComparer.OrdinalIgnoreCase);

        using var archive = openArchive(path.ArchiveFilePath);
        foreach (var entry in archive.Entries)
        {
            var entryName = NormalizeEntryName(entry.Key ?? string.Empty);
            if (entryName.Length == 0 ||
                !entryName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var remaining = entryName[prefix.Length..];
            if (remaining.Length == 0)
            {
                continue;
            }

            var slash = remaining.IndexOf('/');
            var name = slash < 0 ? remaining : remaining[..slash];
            if (name.Length == 0 || itemsByName.ContainsKey(name))
            {
                continue;
            }

            var isDirectory = slash >= 0 || entry.IsDirectory;
            var lastWrite = entry.LastModifiedTime ?? DateTime.MinValue;
            itemsByName[name] = new FileSystemItem(
                name,
                path.Combine(name),
                isDirectory ? FileSystemItemType.Directory : FileSystemItemType.File,
                isDirectory ? null : (entry.Size > 0 ? entry.Size : 0),
                lastWrite,
                isDirectory ? string.Empty : GetExtensionWithoutDot(name),
                IsHidden: false,
                IsReadOnly: true);
        }

        return itemsByName.Values
            .OrderByDescending(item => item.ItemType == FileSystemItemType.Directory)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public Stream OpenRead(ArchivePath path)
    {
        using var archive = openArchive(path.ArchiveFilePath);
        var entry = FindEntry(archive, path.EntryPath)
            ?? throw new FileNotFoundException("Archive entry was not found.", path.EntryPath);

        if (entry.IsDirectory)
        {
            throw new IOException("Archive entry is a directory.");
        }

        var memory = new MemoryStream();
        using (var stream = entry.OpenEntryStream())
        {
            stream.CopyTo(memory);
        }

        memory.Position = 0;
        return memory;
    }

    public async Task ExtractToDirectoryAsync(
        ArchivePath sourcePath,
        string destinationDirectory,
        Func<string, string, bool, Task<FileConflictAction>> resolveConflictAsync,
        CancellationToken cancellationToken)
    {
        using var archive = openArchive(sourcePath.ArchiveFilePath);
        var sourceEntry = FindEntry(archive, sourcePath.EntryPath);
        if (sourceEntry is not null && !sourceEntry.IsDirectory)
        {
            await ExtractFileEntryAsync(
                sourceEntry,
                Path.Combine(destinationDirectory, sourcePath.GetFileName()),
                resolveConflictAsync,
                cancellationToken);
            return;
        }

        var prefix = NormalizeEntryName(sourcePath.EntryPath);
        if (prefix.Length > 0)
        {
            prefix += "/";
        }

        var rootDestination = Path.Combine(destinationDirectory, sourcePath.GetFileName());
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entryName = NormalizeEntryName(entry.Key ?? string.Empty);
            if (entryName.Length == 0 ||
                !entryName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                entry.IsDirectory)
            {
                continue;
            }

            var relative = entryName[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            var destinationPath = Path.Combine(rootDestination, relative);
            await ExtractFileEntryAsync(entry, destinationPath, resolveConflictAsync, cancellationToken);
        }
    }

    private static IArchiveEntry? FindEntry(IArchive archive, string entryPath)
    {
        var normalized = NormalizeEntryName(entryPath);
        if (normalized.Length == 0)
        {
            return null;
        }

        return archive.Entries.FirstOrDefault(entry =>
            string.Equals(NormalizeEntryName(entry.Key ?? string.Empty), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task ExtractFileEntryAsync(
        IArchiveEntry entry,
        string destinationPath,
        Func<string, string, bool, Task<FileConflictAction>> resolveConflictAsync,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)
            ?? throw new IOException("Destination directory was not found."));

        var action = await resolveConflictAsync(entry.Key ?? string.Empty, destinationPath, false);
        if (action == FileConflictAction.Cancel)
        {
            throw new OperationCanceledException();
        }

        if (action == FileConflictAction.Skip)
        {
            return;
        }

        await using var source = entry.OpenEntryStream();
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken);
    }

    private static string NormalizeEntryName(string entryPath) =>
        entryPath.Replace('\\', '/').Trim('/');

    private static string GetExtensionWithoutDot(string name)
    {
        var lastDot = name.LastIndexOf('.');
        return lastDot <= 0 ? string.Empty : name[(lastDot + 1)..];
    }
}
