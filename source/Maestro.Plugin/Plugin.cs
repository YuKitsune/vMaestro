using System.ComponentModel.Composition;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.DependencyInjection;
using Maestro.Core;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Hosting;
using Maestro.Core.Hosting.Contracts;
using Maestro.Core.Integration;
using Maestro.Core.Model;
using Maestro.Plugin.Configuration;
using Maestro.Plugin.Handlers;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf;
using Maestro.Wpf.Integrations;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;
using vatsys;
using vatsys.Plugin;
using Coordinate = Maestro.Core.Model.Coordinate;

namespace Maestro.Plugin;

[Export(typeof(IPlugin))]
public class Plugin : IPlugin
{
#if DEBUG
    public const string Name = "Maestro - Debug";
#else
    public const string Name = "Maestro";
#endif

    string IPlugin.Name => Name;

    readonly IMediator? _mediator;
    readonly IMaestroInstanceManager? _instanceManager;
    readonly ILogger? _logger;

    public Plugin()
    {
        try
        {
            EnsureDpiAwareness();

            var configuration = ConfigureConfiguration();

            ConfigureServices(configuration);
            ConfigureTheme();
            AddToolbarItems(configuration);

            _mediator = Ioc.Default.GetRequiredService<IMediator>();
            _instanceManager = Ioc.Default.GetRequiredService<IMaestroInstanceManager>();
            _logger = Ioc.Default.GetRequiredService<ILogger>();

            Network.Connected += NetworkOnConnected;
            Network.Disconnected += NetworkOnDisconnected;

            var executingAssembly = Assembly.GetExecutingAssembly();
            var version = executingAssembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? string.Empty;
#if RELEASE
            version = version.Split('+').First();
#endif
            _logger.Information("{PluginName} {Version} initialized.", Name, version);

#if RELEASE
            // Check for updates in background (fire-and-forget)
            var pluginConfiguration = Ioc.Default.GetRequiredService<PluginConfiguration>();
            if (pluginConfiguration.CheckForUpdates)
            {
                _ = GitHubReleaseChecker.CheckForUpdatesAsync(version, _logger);
            }
#endif
        }
        catch (Exception ex)
        {
            Errors.Add(ex, Name);
            _logger?.Error(ex, "An error occurred during initialization.");
        }
    }

    void ConfigureTheme()
    {
        Theme.BackgroundColor = new SolidColorBrush(Colours.GetColour(Colours.Identities.WindowBackground).ToWindowsColor());
        Theme.GenericTextColor = new SolidColorBrush(Colours.GetColour(Colours.Identities.GenericText).ToWindowsColor());
        Theme.InteractiveTextColor = new SolidColorBrush(Colours.GetColour(Colours.Identities.InteractiveText).ToWindowsColor());
        Theme.NonInteractiveTextColor = new SolidColorBrush(Colours.GetColour(Colours.Identities.NonInteractiveText).ToWindowsColor());
        Theme.SelectedButtonColor = new SolidColorBrush(Colours.GetColour(Colours.Identities.WindowButtonSelected).ToWindowsColor());
        Theme.FontFamily = new FontFamily(MMI.eurofont_xsml.FontFamily.Name);
        Theme.FontSize = MMI.eurofont_xsml.Size;
        Theme.FontWeight = MMI.eurofont_xsml.Bold ? FontWeights.Bold : FontWeights.Regular;
    }

    void ConfigureServices(PluginConfiguration pluginConfiguration)
    {
        var logger = ConfigureLogger(pluginConfiguration.Logging);

        Ioc.Default.ConfigureServices(
            new ServiceCollection()
                .AddConfiguration(pluginConfiguration)
                .AddSingleton(pluginConfiguration.Server)
                .AddViewModels()
                .AddMaestro()
                .AddMediatR(c =>
                {
                    c.NotificationPublisher = new AsyncNotificationPublisher(logger);

                    c.RegisterServicesFromAssemblies(
                        typeof(Core.AssemblyMarker).Assembly,
                        typeof(AssemblyMarker).Assembly,
                        typeof(Wpf.AssemblyMarker).Assembly
                    );
                })
                .AddSingleton<IFixLookup, VatsysFixLookup>()
                .AddSingleton<IPerformanceLookup, VatsysPerformanceDataLookup>()
                .AddSingleton(new GuiInvoker(MMI.InvokeOnGUI))
                .AddSingleton(logger)
                .AddSingleton<IErrorReporter>(new ErrorReporter(Name, logger))
                .AddSingleton<WindowManager>()
                .BuildServiceProvider());
    }

