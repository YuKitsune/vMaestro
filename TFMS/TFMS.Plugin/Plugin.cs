using System.ComponentModel.Composition;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.DependencyInjection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TFMS.Core;
using TFMS.Core.DTOs;
using TFMS.Core.Model;
using TFMS.Wpf;
using vatsys;
using vatsys.Plugin;

namespace TFMS.Plugin
{
    [Export(typeof(IPlugin))]
    public class Plugin : IPlugin, IDisposable
    {
        public string Name => "TFMS";
        public static string DisplayName => "TFMS";

        static TFMSWindow TFMSWindow;

        IMediator mediator;

        public Plugin()
        {
            try
            {
                ConfigureServices();
                AddMenuItem();

                mediator = Ioc.Default.GetRequiredService<IMediator>();

                Network.Connected += Network_Connected;
                Network.Disconnected += Network_Disconnected;
            }
            catch (Exception ex)
            {
                Errors.Add(ex, DisplayName);
            }
        }

        void ConfigureServices()
        {
            Ioc.Default.ConfigureServices(
                new ServiceCollection()
                    .AddViewModels()
                    .AddMaestro()
                    .AddMediatR(c => c.RegisterServicesFromAssemblies([typeof(Sequence).Assembly, typeof(TFMSView).Assembly])) // TODO: CommunityToolkit has a messenger. what if we just used that?
                    .BuildServiceProvider());
        }

        void AddMenuItem()
        {
            var menuItem = new CustomToolStripMenuItem(
                CustomToolStripMenuItemWindowType.Main,
                CustomToolStripMenuItemCategory.Windows,
                new ToolStripMenuItem(DisplayName));

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
                    TFMSWindow = new TFMSWindow();
                }

                var mainForm = Application.OpenForms["MainForm"];
                if (mainForm == null)
                    return;

                MMI.InvokeOnGUI(delegate () { TFMSWindow.Show(mainForm); });
            }
            catch (Exception ex)
            {
                Errors.Add(ex, DisplayName);
            }
        }

        void Network_Connected(object sender, EventArgs e)
        {
            var notification = new InitializedNotification(FDP2.GetFDRs.Select(ConvertFDRToDTO).ToArray());
            mediator.Publish(notification);
        }

        public void OnFDRUpdate(FDP2.FDR updated)
        {
            var notification = new FDRUpdatedNotification(ConvertFDRToDTO(updated));
            mediator.Publish(notification);
        }

        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {
            if (updated.CoupledFDR is null)
                return;

            // TODO: Recalculate things if necessary. Maybe add another notification here for that, or just refactor FDRUpdatedNotification to be more generic and handle that scenario.
            var notification = new FDRUpdatedNotification(ConvertFDRToDTO(updated.CoupledFDR));
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

        public FlightDataRecord ConvertFDRToDTO(FDP2.FDR fdr)
        {
            return new FlightDataRecord(
                fdr.Callsign,
                fdr.DepAirport,
                fdr.DesAirport,
                fdr.ArrivalRunway?.Name,
                fdr.STAR?.Name,
                fdr.ParsedRoute.Select(s => new Fix(s.Intersection.Name, new DateTimeOffset(s.ETO, TimeSpan.Zero))).ToArray());
        }
    }
}
