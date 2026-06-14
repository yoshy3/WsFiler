using WsFiler.Infra.Files;

namespace WsFiler.Infra.Tests;

public sealed class LocalFileSystemProviderRenameTests
{
    [Fact]
    public async Task RenameAsync_RenamesFileWhenOnlyCasingChanges()
    {
        var directory = CreateTempDirectory();
        var sourcePath = Path.Combine(directory, "sample.txt");
        await File.WriteAllTextAsync(sourcePath, "content");

        var provider = new LocalFileSystemProvider();

        await provider.RenameAsync(sourcePath, "SAMPLE.txt");

        Assert.Contains("SAMPLE.txt", Directory.EnumerateFiles(directory).Select(Path.GetFileName));
    }

    [Fact]
    public async Task RenameAsync_RenamesDirectoryWhenOnlyCasingChanges()
    {
        var directory = CreateTempDirectory();
        var sourcePath = Path.Combine(directory, "sample");
        Directory.CreateDirectory(sourcePath);

        var provider = new LocalFileSystemProvider();

        await provider.RenameAsync(sourcePath, "SAMPLE");

        Assert.Contains("SAMPLE", Directory.EnumerateDirectories(directory).Select(Path.GetFileName));
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
