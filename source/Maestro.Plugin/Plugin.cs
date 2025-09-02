using System.ComponentModel.Composition;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.DependencyInjection;
using Maestro.Core;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Plugin.Configuration;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf;
using Maestro.Wpf.Integrations;
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
    const string Name = "Maestro - Debug";
#else
    const string Name = "Maestro";
#endif

    string IPlugin.Name => Name;

    static BaseForm? _maestroWindow;

    readonly IMediator? _mediator;
    readonly ILogger? _logger;

    public Plugin()
    {
        try
        {
            ConfigureServices();
            ConfigureTheme();
            AddMenuItem();
            AddResetMenuItem();

            _mediator = Ioc.Default.GetRequiredService<IMediator>();
            _logger = Ioc.Default.GetRequiredService<ILogger>();

            var executingAssembly = Assembly.GetExecutingAssembly();
            var version = executingAssembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? string.Empty;
#if RELEASE
            version = version.Split('+').First();
#endif
            _logger.Information("{PluginName} {Version} initialized.", Name, version);

            Network.Connected += Network_Connected;
            Network.Disconnected += Network_Disconnected;
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
                .AddSingleton<IServerConnection, StubServerConnection>() // TODO
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

    void AddMenuItem()
    {
        var menuItem = new CustomToolStripMenuItem(
            CustomToolStripMenuItemWindowType.Main,
            CustomToolStripMenuItemCategory.Windows,
            new ToolStripMenuItem("TFMS"));

        menuItem.Item.Click += (_, _) =>
        {
            ShowMaestro();
        };

        MMI.AddCustomMenuItem(menuItem);
    }

    void AddResetMenuItem()
    {
        var resetMenuItem = new CustomToolStripMenuItem(
            CustomToolStripMenuItemWindowType.Main,
            CustomToolStripMenuItemCategory.Tools,
            new ToolStripMenuItem("Reset Maestro"));

        resetMenuItem.Item.Click += (_, _) =>
        {
            MMI.InvokeOnGUI(delegate
            {
                if (MessageBox.Show(
                        "Are you sure you want to reset Maestro?",
                        "Reset Maestro",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    _mediator?.Send(new ResetRequest());
                }
            });
        };

        MMI.AddCustomMenuItem(resetMenuItem);
    }

    static void ShowMaestro()
    {
        try
        {
            if (_maestroWindow == null || _maestroWindow.IsDisposed)
            {
                _maestroWindow = new VatSysForm(
                    title: "TFMS",
                    new MaestroView(),
                    shrinkToContent: false)
                {
                    Width = 560,
                    Height = 800
                };
            }

            var mainForm = System.Windows.Forms.Application.OpenForms["MainForm"];
            if (mainForm == null)
                return;

            MMI.InvokeOnGUI(delegate { _maestroWindow.Show(mainForm); });
        }
        catch (Exception ex)
        {
            Errors.Add(ex, Name);
        }
    }

    void Network_Connected(object sender, EventArgs e)
    {
        try
        {
            // TODO: Load sequence from local storage

            _mediator?.Send(new StartSequencingAllRequest());

            foreach (var fdr in FDP2.GetFDRs)
            {
                OnFDRUpdate(fdr);
            }
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "An error occurred while handling Network_Connected.");
            Errors.Add(ex, Name);
        }
    }

    void Network_Disconnected(object sender, EventArgs e)
    {
        // _mediator.Send(new ShutdownRequest());
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
        var wake = updated.AircraftWake switch
        {
            "J" => WakeCategory.SuperHeavy,
            "H" => WakeCategory.Heavy,
            "M" => WakeCategory.Medium,
            "L" => WakeCategory.Light,
            _ => WakeCategory.Heavy
        };

        var estimates = updated.ParsedRoute
            .Select((s, i) => new FixEstimate(
                s.Intersection.Name,
                ToDateTimeOffset(s.ETO),
                i <= updated.ParsedRoute.OverflownIndex // If this fix has been overflown, then use the ATO
                    ? ToDateTimeOffset(s.ATO) // BUG: If a flight has passed FF before we connect to the network, this will be MaxValue. ATO is unknown.
                    : null))
            .ToArray();

        var isActivated = updated.State > FDP2.FDR.FDRStates.STATE_PREACTIVE;
        if (!isActivated)
            return;

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

        var notification = new FlightUpdatedNotification(
            updated.Callsign,
            updated.AircraftType,
            wake,
            updated.DepAirport,
            updated.DesAirport,
            ToDateTimeOffset(updated.ETD),
            updated.EET,
            updated.STAR?.Name,
            position,
            estimates);

        _mediator?.Publish(notification);
    }

    internal static DateTimeOffset ToDateTimeOffset(DateTime dateTime)
    {
        return new DateTimeOffset(
            dateTime.Year, dateTime.Month, dateTime.Day,
            dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond,
            TimeSpan.Zero);
    }
}
