using System.Windows.Forms.Integration;
using System.Windows.Forms;
using vatsys;
using TFMS.Wpf;

namespace TFMS.Plugin
{
    public class TFMSWindow : BaseForm
    {
        public TFMSWindow()
        {
            AutoScaleMode = AutoScaleMode.Font;
            var elementHost = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = new TFMSView()
            };

            this.Controls.Add(elementHost);
        }
    }
}
