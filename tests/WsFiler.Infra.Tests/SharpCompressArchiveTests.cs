using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;
using WsFiler.Core.Files;
using WsFiler.Infra.Files;

namespace WsFiler.Infra.Tests;

public sealed class SharpCompressArchiveTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public SharpCompressArchiveTests()
    {
        Directory.CreateDirectory(tempDirectory);
    }

    [Fact]
    public async Task ListDirectoryAsync_ReadsTarRoot()
    {
        var path = CreateTar();
        var provider = new LocalFileSystemProvider();

        var items = await provider.ListDirectoryAsync(path);

        Assert.Contains(items, item => item.Name == "root.txt" && !item.IsDirectory);
        Assert.Contains(items, item => item.Name == "folder" && item.IsDirectory);
    }

    [Fact]
    public async Task OpenReadAsync_ReadsTarEntry()
    {
        var path = CreateTar();
        var provider = new LocalFileSystemProvider();

        await using var stream = await provider.OpenReadAsync($"{path}!/folder/child.txt");
        using var reader = new StreamReader(stream);

        Assert.Equal("child", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task ListDirectoryAsync_ReadsTarGzNested()
    {
        var path = CreateTarGz();
        var provider = new LocalFileSystemProvider();

        var items = await provider.ListDirectoryAsync($"{path}!/folder");

        var item = Assert.Single(items);
        Assert.Equal("child.txt", item.Name);
    }

    [Fact]
    public async Task OpenReadAsync_ReadsTarGzEntry()
    {
        var path = CreateTarGz();
        var provider = new LocalFileSystemProvider();

        await using var stream = await provider.OpenReadAsync($"{path}!/root.txt");
        using var reader = new StreamReader(stream);

        Assert.Equal("root", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task CanListDirectoryAsync_ReturnsTrueForTarGzRoot()
    {
        var path = CreateTarGz();
        var provider = new LocalFileSystemProvider();

        Assert.True(await provider.CanListDirectoryAsync(path));
    }

    [Fact]
    public async Task CanListDirectoryAsync_ReturnsTrueForTgzRoot()
    {
        var tarPath = CreateTar();
        var path = Path.Combine(tempDirectory, "sample.tgz");
        using (var input = File.OpenRead(tarPath))
        using (var output = File.Create(path))
        using (var gz = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionMode.Compress))
        {
            input.CopyTo(gz);
        }

        var provider = new LocalFileSystemProvider();
        Assert.True(await provider.CanListDirectoryAsync(path));
    }

    [Fact]
    public async Task CanListDirectoryAsync_ReturnsTrueForSingleGzRoot()
    {
        var path = CreateSingleGz("hello.txt", "hello");
        var provider = new LocalFileSystemProvider();

        Assert.True(await provider.CanListDirectoryAsync(path));
    }

    [Fact]
    public async Task CopyAsync_ExtractsTarGzEntry()
    {
        var path = CreateTarGz();
        var provider = new LocalFileSystemProvider();
        var destination = Path.Combine(tempDirectory, "out");

        await provider.CopyAsync(
            [$"{path}!/folder/child.txt"],
            destination,
            _ => Task.FromResult(new FileConflictDecision(FileConflictAction.Overwrite, ApplyToAll: false)));

        Assert.Equal("child", await File.ReadAllTextAsync(Path.Combine(destination, "child.txt")));
    }

    [Fact]
    public async Task ListDirectoryAsync_ReadsSingleFileGz()
    {
        var path = CreateSingleGz("hello.txt", "hello world");
        var provider = new LocalFileSystemProvider();

        var items = await provider.ListDirectoryAsync(path);

        var item = Assert.Single(items);
        Assert.Equal("hello.txt", item.Name);
        Assert.False(item.IsDirectory);
    }

    [Fact]
    public async Task OpenReadAsync_ReadsSingleFileGz()
    {
        var path = CreateSingleGz("hello.txt", "hello world");
        var provider = new LocalFileSystemProvider();

        await using var stream = await provider.OpenReadAsync($"{path}!/hello.txt");
        using var reader = new StreamReader(stream);

        Assert.Equal("hello world", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task CopyAsync_ExtractsSingleFileGz()
    {
        var path = CreateSingleGz("hello.txt", "hello world");
        var provider = new LocalFileSystemProvider();
        var destination = Path.Combine(tempDirectory, "out");

        await provider.CopyAsync(
            [$"{path}!/hello.txt"],
            destination,
            _ => Task.FromResult(new FileConflictDecision(FileConflictAction.Overwrite, ApplyToAll: false)));

        Assert.Equal("hello world", await File.ReadAllTextAsync(Path.Combine(destination, "hello.txt")));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private string CreateTar()
    {
        var path = Path.Combine(tempDirectory, "sample.tar");
        using var output = File.Create(path);
        using var writer = new TarWriter(output, new TarWriterOptions(CompressionType.None, finalizeArchiveOnClose: true));
        WriteTarEntry(writer, "root.txt", "root");
        WriteTarEntry(writer, "folder/child.txt", "child");
        return path;
    }

    private static void WriteTarEntry(TarWriter writer, string entryName, string contents)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(contents);
        using var memory = new MemoryStream(bytes);
        writer.Write(entryName, memory, modificationTime: DateTime.UtcNow, size: bytes.Length);
    }

    private string CreateTarGz()
    {
        var tarPath = CreateTar();
        var path = Path.Combine(tempDirectory, "sample.tar.gz");
        using (var input = File.OpenRead(tarPath))
        using (var output = File.Create(path))
        using (var gz = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionMode.Compress))
        {
            input.CopyTo(gz);
        }
        return path;
    }

    private string CreateSingleGz(string innerName, string contents)
    {
        var path = Path.Combine(tempDirectory, innerName + ".gz");
        using var output = File.Create(path);
        using var gz = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionMode.Compress);
        using var writer = new StreamWriter(gz);
        writer.Write(contents);
        return path;
    }

}
