using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Octokit;
using Serilog;

[SupportedOSPlatform("Windows")]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [GitVersion]
    readonly GitVersion GitVersion;

    const string ReleasePluginName = "MaestroPlugin";
    const string DebugPluginName = "MaestroPlugin - Debug";
    string PluginName => Configuration == Configuration.Debug ? DebugPluginName : ReleasePluginName;

    AbsolutePath PluginProjectPath => RootDirectory / "source" / "Maestro.Plugin" / "Maestro.Plugin.csproj";
    AbsolutePath BuildOutputDirectory => TemporaryDirectory / "build" / PluginName;
    AbsolutePath ZipPath => TemporaryDirectory / $"Maestro.{GetSemanticVersion()}.zip";
    AbsolutePath PackageDirectory => TemporaryDirectory / "package";

    [Parameter]
    string ProfileName { get; }

    Target Compile => _ => _
        .Executes(() =>
        {
            var version = GetSemanticVersion();

            Log.Information(
                "Building version {Version} with configuration {Configuration} to {OutputDirectory}",
                version,
                Configuration,
                BuildOutputDirectory);

            DotNetTasks.DotNetBuild(s => s
                .SetProjectFile(PluginProjectPath)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(BuildOutputDirectory)
                .SetVersion(version)
                .SetAssemblyVersion(GitVersion.MajorMinorPatch)
                .SetFileVersion(GitVersion.MajorMinorPatch)
                .SetInformationalVersion(version));
        });

    Target Uninstall => _ => _
        .Requires(() => ProfileName)
        .Executes(() =>
        {
            var pluginsDirectory = GetVatSysPluginsDirectory(ProfileName);
            AbsolutePath[] pluginDirectories =
            [
                pluginsDirectory / DebugPluginName,
                pluginsDirectory / ReleasePluginName
            ];

            foreach (var pluginDirectory in pluginDirectories)
            {
                pluginDirectory.DeleteDirectory();
                Log.Information("Plugin uninstalled from {Directory}", pluginDirectory);
            }
        });

    Target Install => _ => _
        .Requires(() => ProfileName)
        .DependsOn(Compile)
        .DependsOn(Uninstall)
        .Executes(() =>
        {
            var pluginsDirectory = GetVatSysPluginsDirectory(ProfileName);
            Log.Information("Installing plugin to {TargetDirectory}", pluginsDirectory);

            pluginsDirectory.CreateOrCleanDirectory();
            BuildOutputDirectory.CopyToDirectory(pluginsDirectory, ExistsPolicy.MergeAndOverwrite);
            Log.Information("Plugin installed to {PluginsDirectory}", pluginsDirectory);

            var configFile = RootDirectory / "Maestro.json";
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var profileDirectory = Path.Combine(documentsPath, "vatSys Files", "Profiles", ProfileName);
            configFile.CopyToDirectory(profileDirectory, ExistsPolicy.FileOverwrite);
            Log.Information("Maestro.json copied to to {ProfileDirectory}", profileDirectory);
        });

    Target Package => _ => _
        .DependsOn(Compile)
        .Requires(() => Configuration == Configuration.Release)
        .Executes(() =>
        {
            var dpiAwareFixScript = RootDirectory / "dpiawarefix.bat";
            var readmeFile = RootDirectory / "README.md";
            var configFile = RootDirectory / "Maestro.json";
            // var changelogFile = RootDirectory / "CHANGELOG.md";

            PackageDirectory.CreateOrCleanDirectory();
            BuildOutputDirectory.CopyToDirectory(PackageDirectory, ExistsPolicy.FileOverwrite);
            dpiAwareFixScript.CopyToDirectory(PackageDirectory, ExistsPolicy.FileOverwrite);
            readmeFile.CopyToDirectory(PackageDirectory, ExistsPolicy.FileOverwrite);
            configFile.CopyToDirectory(PackageDirectory, ExistsPolicy.FileOverwrite);
            // changelogFile.CopyToDirectory(PackageDirectory, ExistsPolicy.FileOverwrite);

            Log.Information("Packaging {OutputDirectory} to {ZipPath}", PackageDirectory, ZipPath);
            PackageDirectory.ZipTo(ZipPath);
        });

    static AbsolutePath GetVatSysPluginsDirectory(string profileName)
    {
        return GetVatSysProfilePath(profileName) / "Plugins";
    }

    static AbsolutePath GetVatSysProfilePath(string profileName)
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, "vatSys Files", "Profiles", profileName);
    }

    private string GetSemanticVersion()
    {
        // For main/master branch: use major.minor.patch (e.g., "1.2.3")
        if (GitVersion.BranchName is "main" or "master")
        {
            return GitVersion.MajorMinorPatch;
        }

        // For feature branches: use major.minor.patch-feature-name (e.g., "1.2.3-feature-name")
        if (GitVersion.BranchName.StartsWith("feature/") || GitVersion.BranchName.StartsWith("features/"))
        {
            var featureName = GitVersion.BranchName
                .Replace("feature/", "")
                .Replace("features/", "")
                .Replace("/", "-")
                .Replace("_", "-");
            return $"{GitVersion.MajorMinorPatch}-{featureName}";
        }

        // For other branches (develop, hotfix, etc.): use SemVer format
        return GitVersion.SemVer;
    }
}
