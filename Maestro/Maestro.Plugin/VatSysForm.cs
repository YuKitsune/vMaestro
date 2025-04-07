using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Forms;
using vatsys;

namespace Maestro.Plugin
{
    public class VatSysForm : BaseForm
    {
        public VatSysForm(UIElement child)
        {
            AutoScaleMode = AutoScaleMode.Font;
            var elementHost = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = child
            };

            Controls.Add(elementHost);
        }
    }
}
