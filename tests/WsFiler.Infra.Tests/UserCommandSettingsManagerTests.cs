using WsFiler.Infra.Settings;

namespace WsFiler.Infra.Tests;

public sealed class UserCommandSettingsManagerTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsUserCommands()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "user-commands.json");
        var commands = new[]
        {
            new UserCommandEntry
            {
                Name = "Edit",
                ExecutablePath = @"C:\Tools\edit.exe",
                Arguments = "\"{currentFullPath}\"",
                WorkingDirectoryMode = UserCommandEntry.WorkingDirectoryExecutable,
            },
        };

        UserCommandSettingsManager.Save(path, commands);

        var loaded = UserCommandSettingsManager.Load(path);
        Assert.Single(loaded);
        Assert.Equal("Edit", loaded[0].Name);
        Assert.Equal(@"C:\Tools\edit.exe", loaded[0].ExecutablePath);
        Assert.Equal("\"{currentFullPath}\"", loaded[0].Arguments);
        Assert.Equal(UserCommandEntry.WorkingDirectoryExecutable, loaded[0].WorkingDirectoryMode);
    }

    [Fact]
    public void Load_ReturnsEmptyList_WhenFileDoesNotExist()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "user-commands.json");

        Assert.Empty(UserCommandSettingsManager.Load(path));
    }

    [Fact]
    public void Load_ReturnsEmptyList_WhenJsonIsInvalid()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "user-commands.json");
        File.WriteAllText(path, "{ invalid");

        Assert.Empty(UserCommandSettingsManager.Load(path));
    }
}
