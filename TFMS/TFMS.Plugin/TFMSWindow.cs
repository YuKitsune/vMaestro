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
            AutoScaleMode = AutoScaleMode.Dpi;
            var elementHost = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = new TFMSView(
                    new Theme
                    {
                        Font = Font,
                        BackgroundColor = BackColor,
                        ForegroundColor = ForeColor,
                        ButtonHoverColor = ButtonHoverColor,
                        BorderColor = BorderColor
                    })
            };

            this.Controls.Add(elementHost);
        }
    }
}
