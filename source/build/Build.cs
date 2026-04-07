using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.Versioning;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitHub;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.ILRepack;
using Octokit;
using Serilog;

[SupportedOSPlatform("Windows")]
[GitHubActions(
    "build",
    GitHubActionsImage.WindowsLatest,
    OnPushBranches = ["main"],
    OnPullRequestBranches = ["main"],
    InvokedTargets = [nameof(CompilePlugin), nameof(Test)],
    FetchDepth = 0)]
[GitHubActions(
    "release",
    GitHubActionsImage.WindowsLatest,
    OnPushTags = ["v*"],
    InvokedTargets = [nameof(Release)],
    ImportSecrets = [nameof(GitHubToken)],
    EnableGitHubToken = true,
    FetchDepth = 0,
    WritePermissions = [GitHubActionsPermissions.Contents])]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>();

    [Parameter("Configuration to build - Default is 'Debug' for local development, 'Release' will be used in CI")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("The name of the vatSys profile where the plugin should be (un)installed")]
    string ProfileName { get; }

    [Parameter("The GitHub token to use when creating releases")]
    [Secret]
    readonly string GitHubToken;

    [Parameter("Output directory for server build")]
    readonly string ServerOutputDirectory;

    [GitRepository]
    readonly GitRepository GitRepository;

    [GitVersion]
    readonly GitVersion GitVersion;

    const string ReleasePluginName = "MaestroPlugin";
    const string DebugPluginName = "MaestroPlugin - Debug";
    const string PluginAssemblyFileName = "Maestro.Plugin.dll";

    string PluginName => Configuration == Configuration.Debug ? DebugPluginName : ReleasePluginName;

    // Test projects
    AbsolutePath CoreTestsProjectPath => RootDirectory / "source" / "Maestro.Core.Tests" / "Maestro.Core.Tests.csproj";
    AbsolutePath PluginTestsProjectPath => RootDirectory / "source" / "Maestro.Plugin.Tests" / "Maestro.Plugin.Tests.csproj";
    AbsolutePath ContractTestsProjectPath => RootDirectory / "source" / "Maestro.Contracts.Tests" / "Maestro.Contracts.Tests.csproj";
    AbsolutePath ServerTestsProjectPath => RootDirectory / "source" / "Maestro.Server.Tests" / "Maestro.Server.Tests.csproj";

    // Plugin paths
    AbsolutePath PluginProjectPath => RootDirectory / "source" / "Maestro.Plugin" / "Maestro.Plugin.csproj";
    AbsolutePath PluginBuildOutputDirectory => TemporaryDirectory / "build-plugin";
    AbsolutePath PluginZipPath => TemporaryDirectory / $"Maestro.Plugin.{GetSemanticVersion()}.zip";
    AbsolutePath PluginPackageDirectory => TemporaryDirectory / "package-plugin";

    // Server paths
    AbsolutePath ServerProjectPath => RootDirectory / "source" / "Maestro.Server" / "Maestro.Server.csproj";
    AbsolutePath ServerBuildOutputDirectory => !string.IsNullOrEmpty(ServerOutputDirectory)
        ? (AbsolutePath)ServerOutputDirectory
        : TemporaryDirectory / "build-server";
    AbsolutePath ServerZipPath => TemporaryDirectory / $"Maestro.Server.{GetSemanticVersion()}.zip";
    AbsolutePath ServerPackageDirectory => TemporaryDirectory / "package-server";

    // Tools paths
    static readonly string[] ToolsRuntimeIdentifiers = ["win-x64", "linux-x64", "osx-arm64"];
    AbsolutePath ToolsProjectPath => RootDirectory / "source" / "Maestro.Tools" / "Maestro.Tools.csproj";
    AbsolutePath ToolsBuildOutputDirectory => TemporaryDirectory / "build-tools";
    AbsolutePath ToolsPackageDirectory(string rid) => TemporaryDirectory / "package-tools" / rid;
    AbsolutePath ToolsZipPath(string rid) => TemporaryDirectory / $"Maestro.Tools.{GetSemanticVersion()}.{rid}.zip";

    // vatSys paths
    [Parameter("Path to the vatSys installation")]
    AbsolutePath VatSysPath { get; }
    AbsolutePath VatSysSetupDirectory => TemporaryDirectory / "vatsys-setup";
    AbsolutePath VatSysExePath => VatSysPath ?? VatSysSetupDirectory / "bin" / "vatSys.exe";

    public Target CheckVersion => _ => _
        .Description("Prints the semantic version that will be assigned to the binaries.")
        .Unlisted()
        .Executes(() => Log.Information("Version: {Version}", GetSemanticVersion()));

    Target DownloadVatSys => _ => _
        .Description("Downloads and extracts the vatSys executable so the plugin can be built in a headless environment (i.e. a CI system)")
        .Unlisted()
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

    Target CompilePlugin => _ => _
        .Description("Compiles the plugin")
        .DependsOn(DownloadVatSys)
        .Executes(() =>
        {
            var version = GetSemanticVersion();
            Log.Information(
                "Building version {Version} with configuration {Configuration} to {OutputDirectory}",
                version,
                Configuration,
                PluginBuildOutputDirectory);

            DotNetTasks.DotNetBuild(s => s
                .SetProjectFile(PluginProjectPath)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(PluginBuildOutputDirectory)
                .SetVersion(version)
                .SetAssemblyVersion(GitVersion.MajorMinorPatch)
                .SetFileVersion(GitVersion.MajorMinorPatch)
                .SetInformationalVersion(version)
                .SetProperty("VatSysPath", VatSysExePath.Parent.Parent));
        });

    Target RepackPlugin => _ => _
        .Description("Combines all plugin dependencies into a single assembly using ILRepack. This helps avoid dependency conflicts with other vatSys plugins, and vatSys itself.")
        .DependsOn(CompilePlugin)
        .Executes(() =>
        {
            var mainAssembly = PluginBuildOutputDirectory / PluginAssemblyFileName;
            var assembliesToMerge = PluginBuildOutputDirectory
                .GlobFiles("*.dll")
                .Except([mainAssembly])
                .ToArray();

            if (!mainAssembly.FileExists())
                throw new Exception($"Main assembly not found: {mainAssembly}");

            foreach (var assembly in assembliesToMerge.Where(a => !a.FileExists()))
                Log.Warning("Assembly not found (will be skipped): {Assembly}", assembly);

            var existingAssemblies = assembliesToMerge.Where(a => a.FileExists()).ToArray();
            if (existingAssemblies.Length == 0)
            {
                Log.Information("No assemblies found to repack, skipping");
                return;
            }

            var settings = new ILRepackSettings()
                .SetAssemblies([mainAssembly.ToString(), ..existingAssemblies.Select(a => a.ToString())])
                .SetInternalize(false)
                .SetParallel(true)
                .SetOutput(mainAssembly.ToString())
                .SetLib(PluginBuildOutputDirectory.ToString());  // Tell ILRepack where to find referenced assemblies

            Log.Information("Repacking {Count} assemblies into {MainAssembly}", existingAssemblies.Length, mainAssembly);
            foreach (var assembly in existingAssemblies)
                Log.Information("  - {Assembly}", assembly.Name);

            ILRepackTasks.ILRepack(settings);

            // Clean up original merged DLLs
            foreach (var assembly in existingAssemblies)
            {
                assembly.DeleteFile();
                Log.Information("Deleted {Assembly}", assembly);
            }

            Log.Information("Repack complete");
        });

    Target TestContracts => _ => _
        .Executes(() =>
        {
            Log.Information("Running Contract tests");
            DotNetTasks.DotNetTest(s => s
                .SetProjectFile(ContractTestsProjectPath)
                .SetConfiguration(Configuration));
        });

    Target TestPlugin => _ => _
        .DependsOn(DownloadVatSys)
        .Executes(() =>
        {
            Log.Information("Running Plugin tests");
            DotNetTasks.DotNetTest(s => s
                .SetProjectFile(PluginTestsProjectPath)
                .SetConfiguration(Configuration)
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
        .DependsOn(TestContracts, TestCore, TestPlugin, TestServer);

    Target Uninstall => _ => _
        .Requires(() => ProfileName)
        .Description("Uninstalls the plugin from the specified profile.")
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

            var configFilePath = pluginsDirectory / "Configs" / "Maestro" / "Maestro.yaml";
            if (configFilePath.FileExists())
            {
                configFilePath.DeleteFile();
                Log.Information("Config file {ConfigFile} deleted", configFilePath);
            }
        });

    Target Install => _ => _
        .Description("Installs the plugin to the specified profile.")
        .Requires(() => ProfileName)
        .DependsOn(RepackPlugin)
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
            foreach (var absolutePath in PluginBuildOutputDirectory.GetFiles())
            {
                absolutePath.CopyToDirectory(maestroPluginDirectory, ExistsPolicy.MergeAndOverwrite);
            }

            // Copy config
            var configFile = RootDirectory / "Maestro.yaml";
            var configDestinationDirectory = pluginsDirectory / "Configs" / "Maestro";
            configDestinationDirectory.CreateOrCleanDirectory();

            configFile.CopyToDirectory(configDestinationDirectory, ExistsPolicy.MergeAndOverwrite);

            Log.Information("Plugin installed to {PluginsDirectory}", maestroPluginDirectory);
        });

    Target PackagePlugin => _ => _
        .Description("Bundles the plugin along with supplementary files into a zip archive.")
        .DependsOn(CompilePlugin)
        .DependsOn(RepackPlugin)
        .DependsOn(TestContracts, TestCore, TestPlugin)
        .Requires(() => Configuration == Configuration.Release)
        .Executes(() =>
        {
            var dpiAwareFixScript = RootDirectory / "dpiawarefix.bat";
            var unblockDllsScript = RootDirectory / "unblock-dlls.bat";
            var configFile = RootDirectory / "Maestro.yaml";

            PluginPackageDirectory.CreateOrCleanDirectory();

            // Copy plugin assemblies
            foreach (var absolutePath in PluginBuildOutputDirectory.GetFiles().Where(f => f.Extension != ".pdb"))
            {
                absolutePath.CopyToDirectory(PluginPackageDirectory, ExistsPolicy.MergeAndOverwrite);
            }

            // Temporary for testing - include the config with the package
            configFile.CopyToDirectory(PluginPackageDirectory, ExistsPolicy.FileOverwrite);

            dpiAwareFixScript.CopyToDirectory(PluginPackageDirectory, ExistsPolicy.FileOverwrite);
            unblockDllsScript.CopyToDirectory(PluginPackageDirectory, ExistsPolicy.FileOverwrite);

            if (PluginZipPath.FileExists())
                PluginZipPath.DeleteFile();

            Log.Information("Packaging {OutputDirectory} to {ZipPath}", PluginPackageDirectory, PluginZipPath);
            PluginPackageDirectory.ZipTo(PluginZipPath);
        });

    Target CompileServer => _ => _
        .Description("Compiles the server")
        .DependsOn(TestServer, TestContracts)
        .Executes(() =>
        {
            var version = GetSemanticVersion();
            Log.Information(
                "Publishing Maestro.Server version {Version} with configuration {Configuration}",
                version,
                Configuration);

            DotNetTasks.DotNetPublish(s => s
                .SetProject(ServerProjectPath)
                .SetConfiguration(Configuration)
                .SetOutput(ServerBuildOutputDirectory)
                .SetVersion(version)
                .SetAssemblyVersion(GitVersion.MajorMinorPatch)
                .SetFileVersion(GitVersion.MajorMinorPatch)
                .SetInformationalVersion(version));

            Log.Information("Server published to {OutputDirectory}", ServerBuildOutputDirectory);
        });

    Target PackageServer => _ => _
        .Description("Bundles the server into a zip archive.")
        .DependsOn(CompileServer)
        .Requires(() => Configuration == Configuration.Release)
        .Executes(() =>
        {
            ServerPackageDirectory.CreateOrCleanDirectory();

            // Copy all published server files
            foreach (var absolutePath in ServerBuildOutputDirectory.GetFiles().Where(f => f.Extension != ".pdb"))
            {
                absolutePath.CopyToDirectory(ServerPackageDirectory, ExistsPolicy.MergeAndOverwrite);
            }

            if (ServerZipPath.FileExists())
                ServerZipPath.DeleteFile();

            Log.Information("Packaging {OutputDirectory} to {ZipPath}", ServerPackageDirectory, ServerZipPath);
            ServerPackageDirectory.ZipTo(ServerZipPath);
        });

    Target CompileTools => _ => _
        .Description("Publishes the CLI tool as self-contained single-file executables for each supported platform.")
        .Executes(() =>
        {
            var version = GetSemanticVersion();
            foreach (var rid in ToolsRuntimeIdentifiers)
            {
                var outputDir = ToolsBuildOutputDirectory / rid;
                Log.Information("Publishing Maestro.Tools {Version} for {RID}", version, rid);

                DotNetTasks.DotNetPublish(s => s
                    .SetProject(ToolsProjectPath)
                    .SetConfiguration(Configuration)
                    .SetOutput(outputDir)
                    .SetRuntime(rid)
                    .SetSelfContained(true)
                    .SetVersion(version)
                    .SetAssemblyVersion(GitVersion.MajorMinorPatch)
                    .SetFileVersion(GitVersion.MajorMinorPatch)
                    .SetInformationalVersion(version)
                    .SetProperty("PublishSingleFile", "true")
                    .SetProperty("IncludeNativeLibrariesForSelfExtract", "true"));
            }
        });

    Target PackageTools => _ => _
        .Description("Bundles each platform build of the CLI tool into its own zip archive.")
        .DependsOn(CompileTools)
        .Requires(() => Configuration == Configuration.Release)
        .Executes(() =>
        {
            foreach (var rid in ToolsRuntimeIdentifiers)
            {
                var packageDir = ToolsPackageDirectory(rid);
                packageDir.CreateOrCleanDirectory();

                var files = (ToolsBuildOutputDirectory / rid)
                    .GetFiles()
                    .Where(f => f.Extension != ".pdb");

                foreach (var file in files)
                    file.CopyToDirectory(packageDir, ExistsPolicy.MergeAndOverwrite);

                var zipPath = ToolsZipPath(rid);
                if (zipPath.FileExists())
                    zipPath.DeleteFile();

                Log.Information("Packaging {OutputDirectory} to {ZipPath}", packageDir, zipPath);
                packageDir.ZipTo(zipPath);
            }
        });

    Target Release => _ => _
        .Description("Creates a GitHub release with the plugin and server binaries attached.")
        .DependsOn(PackagePlugin, PackageServer, PackageTools)
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

            // Upload the plugin zip as an asset
            using var pluginZipStream = File.OpenRead(PluginZipPath);
            var pluginAssetUpload = new ReleaseAssetUpload
            {
                FileName = PluginZipPath.Name,
                ContentType = "application/zip",
                RawData = pluginZipStream
            };

            var pluginAsset = await githubClient.Repository.Release.UploadAsset(release, pluginAssetUpload);
            Log.Information("Plugin asset uploaded: {AssetUrl}", pluginAsset.BrowserDownloadUrl);

            // Upload the server zip as an asset
            using var serverZipStream = File.OpenRead(ServerZipPath);
            var serverAssetUpload = new ReleaseAssetUpload
            {
                FileName = ServerZipPath.Name,
                ContentType = "application/zip",
                RawData = serverZipStream
            };

            var serverAsset = await githubClient.Repository.Release.UploadAsset(release, serverAssetUpload);
            Log.Information("Server asset uploaded: {AssetUrl}", serverAsset.BrowserDownloadUrl);

            // Upload a tools zip per platform
            foreach (var rid in ToolsRuntimeIdentifiers)
            {
                var toolsZipPath = ToolsZipPath(rid);
                using var toolsZipStream = File.OpenRead(toolsZipPath);
                var toolsAssetUpload = new ReleaseAssetUpload
                {
                    FileName = toolsZipPath.Name,
                    ContentType = "application/zip",
                    RawData = toolsZipStream
                };

                var toolsAsset = await githubClient.Repository.Release.UploadAsset(release, toolsAssetUpload);
                Log.Information("Tools asset uploaded ({RID}): {AssetUrl}", rid, toolsAsset.BrowserDownloadUrl);
            }
        });

    /// <summary>
    /// Publishes Docker image to GitHub Container Registry.
    /// TODO: Implement when ready to publish Docker images alongside releases.
    /// </summary>
    Target PublishDockerImage => _ => _
        .Description("Builds and uploads the Docker image for the server.")
        .Unlisted()
        .DependsOn(CompileServer)
        .Requires(() => Configuration == Configuration.Release)
        .Executes(() =>
        {
            // Future: docker build, docker tag, docker push to ghcr.io
            Log.Fatal("Docker image publishing not yet implemented");
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
        // For main/master branch, or if we're on a detached head (building from a tag)
        // use major.minor.patch (e.g., "1.2.3")
        if (string.IsNullOrEmpty(GitVersion.BranchName) || GitVersion.BranchName is "main" or "master")
        {
            return GitVersion.MajorMinorPatch;
        }

        // For other branches (develop, hotfix, etc.): use SemVer format
        return GitVersion.SemVer;
    }
}
