using WsFiler.Infra.Updates;

public sealed class UpdateAssetSelectorTests
{
    [Fact]
    public void Select_PicksWindowsX64Setup()
    {
        var assets = new[]
        {
            new GitHubReleaseAsset("WsFiler-1.2.3-any-platform.zip", "https://example.com/any"),
            new GitHubReleaseAsset("WsFiler-1.2.3-win-x64-setup.exe", "https://example.com/win"),
            new GitHubReleaseAsset("wsfiler_1.2.3_amd64.deb", "https://example.com/linux"),
        };

        var selected = UpdateAssetSelector.Select(assets, "win", "x64");

        Assert.Equal("https://example.com/win", selected?.DownloadUrl);
    }

    [Fact]
    public void Select_PicksLinuxAmd64DebForX64()
    {
        var assets = new[]
        {
            new GitHubReleaseAsset("WsFiler-1.2.3-win-x64-setup.exe", "https://example.com/win"),
            new GitHubReleaseAsset("wsfiler_1.2.3_amd64.deb", "https://example.com/linux"),
        };

        var selected = UpdateAssetSelector.Select(assets, "linux", "x64");

        Assert.Equal("https://example.com/linux", selected?.DownloadUrl);
    }

    [Fact]
    public void Select_PicksMacArm64Dmg()
    {
        var assets = new[]
        {
            new GitHubReleaseAsset("WsFiler-1.2.3-osx-arm64.dmg", "https://example.com/mac"),
            new GitHubReleaseAsset("WsFiler-1.2.3-win-x64-setup.exe", "https://example.com/win"),
        };

        var selected = UpdateAssetSelector.Select(assets, "osx", "arm64");

        Assert.Equal("https://example.com/mac", selected?.DownloadUrl);
    }

    [Fact]
    public void Select_IgnoresAnyPlatformAsset()
    {
        var assets = new[]
        {
            new GitHubReleaseAsset("WsFiler-1.2.3-any-platform.zip", "https://example.com/any"),
        };

        var selected = UpdateAssetSelector.Select(assets, "win", "x64");

        Assert.Null(selected);
    }
}
