using System.Text.Json;
using WsFiler.Infra.Settings;

namespace WsFiler.Infra.Tests;

public sealed class SettingsManagerTests
{
    [Fact]
    public void AppSettings_RoundTripsSortSettingsCorrectly()
    {
        // Arrange
        var settings = new AppSettings
        {
            LastSession = new LastSessionSettings
            {
                LeftPath = "/left/path",
                RightPath = "/right/path",
                LeftSortField = "Size",
                LeftSortAscending = false,
                RightSortField = "Extension",
                RightSortAscending = true
            }
        };

        // Act
        var json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);
        var deserialized = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.LastSession);
        Assert.Equal("/left/path", deserialized.LastSession.LeftPath);
        Assert.Equal("/right/path", deserialized.LastSession.RightPath);
        Assert.Equal("Size", deserialized.LastSession.LeftSortField);
        Assert.False(deserialized.LastSession.LeftSortAscending);
        Assert.Equal("Extension", deserialized.LastSession.RightSortField);
        Assert.True(deserialized.LastSession.RightSortAscending);
    }
}
