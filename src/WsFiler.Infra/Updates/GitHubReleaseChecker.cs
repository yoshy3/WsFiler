using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace WsFiler.Infra.Updates;

public sealed class GitHubReleaseChecker
{
    public const string LatestReleaseApiUrl = "https://api.github.com/repos/yoshy3/WsFiler/releases/latest";
    public const string ReleasesUrl = "https://github.com/yoshy3/WsFiler/releases";

    private readonly HttpClient httpClient;

    public GitHubReleaseChecker()
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
    {
    }

    public GitHubReleaseChecker(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<GitHubReleaseInfo?> CheckLatestAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
        request.Headers.UserAgent.ParseAdd(BuildUserAgent());

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var release = await JsonSerializer.DeserializeAsync(
            stream,
            UpdateJsonContext.Default.GitHubReleaseApiResponse,
            cancellationToken).ConfigureAwait(false);

        if (release is null ||
            string.IsNullOrWhiteSpace(release.TagName) ||
            string.IsNullOrWhiteSpace(release.HtmlUrl) ||
            !IsNewerVersion(release.TagName, currentVersion))
        {
            return null;
        }

        return new GitHubReleaseInfo(
            NormalizeVersionText(release.TagName),
            string.IsNullOrWhiteSpace(release.Name) ? release.TagName : release.Name,
            release.HtmlUrl);
    }

    public static bool IsNewerVersion(string candidateVersion, string currentVersion)
    {
        var candidate = ParseVersionParts(candidateVersion);
        var current = ParseVersionParts(currentVersion);
        if (candidate is null || current is null)
        {
            return false;
        }

        for (var i = 0; i < Math.Max(candidate.Length, current.Length); i++)
        {
            var left = i < candidate.Length ? candidate[i] : 0;
            var right = i < current.Length ? current[i] : 0;
            if (left != right)
            {
                return left > right;
            }
        }

        return false;
    }

    public static string NormalizeVersionText(string version)
    {
        var normalized = version.Split('+', 2)[0].Trim();
        return normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? normalized[1..]
            : normalized;
    }

    private static int[]? ParseVersionParts(string version)
    {
        var normalized = NormalizeVersionText(version);
        var prereleaseIndex = normalized.IndexOf('-', StringComparison.Ordinal);
        if (prereleaseIndex >= 0)
        {
            normalized = normalized[..prereleaseIndex];
        }

        var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var values = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out values[i]) || values[i] < 0)
            {
                return null;
            }
        }

        return values;
    }

    private static string BuildUserAgent()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0";
        return $"WsFiler/{version}";
    }
}

internal sealed class GitHubReleaseApiResponse
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GitHubReleaseApiResponse))]
internal partial class UpdateJsonContext : JsonSerializerContext
{
}
