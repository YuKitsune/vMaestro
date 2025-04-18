using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using CommunityToolkit.Mvvm.DependencyInjection;
using Maestro.Core;
using Maestro.Core.Handlers;
using Maestro.Core.Model;
using Maestro.Plugin.Configuration;
using Maestro.Wpf;
using Maestro.Wpf.Views;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using vatsys;
using vatsys.Plugin;
using Coordinate = Maestro.Core.Model.Coordinate;

// TODO:
//  - Fix debug window
//  - Single flight recompute
//  - Revisit automatic runway assignment
//  - Ask group chat about how exactly the STAR ETI calculations should work (current ground speed? What about when they slow down?)

namespace Maestro.Plugin
{
    [Export(typeof(IPlugin))]
    public class Plugin : IPlugin, IDisposable
    {
        const string Name = "Maestro";
        string IPlugin.Name => Name;

        static BaseForm? _maestroWindow;

        IMediator _mediator = null!;

        StreamWriter _logFileWriter;

        public Plugin()
        {
            try
            {
                ConfigureServices();
                ConfigureTheme();
                AddMenuItem();

                _mediator = Ioc.Default.GetRequiredService<IMediator>();

                Network.Connected += Network_Connected;
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
            Ioc.Default.ConfigureServices(
                new ServiceCollection()
                    .WithPluginConfigurationSource()
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
                    _maestroWindow = new VatSysForm(new MaestroView())
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
            foreach (var fdr in FDP2.GetFDRs)
            {
                OnFDRUpdate(fdr);
            }
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
                "S" => WakeCategory.SuperHeavy,
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
                updated.STAR?.Name,
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

        public void Dispose()
        {
            _logFileWriter.Flush();
            _logFileWriter.Dispose();
        }
    }
}
