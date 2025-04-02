using System.Windows.Forms.Integration;
using System.Windows.Forms;
using vatsys;
using Maestro.Wpf;

namespace Maestro.Plugin
{
    public class MaestroWindow : BaseForm
    {
        public MaestroWindow()
        {
            AutoScaleMode = AutoScaleMode.Font;
            var elementHost = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = new MaestroView()
            };

            Controls.Add(elementHost);
        }
    }
}
