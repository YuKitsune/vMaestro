using System.ComponentModel.Composition;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Media;
using System.Xml.Linq;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.DependencyInjection;
using Maestro.Core;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
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
    readonly ISessionManager? _sessionManager;
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
            _sessionManager = Ioc.Default.GetRequiredService<ISessionManager>();
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

        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        var configFilePath = Path.Combine(assemblyDirectory, configFileName);

        if (!File.Exists(configFilePath))
            throw new MaestroException($"Unable to locate {configFileName}");

        var configurationJson = File.ReadAllText(configFilePath);
        var configuration = JsonConvert.DeserializeObject<PluginConfiguration>(configurationJson)!;

        // TODO: Reloadable configuration would be cool
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
        if (_sessionManager is null || !_sessionManager.HasSessionFor(updated.DesAirport))
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
}
