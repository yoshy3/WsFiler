using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WsFiler.App.Shell;

internal static class MacSmbPathResolver
{
    public static async Task<string> ResolveAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsMacOS() || !TryParseSmbUri(path, out var smbUri))
        {
            return path;
        }

        var share = GetShareName(smbUri);
        var mountPoint = await FindMountPointAsync(smbUri.Host, share, cancellationToken);
        if (mountPoint is null)
        {
            await MountShareAsync(GetShareUri(smbUri), cancellationToken);
            mountPoint = await WaitForMountPointAsync(smbUri.Host, share, cancellationToken);
        }

        return CombineMountPointAndRelativePath(mountPoint, GetPathAfterShare(smbUri));
    }

    private static bool TryParseSmbUri(string path, out Uri uri)
    {
        return Uri.TryCreate(path, UriKind.Absolute, out uri!) &&
            string.Equals(uri.Scheme, "smb", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(uri.Host) &&
            uri.Segments.Length >= 2;
    }

    private static Uri GetShareUri(Uri uri)
    {
        var builder = new UriBuilder(uri.Scheme, uri.Host)
        {
            Path = GetShareName(uri),
        };

        return builder.Uri;
    }

    private static string GetShareName(Uri uri)
    {
        return Uri.UnescapeDataString(uri.Segments[1].TrimEnd('/'));
    }

    private static string GetPathAfterShare(Uri uri)
    {
        if (uri.Segments.Length <= 2)
        {
            return string.Empty;
        }

        return string.Join(
            Path.DirectorySeparatorChar,
            uri.Segments
                .Skip(2)
                .Select(segment => Uri.UnescapeDataString(segment.TrimEnd('/')))
                .Where(segment => segment.Length > 0));
    }

    private static async Task<string> WaitForMountPointAsync(
        string host,
        string share,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        do
        {
            var mountPoint = await FindMountPointAsync(host, share, cancellationToken);
            if (mountPoint is not null)
            {
                return mountPoint;
            }

            await Task.Delay(250, cancellationToken);
        }
        while (DateTimeOffset.UtcNow < deadline);

        throw new IOException($"SMB share was not mounted: smb://{host}/{share}");
    }

    private static async Task MountShareAsync(Uri shareUri, CancellationToken cancellationToken)
    {
        var escaped = shareUri.AbsoluteUri.Replace("\"", "\\\"", StringComparison.Ordinal);
        await RunProcessAsync("/usr/bin/osascript", ["-e", $"mount volume \"{escaped}\""], cancellationToken);
    }

    private static async Task<string?> FindMountPointAsync(
        string host,
        string share,
        CancellationToken cancellationToken)
    {
        var output = await RunProcessAsync("/sbin/mount", [], cancellationToken);
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var entry = ParseMountEntry(line);
            if (entry is null)
            {
                continue;
            }

            if (IsSameSmbShare(entry.Value.Source, host, share) &&
                Directory.Exists(entry.Value.MountPoint))
            {
                return entry.Value.MountPoint;
            }
        }

        return null;
    }

    private static (string Source, string MountPoint)? ParseMountEntry(string line)
    {
        var onIndex = line.IndexOf(" on ", StringComparison.Ordinal);
        var optionsIndex = line.LastIndexOf(" (", StringComparison.Ordinal);
        if (onIndex <= 0 || optionsIndex <= onIndex)
        {
            return null;
        }

        return (line[..onIndex], line[(onIndex + 4)..optionsIndex]);
    }

    private static bool IsSameSmbShare(string source, string host, string share)
    {
        if (!source.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        var withoutSlashes = source[2..];
        var slashIndex = withoutSlashes.IndexOf('/');
        if (slashIndex <= 0 || slashIndex == withoutSlashes.Length - 1)
        {
            return false;
        }

        var sourceHost = withoutSlashes[..slashIndex];
        var atIndex = sourceHost.LastIndexOf('@');
        if (atIndex >= 0)
        {
            sourceHost = sourceHost[(atIndex + 1)..];
        }

        var sourceShare = withoutSlashes[(slashIndex + 1)..];
        return string.Equals(sourceHost, host, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Uri.UnescapeDataString(sourceShare), share, StringComparison.OrdinalIgnoreCase);
    }

    private static string CombineMountPointAndRelativePath(string mountPoint, string relativePath)
    {
        return string.IsNullOrEmpty(relativePath)
            ? mountPoint
            : Path.Combine(mountPoint, relativePath);
    }

    private static async Task<string> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        if (process.ExitCode == 0)
        {
            return output.Trim();
        }

        var error = await errorTask;
        throw new IOException(error.Trim());
    }
}