    PluginConfiguration ConfigureConfiguration()
    {
        const string configFileName = "Maestro.json";

        var searchDirectories = new List<string>();

        // Search the profile first
        if (TryFindProfileDirectory(out var profileDirectory))
        {
            searchDirectories.AddRange([
                Path.Combine(profileDirectory.FullName, "Plugins", "Configs", "Maestro"),
                Path.Combine(profileDirectory.FullName, "Plugins", "Configs"),
                Path.Combine(profileDirectory.FullName, "Plugins"),
                profileDirectory.FullName
            ]);
        }

        // Search the assembly directory last
        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        searchDirectories.Add(assemblyDirectory);

        var configFilePath = string.Empty;
        foreach (var searchDirectory in searchDirectories)
        {
            var filePath = Path.Combine(searchDirectory, configFileName);
            if (!File.Exists(filePath))
                continue;

            configFilePath = filePath;
            break;
        }

        if (string.IsNullOrEmpty(configFilePath))
            throw new MaestroException($"Unable to locate {configFileName}");

        var configurationJson = File.ReadAllText(configFilePath);
        var configuration = JsonConvert.DeserializeObject<PluginConfiguration>(configurationJson)!;

        return configuration;
    }

    ILogger ConfigureLogger(ILoggingConfiguration loggingConfiguration)
    {
        var logFileName = Path.Combine(Helpers.GetFilesFolder(), "maestro_log.txt");

        // TODO: Include the airport in the log output
        var logger = new LoggerConfiguration()
            .WriteTo.File(
                path: logFileName,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: loggingConfiguration.MaxFileAgeDays,
                outputTemplate: "{Timestamp:u} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .MinimumLevel.Is(loggingConfiguration.LogLevel)
            .CreateLogger();

        Log.Logger = logger;

        return logger;
    }

    void AddToolbarItems(PluginConfiguration pluginConfiguration)
    {
        const string MenuItemCategory = "TFMS";

        foreach (var airportConfiguration in pluginConfiguration.Airports)
        {
            var menuItem = new CustomToolStripMenuItem(
                CustomToolStripMenuItemWindowType.Main,
                MenuItemCategory,
                new ToolStripMenuItem(airportConfiguration.Identifier));

            menuItem.Item.Click += (_, _) => OpenOrFocusWindowFor(airportConfiguration.Identifier);

            MMI.AddCustomMenuItem(menuItem);
        }
    }

    void OpenOrFocusWindowFor(string airportIdentifier)
    {
        var windowManager = Ioc.Default.GetRequiredService<WindowManager>();
        if (windowManager.TryGetWindow(WindowKeys.Maestro(airportIdentifier), out var windowHandle))
        {
            windowHandle!.Focus();

            var guiInvoker = Ioc.Default.GetRequiredService<GuiInvoker>();
            guiInvoker.InvokeOnUiThread(_ =>
            {
            });
        }
        else
        {
            _mediator.Send(new CreateMaestroInstanceRequest(airportIdentifier), CancellationToken.None);
        }
    }

    void NetworkOnConnected(object sender, EventArgs e)
    {
        try
        {
            _mediator.Publish(new NetworkConnectedNotification(Network.Callsign), CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "An error occurred while handling Network.Connected.");
            Errors.Add(ex, Name);
        }
    }

    void NetworkOnDisconnected(object sender, EventArgs e)
    {
        try
        {
            _mediator.Publish(new NetworkDisconnectedNotification(), CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "An error occurred while handling Network.Disconnected.");
            Errors.Add(ex, Name);
        }
    }

    public void OnFDRUpdate(FDP2.FDR updated)
    {
        try
        {
            PublishFlightUpdatedEvent(updated);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Failed to handle OnFDRUpdate for {Callsign}.", updated.Callsign);
        }
    }

    public void OnRadarTrackUpdate(RDP.RadarTrack updated)
    {
        try
        {
            if (updated.CoupledFDR is null)
                return;

            // We publish the FlightUpdatedEvent here because OnFDRUpdate and Network_Connected aren't guaranteed to
            // fire for all flights when initially connecting to the network.
            // The handler for this event will publish the FlightPositionReport if a FlightPosition is available.
            PublishFlightUpdatedEvent(updated.CoupledFDR);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Failed to handle OnRadarTrackUpdate for {Callsign}.", updated.CoupledFDR?.Callsign);
        }
    }

    void PublishFlightUpdatedEvent(FDP2.FDR updated)
    {
        // BUG: FDR updates can be sent before the instance manager has been created, in which case we miss updates
        //  When an instance is created, scan the active FDRs to ensure it's populated.
        if (_instanceManager is null || !_instanceManager.InstanceExists(updated.DesAirport))
            return;

        var isActivated = updated.State > FDP2.FDR.FDRStates.STATE_PREACTIVE;
        if (!isActivated)
            return;

        // Estimates have not been calculated yet
        if (!updated.ESTed)
            return;

        var estimates = updated.ParsedRoute
            .ToArray() // Materialize to avoid mutation during enumeration
            .Select((s, i) => new FixEstimate(
                s.Intersection.Name,
                ToDateTimeOffset(s.ETO),
                i <= updated.ParsedRoute.OverflownIndex // If this fix has been overflown, then use the ATO
                    ? ToDateTimeOffset(s.ATO) // BUG: If a flight has passed FF before we connect to the network, this will be MaxValue. ATO is unknown.
                    : null))
            .ToArray();

        FlightPosition? position = null;
        if (updated.CoupledTrack is not null)
        {
            var track = updated.CoupledTrack;
            var verticalTrack = track.VerticalSpeed >= RDP.VS_CLIMB
                ? VerticalTrack.Climbing
                : track.VerticalSpeed <= RDP.VS_DESCENT
                    ? VerticalTrack.Descending
                    : VerticalTrack.Maintaining;

            position = new FlightPosition(
                new Coordinate(track.LatLong.Latitude, track.LatLong.Longitude),
                track.CorrectedAltitude,
                verticalTrack,
                track.GroundSpeed,
                track.OnGround);
        }

        // PerformanceData can be null
        var aircraftCategory = updated.PerformanceData is null || updated.PerformanceData.IsJet
            ? AircraftCategory.Jet
            : AircraftCategory.NonJet;

        var wake = updated.AircraftWake switch
        {
            "J" => WakeCategory.SuperHeavy,
            "H" => WakeCategory.Heavy,
            "M" => WakeCategory.Medium,
            "L" => WakeCategory.Light,
            _ => WakeCategory.Heavy
        };

        var notification = new FlightUpdatedNotification(
            updated.Callsign,
            updated.AircraftType,
            aircraftCategory,
            wake,
            updated.DepAirport,
            updated.DesAirport,
            ToDateTimeOffset(updated.ETD),
            updated.EET,
            position,
            estimates);

        _mediator?.Publish(notification, CancellationToken.None);
    }

    internal static DateTimeOffset ToDateTimeOffset(DateTime dateTime)
    {
        if (dateTime == DateTime.MaxValue)
            return DateTimeOffset.MaxValue;

        return new DateTimeOffset(
            dateTime.Year, dateTime.Month, dateTime.Day,
            dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond,
            TimeSpan.Zero);
    }

    void EnsureDpiAwareness()
    {
        try
        {
            if (!TryGetVatSysExecutablePath(out var vatSysExecutablePath))
                return;

            const string registryPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
            const string dpiValue = "DPIUNAWARE";

            using var key = Registry.CurrentUser.OpenSubKey(registryPath, writable: false);
            var existingValue = key?.GetValue(vatSysExecutablePath) as string;

            // If already set, exit early
            if (existingValue != null && existingValue.Contains(dpiValue))
                return;

            // Set the registry key
            using var writableKey = Registry.CurrentUser.OpenSubKey(registryPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(registryPath);

            writableKey.SetValue(vatSysExecutablePath, dpiValue, RegistryValueKind.String);

            // Restart vatSys to apply the DPI setting
            RestartVatSys();
        }
        catch (Exception ex)
        {
            Errors.Add(ex, Name);
        }
    }

    void RestartVatSys()
    {
        try
        {
            if (!TryGetVatSysExecutablePath(out var vatSysExecutablePath))
                return;

            System.Diagnostics.Process.Start(vatSysExecutablePath);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Errors.Add(ex, Name);
        }
    }

    bool TryGetVatSysInstallationPath(out string? installationPath)
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Sawbe\vatSys");
        installationPath = key?.GetValue("Path") as string;
        return !string.IsNullOrEmpty(installationPath) && Directory.Exists(installationPath);
    }

    bool TryGetVatSysExecutablePath(out string? executablePath)
    {
        try
        {
            if (!TryGetVatSysInstallationPath(out var installationPath))
            {
                executablePath = null;
                return false;
            }

            executablePath = Path.Combine(installationPath, "bin", "vatSys.exe");
            return File.Exists(executablePath);
        }
        catch
        {
            executablePath = null;
            return false;
        }
    }

    // Thanks Max!
    bool TryFindProfileDirectory(out DirectoryInfo? directoryInfo)
    {
        directoryInfo = null;
        if (!Profile.Loaded)
            return false;

        var shortNameObject = typeof(Profile).GetField("shortName", BindingFlags.Static | BindingFlags.NonPublic);
        var shortName = (string)shortNameObject.GetValue(shortNameObject);

        directoryInfo = new DirectoryInfo(Path.Combine(Helpers.GetFilesFolder(), "Profiles", shortName));
        return true;
    }
}
