using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace WsFiler.Infra.Updates;

public sealed class UpdateAssetDownloader
{
    private readonly HttpClient httpClient;

    public UpdateAssetDownloader()
        : this(new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
    {
    }

    public UpdateAssetDownloader(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<string> DownloadAsync(GitHubReleaseAsset asset, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUrl);
        request.Headers.UserAgent.ParseAdd(BuildUserAgent());

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var downloadDirectory = Path.Combine(Path.GetTempPath(), "WsFiler", "updates");
        Directory.CreateDirectory(downloadDirectory);

        var destinationPath = Path.Combine(downloadDirectory, SafeFileName(asset.Name));
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

        return destinationPath;
    }

    private static string SafeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "WsFiler-update" : name;
    }

    private static string BuildUserAgent()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0";
        return $"WsFiler/{version}";
    }
}
