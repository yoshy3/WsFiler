using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace WsFiler.Infra.Updates;

public static class UpdateAssetSelector
{
    public static GitHubReleaseAsset? SelectForCurrentPlatform(IReadOnlyList<GitHubReleaseAsset> assets)
    {
        if (assets.Count == 0)
        {
            return null;
        }

        var os = CurrentOperatingSystemToken();
        var arch = CurrentArchitectureToken();
        if (os is null || arch is null)
        {
            return null;
        }

        return Select(assets, os, arch);
    }

    public static GitHubReleaseAsset? Select(IReadOnlyList<GitHubReleaseAsset> assets, string os, string arch)
    {
        var candidates = assets
            .Select(asset => new ScoredAsset(asset, Score(asset.Name, os, arch)))
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Asset.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return candidates.Count == 0 ? null : candidates[0].Asset;
    }

    private static int Score(string assetName, string os, string arch)
    {
        var name = assetName.ToLowerInvariant();
        if (name.Contains("any-platform", StringComparison.Ordinal) ||
            name.Contains("any_platform", StringComparison.Ordinal) ||
            name.Contains("any platform", StringComparison.Ordinal) ||
            name.Contains("anyplatform", StringComparison.Ordinal))
        {
            return 0;
        }

        var score = os switch
        {
            "win" when HasAny(name, "win", "windows") && EndsWithAny(name, ".exe", ".msi") => 100,
            "osx" when HasAny(name, "osx", "mac", "macos", "darwin") && EndsWithAny(name, ".dmg", ".pkg") => 100,
            "linux" when HasAny(name, "linux", "ubuntu", "debian", "deb") && EndsWithAny(name, ".deb", ".rpm", ".appimage") => 100,
            _ => 0,
        };

        if (score == 0)
        {
            return 0;
        }

        if (HasArchitecture(name, arch))
        {
            score += 30;
        }
        else if (HasAnyArchitecture(name))
        {
            return 0;
        }

        if (HasAny(name, "setup", "installer", "install"))
        {
            score += 10;
        }

        return score;
    }

    private static string? CurrentOperatingSystemToken()
    {
        if (OperatingSystem.IsWindows())
        {
            return "win";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "osx";
        }

        if (OperatingSystem.IsLinux())
        {
            return "linux";
        }

        return null;
    }

    private static string? CurrentArchitectureToken() => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => "x64",
        Architecture.Arm64 => "arm64",
        Architecture.X86 => "x86",
        Architecture.Arm => "arm",
        _ => null,
    };

    private static bool HasArchitecture(string name, string arch) => arch switch
    {
        "x64" => HasAny(name, "x64", "x86_64", "amd64"),
        "arm64" => HasAny(name, "arm64", "aarch64"),
        "x86" => HasAny(name, "x86", "i386", "i686"),
        "arm" => HasAny(name, "arm"),
        _ => false,
    };

    private static bool HasAnyArchitecture(string name) =>
        HasAny(name, "x64", "x86_64", "amd64", "arm64", "aarch64", "x86", "i386", "i686", "arm");

    private static bool HasAny(string value, params string[] needles) =>
        needles.Any(needle => value.Contains(needle, StringComparison.Ordinal));

    private static bool EndsWithAny(string value, params string[] suffixes) =>
        suffixes.Any(suffix => value.EndsWith(suffix, StringComparison.Ordinal));

    private sealed record ScoredAsset(GitHubReleaseAsset Asset, int Score);
}
