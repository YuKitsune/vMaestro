using System.Reflection;

namespace Maestro.Plugin;

public static class VersionInfo
{
    private static readonly Assembly ExecutingAssembly = Assembly.GetExecutingAssembly();

    /// <summary>
    /// Gets the full version string including pre-release information (e.g., "1.2.3" or "1.2.3-feature-name")
    /// </summary>
    public static string FullVersion =>
        ExecutingAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Unknown";

    /// <summary>
    /// Gets the base version without pre-release information (e.g., "1.2.3")
    /// </summary>
    public static string BaseVersion =>
        ExecutingAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "Unknown";

    /// <summary>
    /// Gets the pre-release part of the version (e.g., "feature-name") or null if this is a release build
    /// </summary>
    public static string? PreReleaseTag
    {
        get
        {
            var fullVersion = FullVersion;
            var baseVersion = BaseVersion;

            if (fullVersion.Length > baseVersion.Length && fullVersion.StartsWith(baseVersion))
            {
                var preRelease = fullVersion.Substring(baseVersion.Length);
                return preRelease.StartsWith("-") ? preRelease.Substring(1) : preRelease;
            }

            return null;
        }
    }

    /// <summary>
    /// Gets whether this is a pre-release build
    /// </summary>
    public static bool IsPreRelease => !string.IsNullOrEmpty(PreReleaseTag);

    /// <summary>
    /// Gets a display-friendly version string for UI purposes
    /// </summary>
    public static string DisplayVersion => IsPreRelease ? $"{BaseVersion} ({PreReleaseTag})" : BaseVersion;
}
