using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using CommunityToolkit.Mvvm.DependencyInjection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TFMS.Core;
using TFMS.Core.Dtos;
using TFMS.Core.Dtos.Messages;
using TFMS.Plugin.Configuration;
using TFMS.Wpf;
using vatsys;
using vatsys.Plugin;

namespace TFMS.Plugin
{
    [Export(typeof(IPlugin))]
    public class Plugin : IPlugin, IDisposable
    {
        public const string Name = "Maestro";
        string IPlugin.Name => "Maestro";

        static BaseForm? TFMSWindow;

        IMediator mediator;

        public Plugin()
        {
            try
            {
                ConfigureServices();
                ConfigureTheme();
                AddMenuItem();

                mediator = Ioc.Default.GetRequiredService<IMediator>();

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
                ShowTFMS();
            };

            MMI.AddCustomMenuItem(menuItem);
        }

        private static void ShowTFMS()
        {
            try
            {
                if (TFMSWindow == null || TFMSWindow.IsDisposed)
                {
                    TFMSWindow = new TFMSWindow
                    {
                        Width = 560,
                        Height = 800
                    };
                }

                var mainForm = System.Windows.Forms.Application.OpenForms["MainForm"];
                if (mainForm == null)
                    return;

                MMI.InvokeOnGUI(delegate () { TFMSWindow.Show(mainForm); });
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
                mediator.Publish(notification);
            }
        }

        public void OnFDRUpdate(FDP2.FDR updated)
        {
            var notification = CreateNotificationFor(updated);
            mediator.Publish(notification);
        }

        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {
            if (updated.CoupledFDR is null)
                return;

            var notification = CreateNotificationFor(updated.CoupledFDR);
            mediator.Publish(notification);
        }

        void Network_Disconnected(object sender, EventArgs e)
        {
            // TODO: Deinit.
        }

        public void Dispose()
        {
            Network_Disconnected(this, new EventArgs());
        }

        public FlightUpdatedNotification CreateNotificationFor(FDP2.FDR fdr)
        {
            return new FlightUpdatedNotification(
                fdr.Callsign,
                fdr.AircraftType,
                fdr.DepAirport,
                fdr.DesAirport,
                fdr.ArrivalRunway?.Name,
                fdr.STAR?.Name,
                fdr.ParsedRoute.Select(s => new FixDTO(s.Intersection.Name, new DateTimeOffset(s.ETO, TimeSpan.Zero))).ToArray());
        }
    }
}
