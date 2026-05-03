using WsFiler.Core.Files;

namespace WsFiler.Infra.Files;

public sealed class LocalFileSystemProvider : IFileSystemProvider
{
    public Task<IReadOnlyList<FileSystemItem>> ListDirectoryAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var directory = new DirectoryInfo(path);

        if (!directory.Exists)
        {
            return Task.FromResult<IReadOnlyList<FileSystemItem>>([]);
        }

        var items = directory
            .EnumerateFileSystemInfos()
            .Select(ToItem)
            .OrderByDescending(item => item.ItemType == FileSystemItemType.Directory)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<FileSystemItem>>(items);
    }

    private static FileSystemItem ToItem(FileSystemInfo info)
    {
        var attributes = info.Attributes;
        var isDirectory = attributes.HasFlag(FileAttributes.Directory);
        var isSymbolicLink = attributes.HasFlag(FileAttributes.ReparsePoint);
        var type = isSymbolicLink
            ? FileSystemItemType.SymbolicLink
            : isDirectory
                ? FileSystemItemType.Directory
                : FileSystemItemType.File;

        var size = info is FileInfo fileInfo ? fileInfo.Length : (long?)null;

        return new FileSystemItem(
            info.Name,
            info.FullName,
            type,
            size,
            info.LastWriteTime,
            isDirectory ? "" : info.Extension.TrimStart('.'),
            attributes.HasFlag(FileAttributes.Hidden),
            attributes.HasFlag(FileAttributes.ReadOnly));
    }
}
