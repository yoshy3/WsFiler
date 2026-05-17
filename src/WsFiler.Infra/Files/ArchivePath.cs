using WsFiler.Infra.Files.Archive;

namespace WsFiler.Infra.Files;

internal sealed record ArchivePath(string ArchiveFilePath, string EntryPath)
{
    private static IReadOnlyList<string> SupportedExtensions => ArchiveReaderFactory.SupportedExtensions;

    public bool IsRoot => string.IsNullOrEmpty(EntryPath);

    public static bool IsSupportedArchiveFile(string path)
    {
        return SupportedExtensions.Any(extension =>
            path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryParse(string path, out ArchivePath archivePath)
    {
        if (File.Exists(path) && IsSupportedArchiveFile(path))
        {
            archivePath = new ArchivePath(path, "");
            return true;
        }

        foreach (var extension in SupportedExtensions)
        {
            var searchStart = 0;
            while (searchStart < path.Length)
            {
                var extensionIndex = path.IndexOf(extension, searchStart, StringComparison.OrdinalIgnoreCase);
                if (extensionIndex < 0)
                {
                    break;
                }

                var separatorIndex = extensionIndex + extension.Length;
                if (separatorIndex < path.Length && path[separatorIndex] == '!')
                {
                    var archiveFilePath = path[..separatorIndex];
                    if (File.Exists(archiveFilePath))
                    {
                        var entryPath = path[(separatorIndex + 1)..]
                            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            .Replace('\\', '/');
                        archivePath = new ArchivePath(archiveFilePath, entryPath);
                        return true;
                    }
                }

                searchStart = extensionIndex + extension.Length;
            }
        }

        archivePath = new ArchivePath("", "");
        return false;
    }

    public string Combine(string name)
    {
        var nextEntryPath = string.IsNullOrEmpty(EntryPath)
            ? name
            : $"{EntryPath.TrimEnd('/')}/{name}";
        return ToVirtualPath(ArchiveFilePath, nextEntryPath);
    }

    public string? GetParentPath()
    {
        if (string.IsNullOrEmpty(EntryPath))
        {
            return Path.GetDirectoryName(ArchiveFilePath);
        }

        var slash = EntryPath.TrimEnd('/').LastIndexOf('/');
        return slash < 0
            ? ArchiveFilePath
            : ToVirtualPath(ArchiveFilePath, EntryPath[..slash]);
    }

    public string GetFileName()
    {
        return string.IsNullOrEmpty(EntryPath)
            ? Path.GetFileName(ArchiveFilePath)
            : EntryPath.TrimEnd('/').Split('/')[^1];
    }

    public static string ToVirtualPath(string archiveFilePath, string entryPath)
    {
        return string.IsNullOrEmpty(entryPath)
            ? archiveFilePath
            : $"{archiveFilePath}!/{entryPath.TrimStart('/')}";
    }
}
