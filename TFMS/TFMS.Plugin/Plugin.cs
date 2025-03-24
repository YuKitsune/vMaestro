using System.ComponentModel.Composition;
using System.Security.Authentication.ExtendedProtection;
using System.Windows.Forms;
using System.Windows.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using DynamicData;
using Microsoft.Extensions.DependencyInjection;
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

        private static CustomToolStripMenuItem TFMSMenu;
        static TFMSWindow TFMSWindow;

        public Plugin()
        {
            if (!Profile.Name.Contains("Australia") && !Profile.Name.Contains("VATNZ"))
            {
                return;
            }

            try
            {
                Network.Connected += Network_Connected;
                Network.Disconnected += Network_Disconnected;

                TFMSMenu = new CustomToolStripMenuItem(
                    CustomToolStripMenuItemWindowType.Main,
                    CustomToolStripMenuItemCategory.Windows,
                    new ToolStripMenuItem(DisplayName));
                TFMSMenu.Item.Click += TFMSMenu_Click;
                MMI.AddCustomMenuItem(TFMSMenu);
            }
            catch (Exception ex)
            {
                Errors.Add(ex, DisplayName);
                if (ex.InnerException != null) Errors.Add(new Exception(ex.InnerException.Message), DisplayName);
            }
        }

        void TFMSMenu_Click(object sender, EventArgs e)
        {
            ShowTFMS();
        }

        private static void ShowTFMS()
        {
            try
            {
                if (TFMSWindow == null || TFMSWindow.IsDisposed)
                {
                    TFMSWindow = InitializeWindow();
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

        static TFMSWindow InitializeWindow()
        {
            Ioc.Default.ConfigureServices(
                new ServiceCollection()
                    .AddSingleton<TFMSViewModel>()
                    .AddSingleton<LadderViewModel>()
                    .BuildServiceProvider());

            return new TFMSWindow();
        }

        void Network_Connected(object sender, EventArgs e)
        {
            // TODO: Deinit.
            var yssy = Airspace2.GetAirport("YPAD")!;

            var yssyArrivals = RDP.RadarTracks.Where(t => t.CoupledFDR?.DesAirport == "YPAD");

            var aircraft = yssyArrivals.Select(r => new AircraftViewModel
            {
                Callsign = r.CoupledFDR.Callsign,
                TotalDelay = TimeSpan.Zero,
                RemainingDelay = TimeSpan.Zero,
                LandingTime = FDP2.GetSystemEstimateAtPosition(r.CoupledFDR, yssy.LatLong)
            }).ToList();

            var viewModel = Ioc.Default.GetRequiredService<LadderViewModel>();
            viewModel.Aircraft = aircraft;
        }

        void Network_Disconnected(object sender, EventArgs e)
        {
            // TODO: Deinit.
        }

        public void OnFDRUpdate(FDP2.FDR updated) {}

        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {
            var yssy = Airspace2.GetAirport("YPAD")!;
            if (updated.CoupledFDR?.DesAirport == "YPAD")
            {
                var viewModel = Ioc.Default.GetRequiredService<LadderViewModel>();

                if (viewModel.Aircraft.Any(a => a.Callsign == updated.CoupledFDR.Callsign))
                    return;

                viewModel.Aircraft.Add(new List<AircraftViewModel>() {new AircraftViewModel
                {
                    Callsign = updated.CoupledFDR.Callsign,
                    LandingTime = FDP2.GetSystemEstimateAtPosition(updated.CoupledFDR, yssy.LatLong)
                } });
            }
        }

        public void Dispose()
        {
            Network_Disconnected(this, null);
        }
    }
}
