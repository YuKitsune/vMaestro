using System.Windows;
using System.Windows.Media;

namespace TFMS.Wpf;

public static class Theme
{
    public static SolidColorBrush BackgroundColor { get; set; } = new SolidColorBrush(System.Drawing.Color.FromArgb(255, 160, 170, 170).ToWindowsColor());
    public static SolidColorBrush GenericTextColor { get; set; } = new SolidColorBrush(System.Drawing.Color.FromArgb(255, 96, 0, 0).ToWindowsColor());
    public static SolidColorBrush InteractiveTextColor { get; set; } = new SolidColorBrush(System.Drawing.Color.FromArgb(255, 0, 0, 96).ToWindowsColor());
    public static SolidColorBrush NonInteractiveTextColor { get; set; } = new SolidColorBrush(System.Drawing.Color.FromArgb(255, 90, 90, 90).ToWindowsColor());
    public static SolidColorBrush SelectedButtonColor { get; set; } = new SolidColorBrush(System.Drawing.Color.FromArgb(255, 0, 0, 96).ToWindowsColor());

    // TODO: Support live updating font sizes
    public static FontFamily FontFamily { get; set; } = new FontFamily("Terminus (TTF)");
    public static double FontSize { get; set; } = 18.0;
    public static FontWeight FontWeight { get; set; } = FontWeights.Bold;
}
