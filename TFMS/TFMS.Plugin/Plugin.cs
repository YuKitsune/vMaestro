using System.ComponentModel.Composition;
using System.Windows.Forms;
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
                Errors.Add(new Exception(ex.Message), DisplayName);
                if (ex.InnerException != null) Errors.Add(new Exception(ex.InnerException.Message), DisplayName);
            }
        }

        void TFMSMenu_Click(object sender, EventArgs e)
        {
            ShowTFMS();
        }

        private static void ShowTFMS()
        {

            if (TFMSWindow == null || TFMSWindow.IsDisposed)
            {
                TFMSWindow = new TFMSWindow();
            }

            var mainForm = Application.OpenForms["MainForm"];

            if (mainForm == null) return;

            try
            {
                MMI.InvokeOnGUI(delegate () { TFMSWindow.Show(mainForm); });
            }
            catch { }
        }

        void Network_Connected(object sender, EventArgs e)
        {
            // TODO: Deinit.
        }

        void Network_Disconnected(object sender, EventArgs e)
        {
            // TODO: Deinit.
        }

        public void OnFDRUpdate(FDP2.FDR updated) {}

        public void OnRadarTrackUpdate(RDP.RadarTrack updated) { }

        public void Dispose()
        {
            Network_Disconnected(this, null);
        }
    }
}
