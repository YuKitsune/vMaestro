using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Forms;
using vatsys;

namespace Maestro.Plugin;

public class VatSysForm : BaseForm
{
    public VatSysForm(string title, UIElement child, bool shrinkToContent)
    {
        Text = title;
            
        var elementHost = new ElementHost();
        if (shrinkToContent)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            child.Arrange(new Rect(child.DesiredSize));
            child.UpdateLayout();
            
            var desired = child.DesiredSize;
            elementHost.Child = child;
            elementHost.Size = new System.Drawing.Size((int)Math.Ceiling(desired.Width), (int)Math.Ceiling(desired.Height));
            elementHost.Location = new System.Drawing.Point(0, 0);
                
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = elementHost.Size;
        }
            
        elementHost.Child = child;
        elementHost.Dock = DockStyle.Fill;
        Controls.Add(elementHost);

        Padding = new Padding(0);
        Margin = new Padding(0);
        PerformLayout();
    }
}