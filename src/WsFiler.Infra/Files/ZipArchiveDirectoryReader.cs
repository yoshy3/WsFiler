using System.IO.Compression;
using WsFiler.Core.Files;

namespace WsFiler.Infra.Files;

internal static class ZipArchiveDirectoryReader
{
    public static bool CanListDirectory(ArchivePath path)
    {
        if (path.IsRoot)
        {
            return true;
        }

        var prefix = path.EntryPath.Trim('/').Replace('\\', '/') + "/";
        using var archive = ZipFile.OpenRead(path.ArchiveFilePath);
        var exactEntry = FindEntry(archive, path.EntryPath);
        if (exactEntry is not null && !exactEntry.FullName.EndsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        return archive.Entries.Any(entry =>
            entry.FullName.Replace('\\', '/').TrimStart('/')
                .StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<FileSystemItem> ListDirectory(ArchivePath path)
    {
        var prefix = string.IsNullOrEmpty(path.EntryPath)
            ? ""
            : path.EntryPath.TrimEnd('/') + "/";
        var itemsByName = new Dictionary<string, FileSystemItem>(StringComparer.OrdinalIgnoreCase);

        using var archive = ZipFile.OpenRead(path.ArchiveFilePath);
        foreach (var entry in archive.Entries)
        {
            var entryName = entry.FullName.Replace('\\', '/').TrimStart('/');
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

            var isDirectory = slash >= 0 || entryName.EndsWith("/", StringComparison.Ordinal);
            itemsByName[name] = new FileSystemItem(
                name,
                path.Combine(name),
                isDirectory ? FileSystemItemType.Directory : FileSystemItemType.File,
                isDirectory ? null : entry.Length,
                entry.LastWriteTime,
                isDirectory ? "" : GetExtensionWithoutDot(name),
                IsHidden: false,
                IsReadOnly: true);
        }

        return itemsByName.Values
            .OrderByDescending(item => item.ItemType == FileSystemItemType.Directory)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static Stream OpenRead(ArchivePath path)
    {
        using var archive = ZipFile.OpenRead(path.ArchiveFilePath);
        var entry = FindEntry(archive, path.EntryPath)
            ?? throw new FileNotFoundException("Archive entry was not found.", path.EntryPath);

        if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
        {
            throw new IOException("Archive entry is a directory.");
        }

        var memory = new MemoryStream();
        using (var stream = entry.Open())
        {
            stream.CopyTo(memory);
        }

        memory.Position = 0;
        return memory;
    }

    public static async Task ExtractToDirectoryAsync(
        ArchivePath sourcePath,
        string destinationDirectory,
        Func<string, string, bool, Task<FileConflictAction>> resolveConflictAsync,
        CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(sourcePath.ArchiveFilePath);
        var sourceEntry = FindEntry(archive, sourcePath.EntryPath);
        if (sourceEntry is not null && !sourceEntry.FullName.EndsWith("/", StringComparison.Ordinal))
        {
            await ExtractFileEntryAsync(sourceEntry, Path.Combine(destinationDirectory, sourcePath.GetFileName()), resolveConflictAsync, cancellationToken);
            return;
        }

        var prefix = sourcePath.EntryPath.TrimEnd('/');
        if (prefix.Length > 0)
        {
            prefix += "/";
        }

        var rootDestination = Path.Combine(destinationDirectory, sourcePath.GetFileName());
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entryName = entry.FullName.Replace('\\', '/').TrimStart('/');
            if (entryName.Length == 0 ||
                !entryName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                entryName.EndsWith("/", StringComparison.Ordinal))
            {
                continue;
            }

            var relative = entryName[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            var destinationPath = Path.Combine(rootDestination, relative);
            await ExtractFileEntryAsync(entry, destinationPath, resolveConflictAsync, cancellationToken);
        }
    }

    private static ZipArchiveEntry? FindEntry(ZipArchive archive, string entryPath)
    {
        var normalized = entryPath.Trim('/').Replace('\\', '/');
        if (normalized.Length == 0)
        {
            return null;
        }

        return archive.Entries.FirstOrDefault(entry =>
            string.Equals(entry.FullName.Trim('/').Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task ExtractFileEntryAsync(
        ZipArchiveEntry entry,
        string destinationPath,
        Func<string, string, bool, Task<FileConflictAction>> resolveConflictAsync,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)
            ?? throw new IOException("Destination directory was not found."));

        var action = await resolveConflictAsync(entry.FullName, destinationPath, false);
        if (action == FileConflictAction.Cancel)
        {
            throw new OperationCanceledException();
        }

        if (action == FileConflictAction.Skip)
        {
            return;
        }

        await using var source = entry.Open();
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken);
    }

    private static string GetExtensionWithoutDot(string name)
    {
        var lastDot = name.LastIndexOf('.');
        return lastDot <= 0 ? string.Empty : name[(lastDot + 1)..];
    }
}
