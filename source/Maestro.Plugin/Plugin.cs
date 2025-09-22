using System.ComponentModel.Composition;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Xml.Linq;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.DependencyInjection;
using Maestro.Core;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Core.Integration;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using Maestro.Plugin.Configuration;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;
using vatsys;
using vatsys.Plugin;
using Coordinate = Maestro.Core.Model.Coordinate;
using MessageBox = System.Windows.Forms.MessageBox;

namespace Maestro.Plugin;

// TODO: Need to improve the boundaries between DTOs, domain models, and view models.
//  - Domain models and DTOs are leaking into the frontend

// TODO: Encapsulate UI related commands into a separate service rather than using the Mediator

// TODO: Setup View
//  - Show setup after opening TFMS window via Setup button
//  - Allow connection to a session after TFMS window is open

// TODO: Smaller messages
//  - Remove SequenceUpdatedNotification, send smaller messages as needed

// TODO: Clean up DTOs
//  - Rename them to DTOs
//  - Move into a Contracts project

// TODO: Information messages
//  - Show information messages for important events (e.g. pending flight activated, desequenced, runway mode changes etc.)

// TODO: Flight insertion
//  - Add options to Pending DTO (i.e. departure time or arrival time)
//  - Allow insertion on the feeder view
//  - Different handlers for pending, overshoot, and dummy flights

// TODO: Windowing
//  - WPF's "*" size is incompatible with WinForms AutoSizing.

// Bugs from Sunday:
//  - Current runway mode is not pulled in when connecting
//  - Changing runways sometimes doesn't deconflict

// Bugs from MRM:
// - Windows don't resize when the content changes.
// - Recompute behavior is unpredictable. Need to revisit.
// - Flights sometimes get a negative total delay, even after stabling.
// - Insert flight window doesn't work.
// - + symbol appears when no delay is remaining. Is this intended?
// - System estimates are very inaccurate, they rely on TAS input by the pilot. Hybrid BRL + system estimate is confusing. (Pick one?)
// - Connection failures are not gracefully handled.

// AIS Notes:
// - When a flight takes off from a non-departure airport, they jump in front of the sequence despite being really far away
//    - Need to model close airports as well as departure airports I think
// - YMML PORTS arrival not modeled correctly
// - YMML needs different state thresholds

// SOPS Notes:
// - Document troubleshooting routes i.e. TANTA LEECE TANTA making the estimates all wrong. Re-route then recompute to fix.
// - Procedure: Update ETA_FF before issuing flow actions.
// - Procedure: Ask pilots for revised TAS speeds if you suspect they are inaccurate.
// - Procedure: Ask pilots for winds at 10k and 6k ft to update Maestro winds.

