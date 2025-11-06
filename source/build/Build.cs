using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.Versioning;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitHub;
using Nuke.Common.Tools.GitVersion;
using Octokit;
using Serilog;

[SupportedOSPlatform("Windows")]
[GitHubActions(
    "build",
    GitHubActionsImage.WindowsLatest,
    OnPushBranches = ["main"],
    OnPullRequestBranches = ["main"],
    InvokedTargets = [nameof(Compile), nameof(Test)],
    FetchDepth = 0)]
[GitHubActions(
    "release",
    GitHubActionsImage.WindowsLatest,
    OnPushTags = ["v*"],
    InvokedTargets = [nameof(Release)],
    ImportSecrets = [nameof(GitHubToken)],
    EnableGitHubToken = true,
    FetchDepth = 0)]
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

    [Parameter("Merges all dependencies into a single assembly", Name = "repack")]
    bool ShouldRepack = false;

    [Parameter("GitHub token for creating releases")]
    [Secret]
    readonly string GitHubToken;

    [GitRepository]
    readonly GitRepository GitRepository;

    [GitVersion]
    readonly GitVersion GitVersion;

    const string ReleasePluginName = "MaestroPlugin";
    const string DebugPluginName = "MaestroPlugin - Debug";
    const string PluginAssemblyFileName = "Maestro.Plugin.dll";

    string PluginName => Configuration == Configuration.Debug ? DebugPluginName : ReleasePluginName;

    AbsolutePath PluginProjectPath => RootDirectory / "source" / "Maestro.Plugin" / "Maestro.Plugin.csproj";
    AbsolutePath CoreTestsProjectPath => RootDirectory / "source" / "Maestro.Core.Tests" / "Maestro.Core.Tests.csproj";
    AbsolutePath ServerTestsProjectPath => RootDirectory / "source" / "Maestro.Server.Tests" / "Maestro.Server.Tests.csproj";
    AbsolutePath BuildOutputDirectory => TemporaryDirectory / "build";
    AbsolutePath RepackDirectory => TemporaryDirectory / "repack";
    AbsolutePath PluginAssembliesPath => ShouldRepack ? RepackDirectory : BuildOutputDirectory;
    AbsolutePath ZipPath => TemporaryDirectory / $"Maestro.{GetSemanticVersion()}.zip";
    AbsolutePath PackageDirectory => TemporaryDirectory / "package";

    [Parameter]
    string ProfileName { get; }

    [Parameter("Path to vatSys installation")]
    AbsolutePath VatSysPath { get; }

    AbsolutePath VatSysSetupDirectory => TemporaryDirectory / "vatsys-setup";
    AbsolutePath VatSysExePath => VatSysPath ?? VatSysSetupDirectory / "bin" / "vatSys.exe";

    Target DownloadVatSys => _ => _
        .OnlyWhenStatic(() => VatSysPath == null && !VatSysExePath.FileExists())
        .Executes(async () =>
        {
            var vatSysSetupUrl = "https://vatsys.sawbe.com/downloads/vatSysSetup.zip";
            var zipPath = TemporaryDirectory / "vatSysSetup.zip";
            var msiPath = TemporaryDirectory / "vatSysSetup.msi";

            Log.Information("Downloading vatSys from {Url}", vatSysSetupUrl);
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(vatSysSetupUrl);
            response.EnsureSuccessStatusCode();
            await using var fileStream = File.Create(zipPath);
            await response.Content.CopyToAsync(fileStream);
            fileStream.Close();

            Log.Information("Extracting vatSysSetup.zip");
            ZipFile.ExtractToDirectory(zipPath, TemporaryDirectory, overwriteFiles: true);

            Log.Information("Extracting vatSysSetup.msi");
            VatSysSetupDirectory.CreateOrCleanDirectory();

            // Use msiexec to extract the MSI contents
            var msiExtractProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "msiexec",
                Arguments = $"/a \"{msiPath}\" /qn TARGETDIR=\"{VatSysSetupDirectory}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (msiExtractProcess != null)
            {
                await msiExtractProcess.WaitForExitAsync();
                if (msiExtractProcess.ExitCode != 0)
                {
                    var error = await msiExtractProcess.StandardError.ReadToEndAsync();
                    throw new Exception($"Failed to extract MSI: {error}");
                }
            }

            if (!VatSysExePath.FileExists())
                throw new Exception($"vatSys.exe not found at {VatSysExePath}");

            Log.Information("vatSys.exe extracted to {Path}", VatSysExePath);
        });

    Target Compile => _ => _
        .DependsOn(DownloadVatSys)
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
                .SetInformationalVersion(version)
                .SetProperty("VatSysPath", VatSysExePath.Parent.Parent));
        });

    Target TestCore => _ => _
        .Executes(() =>
        {
            Log.Information("Running Core tests");
            DotNetTasks.DotNetTest(s => s
                .SetProjectFile(CoreTestsProjectPath)
                .SetConfiguration(Configuration));
        });

    Target TestServer => _ => _
        .Executes(() =>
        {
            Log.Information("Running Server tests");
            DotNetTasks.DotNetTest(s => s
                .SetProjectFile(ServerTestsProjectPath)
                .SetConfiguration(Configuration));
        });

    Target Test => _ => _
        .DependsOn(TestCore)
        .DependsOn(TestServer);

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

            if (!pluginsDirectory.Exists())
                pluginsDirectory.CreateDirectory();

            // Copy plugin assemblies
            var maestroPluginDirectory = pluginsDirectory / PluginName;
            maestroPluginDirectory.CreateOrCleanDirectory();
            foreach (var absolutePath in PluginAssembliesPath.GetFiles())
            {
                absolutePath.CopyToDirectory(maestroPluginDirectory, ExistsPolicy.MergeAndOverwrite);
            }

            Log.Information("Plugin installed to {PluginsDirectory}", maestroPluginDirectory);

            // Copy config to profile
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
            var unblockDllsScript = RootDirectory / "unblock-dlls.bat";
            var readmeFile = RootDirectory / "README.md";
            var configFile = RootDirectory / "Maestro.json";

            PackageDirectory.CreateOrCleanDirectory();

            // Copy plugin assemblies
            var pluginAssembliesDirectory = PackageDirectory / PluginName;
            pluginAssembliesDirectory.CreateOrCleanDirectory();
            foreach (var absolutePath in PluginAssembliesPath.GetFiles())
            {
                absolutePath.CopyToDirectory(pluginAssembliesDirectory, ExistsPolicy.MergeAndOverwrite);
            }

            dpiAwareFixScript.CopyToDirectory(PackageDirectory, ExistsPolicy.FileOverwrite);
            unblockDllsScript.CopyToDirectory(PackageDirectory, ExistsPolicy.FileOverwrite);
            readmeFile.CopyToDirectory(PackageDirectory, ExistsPolicy.FileOverwrite);
            configFile.CopyToDirectory(PackageDirectory, ExistsPolicy.FileOverwrite);

            if (ZipPath.FileExists())
                ZipPath.DeleteFile();

            Log.Information("Packaging {OutputDirectory} to {ZipPath}", PackageDirectory, ZipPath);
            PackageDirectory.ZipTo(ZipPath);
        });

    Target Release => _ => _
        .DependsOn(Package)
        .Requires(() => GitHubToken)
        .Requires(() => GitRepository)
        .Requires(() => Configuration == Configuration.Release)
        .Executes(async () =>
        {
            var version = GetSemanticVersion();
            var tagName = $"v{version}";

            Log.Information("Creating GitHub release {TagName}", tagName);

            var credentials = new Credentials(GitHubToken);
            var githubClient = new GitHubClient(new ProductHeaderValue("nuke-build"))
            {
                Credentials = credentials
            };

            var repositoryOwner = GitRepository.GetGitHubOwner();
            var repositoryName = GitRepository.GetGitHubName();

            var newRelease = new NewRelease(tagName)
            {
                Name = version,
                Draft = false,
                Prerelease = false,
                GenerateReleaseNotes = true
            };

            var release = await githubClient.Repository.Release.Create(repositoryOwner, repositoryName, newRelease);
            Log.Information("Release created: {ReleaseUrl}", release.HtmlUrl);

            // Upload the zip file as an asset
            using var zipStream = File.OpenRead(ZipPath);
            var assetUpload = new ReleaseAssetUpload
            {
                FileName = ZipPath.Name,
                ContentType = "application/zip",
                RawData = zipStream
            };

            var asset = await githubClient.Repository.Release.UploadAsset(release, assetUpload);
            Log.Information("Asset uploaded: {AssetUrl}", asset.BrowserDownloadUrl);
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
