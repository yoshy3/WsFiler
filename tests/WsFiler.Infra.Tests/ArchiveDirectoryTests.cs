using System.IO.Compression;
using WsFiler.Core.Files;
using WsFiler.Infra.Files;

namespace WsFiler.Infra.Tests;

public sealed class ArchiveDirectoryTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public ArchiveDirectoryTests()
    {
        Directory.CreateDirectory(tempDirectory);
    }

    [Fact]
    public async Task ListDirectoryAsync_ReturnsZipRootEntries()
    {
        var zipPath = CreateZip();
        var provider = new LocalFileSystemProvider();

        var items = await provider.ListDirectoryAsync(zipPath);

        Assert.Collection(
            items,
            item =>
            {
                Assert.Equal("folder", item.Name);
                Assert.True(item.IsDirectory);
                Assert.Equal($"{zipPath}!/folder", item.FullPath);
            },
            item =>
            {
                Assert.Equal("root.txt", item.Name);
                Assert.False(item.IsDirectory);
                Assert.Equal($"{zipPath}!/root.txt", item.FullPath);
                Assert.Equal(4, item.Size);
            });
    }

    [Fact]
    public async Task ListDirectoryAsync_ReturnsZipNestedEntries()
    {
        var zipPath = CreateZip();
        var provider = new LocalFileSystemProvider();

        var items = await provider.ListDirectoryAsync($"{zipPath}!/folder");

        var item = Assert.Single(items);
        Assert.Equal("child.txt", item.Name);
        Assert.Equal($"{zipPath}!/folder/child.txt", item.FullPath);
        Assert.Equal(5, item.Size);
    }

    [Fact]
    public async Task CanListDirectoryAsync_ReturnsTrueForZipDirectoriesOnly()
    {
        var zipPath = CreateZip();
        var provider = new LocalFileSystemProvider();

        Assert.True(await provider.CanListDirectoryAsync(zipPath));
        Assert.True(await provider.CanListDirectoryAsync($"{zipPath}!/folder"));
        Assert.False(await provider.CanListDirectoryAsync($"{zipPath}!/folder/child.txt"));
        Assert.False(await provider.CanListDirectoryAsync($"{zipPath}!/root.txt"));
    }

    [Fact]
    public void GetParentPath_ReturnsArchiveAwareParent()
    {
        var zipPath = CreateZip();
        var provider = new LocalFileSystemProvider();

        Assert.Equal(tempDirectory, provider.GetParentPath(zipPath));
        Assert.Equal(zipPath, provider.GetParentPath($"{zipPath}!/folder"));
    }

    [Fact]
    public async Task OpenReadAsync_ReadsZipEntry()
    {
        var zipPath = CreateZip();
        var provider = new LocalFileSystemProvider();

        await using var stream = await provider.OpenReadAsync($"{zipPath}!/folder/child.txt");
        using var reader = new StreamReader(stream);

        Assert.Equal("child", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task CopyAsync_ExtractsZipEntryToDirectory()
    {
        var zipPath = CreateZip();
        var provider = new LocalFileSystemProvider();
        var destination = Path.Combine(tempDirectory, "destination");

        await provider.CopyAsync(
            [$"{zipPath}!/folder/child.txt"],
            destination,
            _ => Task.FromResult(new FileConflictDecision(FileConflictAction.Overwrite, ApplyToAll: false)));

        Assert.Equal("child", await File.ReadAllTextAsync(Path.Combine(destination, "child.txt")));
    }

    [Fact]
    public async Task CopyAsync_ExtractsZipDirectoryToDirectory()
    {
        var zipPath = CreateZip();
        var provider = new LocalFileSystemProvider();
        var destination = Path.Combine(tempDirectory, "destination");

        await provider.CopyAsync(
            [$"{zipPath}!/folder"],
            destination,
            _ => Task.FromResult(new FileConflictDecision(FileConflictAction.Overwrite, ApplyToAll: false)));

        Assert.Equal("child", await File.ReadAllTextAsync(Path.Combine(destination, "folder", "child.txt")));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private string CreateZip()
    {
        var zipPath = Path.Combine(tempDirectory, "sample.zip");
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        AddEntry(archive, "root.txt", "root");
        AddEntry(archive, "folder/child.txt", "child");
        return zipPath;
    }

    private static void AddEntry(ZipArchive archive, string entryName, string contents)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(contents);
    }
}
