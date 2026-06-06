namespace WsFiler.Infra.Updates;

public sealed record GitHubReleaseInfo(
    string Version,
    string Name,
    string ReleaseUrl);
