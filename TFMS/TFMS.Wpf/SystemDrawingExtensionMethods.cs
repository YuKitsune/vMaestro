using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFMS.Wpf
{
    internal static class SystemDrawingExtensionMethods
    {
        public static System.Windows.Media.Color ToWindowsColor(this System.Drawing.Color color)
        {
            return System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static System.Windows.Media.FontFamily ToWindowsFontFamily(this System.Drawing.FontFamily fontFamily)
        {
            return new System.Windows.Media.FontFamily(fontFamily.Name);
        }
    }
}