// RealOps Sydney Must-Haves:
// - [ ] Reliable connection handling (retries, recover from disconnects, manual reconnect button)
// - [ ] Show connection status in the UI
// - [ ] Show information messages
// - [ ] Fix recompute behavior
//   - [ ] New sequencing algorithm (but order the sequence dynamically based on estimates and landing times)

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
    readonly ILogger? _logger;

    public Plugin()
    {
        try
        {
            EnsureDpiAwareness();
            ConfigureServices();
            ConfigureTheme();
            AddToolbarItem();

            _mediator = Ioc.Default.GetRequiredService<IMediator>();
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

    void ConfigureServices()
    {
        var configuration = ConfigureConfiguration();
        var logger = ConfigureLogger(configuration.Logging);

        Ioc.Default.ConfigureServices(
            new ServiceCollection()
                .AddConfiguration(configuration)
                .AddSingleton(configuration.Server)
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
                .AddSingleton<IErrorReporter>(new ErrorReporter(Name))
                .AddSingleton<WindowManager>()
                .BuildServiceProvider());
    }

    PluginConfiguration ConfigureConfiguration()
    {
        const string configFileName = "Maestro.json";
        var profileDirectory = FindProfileDirectory();

        var configFilePath = Path.Combine(profileDirectory, configFileName);

        if (!File.Exists(configFilePath))
            throw new MaestroException($"Unable to locate {configFileName}.");

        var configurationJson = File.ReadAllText(configFilePath);
        var configuration = JsonConvert.DeserializeObject<PluginConfiguration>(configurationJson)!;

        // TODO: Reloadable configuration would be cool
        return configuration;
    }

    ILogger ConfigureLogger(ILoggingConfiguration loggingConfiguration)
    {
        var logFileName = Path.Combine(Helpers.GetFilesFolder(), "maestro_log.txt");
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

    string FindProfileDirectory()
    {
        var vatsysFilesDirectory = Helpers.GetFilesFolder();
        var profilesDirectory = Path.Combine(vatsysFilesDirectory, "Profiles");
        var profileDirectories = Directory.GetDirectories(profilesDirectory);

        foreach (var profileDirectory in profileDirectories)
        {
            var profileXmlFile = Path.Combine(profileDirectory, "Profile.xml");
            if (!File.Exists(profileXmlFile))
                continue;

            var profileXml = File.ReadAllText(profileXmlFile);
            var fullProfileName = ExtractFullName(profileXml);

            // Profile.Name is FullName Version and Revision combined
            if (Profile.Name.StartsWith(fullProfileName))
                return profileDirectory;
        }

        throw new MaestroException($"Unable to locate profile directory for {Profile.Name}. Maestro configuration cannot be loaded.");

        string ExtractFullName(string xmlContent)
        {
            var doc = XDocument.Parse(xmlContent);
            var profileElement = doc.Element("Profile");
            return profileElement?.Attribute("FullName")?.Value ?? string.Empty;
        }
    }

    void AddToolbarItem()
    {
        var menuItem = new CustomToolStripMenuItem(
            CustomToolStripMenuItemWindowType.Main,
            MenuItemCategory,
            new ToolStripMenuItem("New TFMS Window"));

        menuItem.Item.Click += (_, _) => OpenSetupWindow();

        MMI.AddCustomMenuItem(menuItem);
        MMI.AddCustomMenuItem(
            new CustomToolStripMenuItem(
                CustomToolStripMenuItemWindowType.Main,
                MenuItemCategory,
                new ToolStripSeparator()));
    }

    const string MenuItemCategory = "TFMS";
    static readonly IDictionary<string, CustomToolStripMenuItem> MenuItems = new Dictionary<string, CustomToolStripMenuItem>();

    internal static void AddMenuItemFor(string airportIdentifier, Form window)
    {
        var menuItem = new CustomToolStripMenuItem(
            CustomToolStripMenuItemWindowType.Main,
            MenuItemCategory,
            new ToolStripMenuItem(airportIdentifier));
        MMI.AddCustomMenuItem(menuItem);

        MenuItems[airportIdentifier] = menuItem;

        menuItem.Item.Click += (_, _) =>
        {
            window.WindowState = FormWindowState.Normal;
            window.Activate();
        };
    }

    internal static void RemoveMenuItemFor(string airportIdentifier)
    {
        if (MenuItems.TryGetValue(airportIdentifier, out var menuItem))
            MMI.RemoveCustomMenuItem(menuItem);
    }

    // TODO: Extract this into a mediator request
    void OpenSetupWindow()
    {
        try
        {
            var airportConfigurationProvider = Ioc.Default.GetRequiredService<IAirportConfigurationProvider>();
            var sessionManager = Ioc.Default.GetRequiredService<ISessionManager>();
            var windowManager = Ioc.Default.GetRequiredService<WindowManager>();
            var configurations = airportConfigurationProvider
                .GetAirportConfigurations()
                .Where(a => !sessionManager.HasSessionFor(a.Identifier))
                .ToArray();

            if (configurations.Length == 0)
            {
                // TODO: Eurocat style message box
                MessageBox.Show(
                    "All configured airports already have an active TFMS sequence.",
                    "No Available Airports",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            windowManager.FocusOrCreateWindow(
                WindowKeys.SetupWindow,
                "Maestro Setup",
                handle =>
                {
                    var viewModel = new SetupViewModel(
                        configurations,
                        Ioc.Default.GetRequiredService<IMediator>(),
                        handle,
                        Ioc.Default.GetRequiredService<IErrorReporter>());

                    return new SetupView(viewModel);
                });
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "An error occurred while opening the Setup window.");
            Errors.Add(ex, Name);
        }
    }

    void NetworkOnConnected(object sender, EventArgs e)
    {
        try
        {
            var sessionManager = Ioc.Default.GetRequiredService<ISessionManager>();
            foreach (var airportIdentifier in sessionManager.ActiveSessions)
            {
                try
                {
                    _mediator?.Send(
                        new StartSessionRequest(airportIdentifier, Network.Callsign),
                        CancellationToken.None);
                }
                catch
                {
                    // Just play it safe and destroy the session if starting it fails
                    _mediator?.Send(
                        new DestroySessionRequest(airportIdentifier),
                        CancellationToken.None);
                }
            }
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
            var sessionManager = Ioc.Default.GetRequiredService<ISessionManager>();
            foreach (var airportIdentifier in sessionManager.ActiveSessions)
            {
                _mediator?.Send(new StopSessionRequest(airportIdentifier), CancellationToken.None);
            }
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
            _logger?.Error(ex, "An error occurred while handling OnFDRUpdate.");
            Errors.Add(ex, Name);
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
            _logger?.Error(ex, "An error occurred while handling OnRadarTrackUpdate.");
            Errors.Add(ex, Name);
        }
    }

    void PublishFlightUpdatedEvent(FDP2.FDR updated)
    {
        var isActivated = updated.State > FDP2.FDR.FDRStates.STATE_PREACTIVE;
        if (!isActivated)
            return;

        // Estimates have not been calculated yet
        if (!updated.ESTed)
            return;

        var estimates = updated.ParsedRoute
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

        var aircraftCategory = updated.PerformanceData.IsJet
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
            updated.STAR?.Name,
            position,
            estimates);

        _mediator?.Publish(notification, CancellationToken.None);
    }

    internal static DateTimeOffset ToDateTimeOffset(DateTime dateTime)
    {
        return new DateTimeOffset(
            dateTime.Year, dateTime.Month, dateTime.Day,
            dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond,
            TimeSpan.Zero);
    }

    void EnsureDpiAwareness()
    {
        try
        {
            var vatSysPath = GetVatSysExecutablePath();
            if (vatSysPath == null)
                return;

            const string registryPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
            const string dpiValue = "DPIUNAWARE";

            using var key = Registry.CurrentUser.OpenSubKey(registryPath, writable: false);
            var existingValue = key?.GetValue(vatSysPath) as string;

            // If already set, exit early
            if (existingValue != null && existingValue.Contains(dpiValue))
                return;

            // Set the registry key
            using var writableKey = Registry.CurrentUser.OpenSubKey(registryPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(registryPath);

            writableKey.SetValue(vatSysPath, dpiValue, RegistryValueKind.String);

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
            var vatSysPath = GetVatSysExecutablePath();
            if (vatSysPath != null)
            {
                System.Diagnostics.Process.Start(vatSysPath);
                Environment.Exit(0);
            }
        }
        catch (Exception ex)
        {
            Errors.Add(ex, Name);
        }
    }

    string? GetVatSysExecutablePath()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Sawbe\vatSys");
            var installPath = key?.GetValue("Path") as string;

            if (string.IsNullOrEmpty(installPath))
                return null;

            var exePath = Path.Combine(installPath, "bin", "vatSys.exe");
            return File.Exists(exePath) ? exePath : null;
        }
        catch
        {
            return null;
        }
    }
}
