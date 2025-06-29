using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
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
    
    AbsolutePath PluginProjectPath => RootDirectory / "source" / "Maestro.Plugin" / "Maestro.Plugin.csproj";
    AbsolutePath OutputDirectory => IsLocalBuild
        ? GetDebugOutputPath()
        : RootDirectory / ".dist";
    AbsolutePath ZipPath => OutputDirectory / $"Maestro.{GitVersion.FullSemVer}.zip";
    AbsolutePath ChecksumPath => OutputDirectory / $"Maestro.{GitVersion.FullSemVer}.zip.sha256";
    
    [GitVersion]
    readonly GitVersion GitVersion;

    Target Clean => _ => _
        .Executes(() =>
        {
            Log.Information("Cleaning {OutputDirectory}", OutputDirectory);
            OutputDirectory.CreateOrCleanDirectory();
        });

    Target Compile => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            Log.Information(
                "Building version {Version} with configuration {Configuration} to {OutputDirectory}",
                GitVersion,
                Configuration,
                OutputDirectory);
            
            DotNetTasks.DotNetBuild(s => s
                .SetProjectFile(PluginProjectPath)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(OutputDirectory)
                .SetVersion(GitVersion.FullSemVer)
                .SetProperty("BuildMetadata", GitVersion.BuildMetaData));
        });

    Target Package => _ => _
        .DependsOn(Compile)
        .Requires(() => Configuration == Configuration.Release)
        .Executes(async () =>
        {
            var dpiAwareFixScript = RootDirectory / "dpiawarefix.bat";
            var readmeFile = RootDirectory / "README.md";
            var changelogFile = RootDirectory / "CHANGELOG.md";
            
            dpiAwareFixScript.CopyToDirectory(OutputDirectory, ExistsPolicy.FileOverwrite);
            readmeFile.CopyToDirectory(OutputDirectory, ExistsPolicy.FileOverwrite);
            changelogFile.CopyToDirectory(OutputDirectory, ExistsPolicy.FileOverwrite);
            
            Log.Information("Packaging {OutputDirectory} to {ZipPath}", OutputDirectory, ZipPath);
            OutputDirectory.ZipTo(ZipPath);
            
            var hash = ZipPath.GetFileHash();
            Log.Information("{ZipPath} checksum {Checksum}", ZipPath, hash);
            
            await File.WriteAllTextAsync(ChecksumPath, hash);
        });

    AbsolutePath GetDebugOutputPath()
    {
        const string key = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Sawbe\vatSys";
        const string value = "Path";
        var basePath = Registry.GetValue(key, value, null)?.ToString();
        return basePath == null ? null : Path.Combine(basePath, "bin", "Plugins", "MaestroPlugin - Debug");
    }
}
