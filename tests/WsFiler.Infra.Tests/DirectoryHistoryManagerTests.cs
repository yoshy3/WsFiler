using WsFiler.Infra.Settings;

namespace WsFiler.Infra.Tests;

public sealed class DirectoryHistoryManagerTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsDirectoryHistory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "directory-history.json");
        var expected = new[] { "/tmp/one", "/tmp/two" };

        DirectoryHistoryManager.Save(path, expected);

        Assert.Equal(expected, DirectoryHistoryManager.Load(path));
    }

    [Fact]
    public void Load_ReturnsEmptyList_WhenFileDoesNotExist()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "directory-history.json");

        Assert.Empty(DirectoryHistoryManager.Load(path));
    }
}
