using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using CommunityToolkit.Mvvm.DependencyInjection;
using Maestro.Core;
using Maestro.Core.Dtos;
using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Plugin.Configuration;
using Maestro.Wpf;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using vatsys;
using vatsys.Plugin;

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
            Theme.FontFamily = new FontFamily(MMI.ASDMainFont.FontFamily.Name);
            Theme.FontSize = MMI.ASDMainFont.Size;
            Theme.FontWeight = MMI.ASDMainFont.Bold ? FontWeights.Bold : FontWeights.Regular;
        }

        void ConfigureServices()
        {
            Ioc.Default.ConfigureServices(
                new ServiceCollection()
                    .WithPluginConfigurationSource()
                    .AddSingleton<IServerConnection, StubServerConnection>() // TODO
                    .AddViewModels()
                    .AddMaestro()
                    .AddMediatR(c => c.RegisterServicesFromAssemblies([
                        typeof(Core.Dtos.AssemblyMarker).Assembly,
                        typeof(Core.AssemblyMarker).Assembly,
                        typeof(AssemblyMarker).Assembly,
                        typeof(Wpf.AssemblyMarker).Assembly
                    ]))
                    .BuildServiceProvider());
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

        static void ShowMaestro()
        {
            try
            {
                if (_maestroWindow == null || _maestroWindow.IsDisposed)
                {
                    _maestroWindow = new MaestroWindow
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
            var clock = Ioc.Default.GetRequiredService<IClock>();
            
            // TODO: vatSys estimates will be 00:00 + ETI when the flight is not activated
            
            var estimates = updated.ParsedRoute
                .Skip(updated.ParsedRoute.OverflownIndex)
                .Select(x => new FixDto(x.Intersection.Name, clock.FromVatSysTime(x.ETO)))
                .ToArray();

            var notification = new FlightUpdatedNotification(
                updated.Callsign,
                updated.AircraftType,
                updated.DepAirport,
                updated.DesAirport,
                updated.ArrivalRunway?.Name,
                updated.STAR?.Name,
                updated.CoupledTrack is not null
                ? new Position
                {
                    Altitude = updated.CoupledTrack.CorrectedAltitude,
                    Latitude = updated.CoupledTrack.LatLong.Latitude,
                    Longitude = updated.CoupledTrack.LatLong.Longitude,
                }
                : null,
                updated.State > FDP2.FDR.FDRStates.STATE_PREACTIVE,
                estimates);
            
            _mediator.Publish(notification);
        }

        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {
            if (updated.CoupledFDR is null)
                return;

            var clock = Ioc.Default.GetRequiredService<IClock>();
            
            var estimates = updated.CoupledFDR.ParsedRoute
                .Skip(updated.CoupledFDR.ParsedRoute.OverflownIndex)
                .Select(x => new FixDto(x.Intersection.Name, clock.FromVatSysTime(x.ETO)))
                .ToArray();

            var notification = new FlightPositionReport(
                updated.CoupledFDR.Callsign,
                updated.CoupledFDR.DesAirport,
                new Position
                {
                    Altitude = updated.CorrectedAltitude,
                    Latitude = updated.LatLong.Latitude,
                    Longitude = updated.LatLong.Longitude,
                },
                estimates);
            
            _mediator.Publish(notification);
        }
    }
}
