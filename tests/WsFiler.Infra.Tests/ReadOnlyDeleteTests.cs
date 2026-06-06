using WsFiler.Core.Files;
using WsFiler.Infra.Files;

namespace WsFiler.Infra.Tests;

public sealed class ReadOnlyDeleteTests
{
    [Fact]
    public async Task DeleteAsync_ConfirmsAndDeletesReadOnlyFile()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "readonly.txt");
        await File.WriteAllTextAsync(path, "content");
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);

        var provider = new LocalFileSystemProvider();
        var confirmationCount = 0;

        await provider.DeleteAsync(
            [path],
            info =>
            {
                confirmationCount++;
                Assert.Equal("readonly.txt", info.ItemName);
                Assert.True(info.IsReadOnly);
                Assert.False(info.IsDirectory);
                return Task.FromResult(new FileDeleteConfirmationDecision(
                    FileDeleteConfirmationAction.Delete,
                    ApplyToAll: false));
            });

        Assert.Equal(1, confirmationCount);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task DeleteAsync_SkipsReadOnlyFile_WhenRequested()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "readonly.txt");
        await File.WriteAllTextAsync(path, "content");
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);

        var provider = new LocalFileSystemProvider();

        await provider.DeleteAsync(
            [path],
            _ => Task.FromResult(new FileDeleteConfirmationDecision(
                FileDeleteConfirmationAction.Skip,
                ApplyToAll: false)));

        Assert.True(File.Exists(path));
        File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
