using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.DependencyInjection;
using Maestro.Core;
using Maestro.Core.Handlers;
using Maestro.Core.Model;
using Maestro.Plugin.Configuration;
using Maestro.Plugin.Logging;
using Maestro.Wpf;
using Maestro.Wpf.Views;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using vatsys;
using vatsys.Plugin;
using Coordinate = Maestro.Core.Model.Coordinate;

// TODO:
//  - What's next?
//      - Refactor estimate calculations
//      - Track miles, speed, and GRIB winds
//      - Projected trajectories?
//      - BRL method vs system estimates
//  - Optimise ladder drawing code
//  - landing delay vs FF delay?
//      - Does the 2 minute rule still apply to the feeder fix?
//      - What to do with flights with different landing times but same feeder fix time?

namespace Maestro.Plugin
{
    [Export(typeof(IPlugin))]
    public class Plugin : IPlugin
    {
        const string Name = "Maestro";
        string IPlugin.Name => Name;

        static BaseForm? _maestroWindow;

        IMediator _mediator = null!;

        public Plugin()
        {
            try
            {
                ConfigureServices();
                ConfigureTheme();
                AddMenuItem();

                _mediator = Ioc.Default.GetRequiredService<IMediator>();

                Network.Connected += Network_Connected;
                Network.Disconnected += Network_Disconnected;
            }
            catch (Exception ex)
            {
                Errors.Add(ex, Name);
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
            
            Ioc.Default.ConfigureServices(
                new ServiceCollection()
                    .AddConfiguration(configuration)
                    .AddSingleton<IServerConnection, StubServerConnection>() // TODO
                    .AddViewModels()
                    .AddMaestro()
                    .AddMediatR(c => c.RegisterServicesFromAssemblies(
                        typeof(Core.AssemblyMarker).Assembly,
                        typeof(AssemblyMarker).Assembly,
                        typeof(Wpf.AssemblyMarker).Assembly
                    ))
                    .AddSingleton<IFixLookup, VatsysFixLookup>()
                    .AddSingleton<IPerformanceLookup, VatsysPerformanceDataLookup>()
                    .AddSingleton(new GuiInvoker(MMI.InvokeOnGUI))
                    .AddLogging()
                    .AddSingleton<LoggingConfigurator>()
                    .BuildServiceProvider());
            
            // Configure log provider
            var configurator = Ioc.Default.GetRequiredService<LoggingConfigurator>();
            configurator.ConfigureLogging();
        }

        IConfiguration ConfigureConfiguration()
        {
            const string configFileName = "Maestro.json";
            var profileDirectory = FindProfileDirectory();
            
            var configFilePath = Path.Combine(profileDirectory, configFileName);

            if (!File.Exists(configFilePath))
                throw new MaestroException($"Unable to locate {configFileName}.");
            
            var configuration = new ConfigurationBuilder()
                .SetBasePath(profileDirectory)
                .AddJsonFile(configFilePath, optional: false, reloadOnChange: true)
                .Build();

            return configuration;
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
            
            var debugMenuItem = new CustomToolStripMenuItem(
                CustomToolStripMenuItemWindowType.Main,
                CustomToolStripMenuItemCategory.Windows,
                new ToolStripMenuItem("Debug TFMS"));

            debugMenuItem.Item.Click += (_, _) =>
            {
                MMI.InvokeOnGUI(delegate {  new DebugWindow().Show(); });
            };

            MMI.AddCustomMenuItem(debugMenuItem);
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
            // TODO: Load sequence from local storage
            
            foreach (var fdr in FDP2.GetFDRs)
            {
                OnFDRUpdate(fdr);
            }
        }

        void Network_Disconnected(object sender, EventArgs e)
        {
            // _mediator.Send(new ShutdownRequest());
        }

        public void OnFDRUpdate(FDP2.FDR updated)
        {
            PublishFlightUpdatedEvent(updated);
        }

        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {
            if (updated.CoupledFDR is null)
                return;
            
            // We publish the FlightUpdatedEvent here because OnFDRUpdate and Network_Connected aren't guaranteed to 
            // fire for all flights when initially connecting to the network.
            // The handler for this event will publish the FlightPositionReport if a FlightPosition is available.
            PublishFlightUpdatedEvent(updated.CoupledFDR);
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
                .Skip(updated.ParsedRoute.OverflownIndex)
                .Select(ToEstimate)
                .ToArray();

            // TODO: Is there any point in tracking preactive flight plans?
            //  Real Maestro does, but it might not be necessary for our purposes.
            //  Revisit this when looking into the pending list.
            var isActivated = updated.State > FDP2.FDR.FDRStates.STATE_PREACTIVE;
            if (!isActivated)
                return;

            // Don't track flights that are on the ground
            // TODO: MAESTRO is capable of sequencing flights on the ground. Need to revisit this.
            if (updated.CoupledTrack is null || updated.CoupledTrack.OnGround)
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
                    track.GroundSpeed);
            }

            var notification = new FlightUpdatedNotification(
                updated.Callsign,
                updated.AircraftType,
                wake,
                updated.DepAirport,
                updated.DesAirport,
                updated.ArrivalRunway?.Name,
                isActivated,
                position,
                estimates);
            
            _mediator.Publish(notification);
        }

        static DateTimeOffset ToDateTimeOffset(DateTime dateTime)
        {
            return new DateTimeOffset(
                dateTime.Year, dateTime.Month, dateTime.Day,
                dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond,
                TimeSpan.Zero);
        }

        static FixEstimate ToEstimate(FDP2.FDR.ExtractedRoute.Segment segment)
        {
            return new FixEstimate(
                segment.Intersection.Name,
                ToDateTimeOffset(segment.ETO));
        }
    }
}
