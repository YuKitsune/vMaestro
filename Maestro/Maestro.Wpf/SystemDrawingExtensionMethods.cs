namespace Maestro.Wpf;

public static class SystemDrawingExtensionMethods
{
    public static System.Windows.Media.Color ToWindowsColor(this System.Drawing.Color color)
    {
        return System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
    }
}
