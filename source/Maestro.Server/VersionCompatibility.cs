namespace Maestro.Server;

public static class VersionCompatibility
{
    public static bool IsCompatible(string clientVersion, string serverVersion)
    {
        var clientParts = ParseVersion(clientVersion);
        var serverParts = ParseVersion(serverVersion);

        // If either version is a pre-release, require exact match
        if (clientParts.PreRelease is not null || serverParts.PreRelease is not null)
        {
            return clientParts.Major == serverParts.Major
                && clientParts.Minor == serverParts.Minor
                && clientParts.Patch == serverParts.Patch
                && clientParts.PreRelease == serverParts.PreRelease;
        }

        // For stable releases, major and minor versions must match
        return clientParts.Major == serverParts.Major && clientParts.Minor == serverParts.Minor;
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
