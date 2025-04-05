using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using CommunityToolkit.Mvvm.DependencyInjection;
using Maestro.Core;
using Maestro.Core.Dtos;
using Maestro.Core.Dtos.Messages;
using Maestro.Plugin.Configuration;
using Maestro.Wpf;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using vatsys;
using vatsys.Plugin;

namespace Maestro.Plugin
{
    [Export(typeof(IPlugin))]
    public class Plugin : IPlugin, IDisposable
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
                var notification = CreateNotificationFor(fdr);
                _mediator.Publish(notification);
            }
        }

        public void OnFDRUpdate(FDP2.FDR updated)
        {
            // TODO: What to do when an aircraft disconnects
            var notification = CreateNotificationFor(updated);
            _mediator.Publish(notification);
        }

        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {
            if (updated.CoupledFDR is null)
                return;

            var notification = CreateNotificationFor(updated.CoupledFDR);
            _mediator.Publish(notification);
        }

        void Network_Disconnected(object sender, EventArgs e)
        {
            // TODO: Deinit.
        }

        public void Dispose()
        {
            Network_Disconnected(this, EventArgs.Empty);
        }

        FlightUpdatedNotification CreateNotificationFor(FDP2.FDR fdr)
        {
            return new FlightUpdatedNotification(
                fdr.Callsign,
                fdr.AircraftType,
                fdr.DepAirport,
                fdr.DesAirport,
                fdr.ArrivalRunway is not null
                    ? fdr.ArrivalRunway.Name
                    : null,
                fdr.STAR is not null
                    ? fdr.STAR.Name
                    : null,
                fdr.ParsedRoute.Select(s =>
                        new FixDto(
                            s.Intersection.Name,
                            new DateTimeOffset(s.ETO, TimeSpan.Zero)))
                    .ToArray());
        }
    }
}
