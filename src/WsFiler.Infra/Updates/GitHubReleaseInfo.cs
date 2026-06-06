namespace WsFiler.Infra.Updates;

public sealed record GitHubReleaseInfo(
    string Version,
    string Name,
    string ReleaseUrl,
    IReadOnlyList<GitHubReleaseAsset> Assets);

public sealed record GitHubReleaseAsset(
    string Name,
    string DownloadUrl);
