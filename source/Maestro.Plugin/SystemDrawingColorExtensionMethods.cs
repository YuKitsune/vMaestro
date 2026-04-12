using Avalonia.Media;

namespace Maestro.Plugin;

public static class SystemDrawingColorExtensionMethods
{
    public static SolidColorBrush ToSolidColorBrush(this System.Drawing.Color color)
    {
        return new SolidColorBrush(new Color(color.A, color.R, color.G, color.B));
    }
}
