using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Maestro.Plugin;

public static class GitHubReleaseChecker
{
    const string GitHubApiUrl = "https://api.github.com/repos/YuKitsune/vMaestro/releases/latest";
    const string UserAgent = "vMaestro-Plugin";

    public static async Task CheckForUpdatesAsync(string currentVersion, ILogger logger)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, currentVersion));
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var response = await httpClient.GetStringAsync(GitHubApiUrl);
            var release = JObject.Parse(response);

            var tagName = release["tag_name"]?.ToString();
            if (string.IsNullOrEmpty(tagName))
            {
                logger.Warning("GitHub release response did not contain a tag_name");
                return;
            }

            // Remove 'v' prefix if present
            var latestVersion = tagName.TrimStart('v');

            if (!IsNewerVersion(latestVersion, currentVersion))
            {
                logger.Information("Plugin is up to date (current: {CurrentVersion}, latest: {LatestVersion})", currentVersion, latestVersion);
                return;
            }

            var message = $"A new version of Maestro is available: {latestVersion} (current: {currentVersion}).";

            logger.Warning(message);
            Errors.Add(new Exception(message), Plugin.Name);
        }
        catch (HttpRequestException ex)
        {
            logger.Warning(ex, "Failed to check for updates: network error");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            logger.Warning("Failed to check for updates: request timed out");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to check for updates");
        }
    }

    static bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        var latest = ParseVersion(latestVersion);
        var current = ParseVersion(currentVersion);

        // Compare major, minor, patch in order
        if (latest.Major != current.Major)
            return latest.Major > current.Major;

        if (latest.Minor != current.Minor)
            return latest.Minor > current.Minor;

        if (latest.Patch != current.Patch)
            return latest.Patch > current.Patch;

        // If versions are equal but one is pre-release, stable is newer
        if (current.PreRelease is not null && latest.PreRelease is null)
            return true;

        // If both are pre-release and different, consider latest as newer
        if (current.PreRelease is not null && latest.PreRelease is not null && current.PreRelease != latest.PreRelease)
            return true;

        return false;
    }

    static (int Major, int Minor, int Patch, string? PreRelease) ParseVersion(string version)
    {
        // Strip build metadata (e.g., "+abc123")
        var metadataIndex = version.IndexOf('+');
        if (metadataIndex >= 0)
            version = version.Substring(0, metadataIndex);

        // Extract pre-release label (e.g., "-beta.1")
        string? preRelease = null;
        var preReleaseIndex = version.IndexOf('-');
        if (preReleaseIndex >= 0)
        {
            preRelease = version.Substring(preReleaseIndex + 1);
            version = version.Substring(0, preReleaseIndex);
        }

        var parts = version.Split('.');

        var major = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : 0;
        var minor = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
        var patch = parts.Length > 2 && int.TryParse(parts[2], out var p) ? p : 0;

        return (major, minor, patch, preRelease);
    }
}
