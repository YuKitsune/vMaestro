using System.Windows;
using System.Windows.Media;

namespace Maestro.Wpf;

public static class Theme
{
    public static float Alpha = 0.4f;
    public static Brush LightBrush = new SolidColorBrush(Color.FromScRgb(Alpha, 255, 255, 255));
    public static Brush DarkBrush = new SolidColorBrush(Color.FromScRgb(Alpha, 0, 0, 0));

    public static SolidColorBrush BackgroundColor { get; set; } = new(System.Drawing.Color.FromArgb(255, 160, 170, 170).ToWindowsColor());
    public static SolidColorBrush GenericTextColor { get; set; } = new(System.Drawing.Color.FromArgb(255, 96, 0, 0).ToWindowsColor());
    public static SolidColorBrush InteractiveTextColor { get; set; } = new(System.Drawing.Color.FromArgb(255, 0, 0, 96).ToWindowsColor());
    public static SolidColorBrush NonInteractiveTextColor { get; set; } = new(System.Drawing.Color.FromArgb(255, 90, 90, 90).ToWindowsColor());
    public static SolidColorBrush SelectedButtonColor { get; set; } = new(System.Drawing.Color.FromArgb(255, 0, 0, 96).ToWindowsColor());

    public static SolidColorBrush UnstableColor { get; set; } = new(System.Drawing.Color.FromArgb(255, 255, 205, 105).ToWindowsColor());
    public static SolidColorBrush StableColor { get; set; } = new(System.Drawing.Color.FromArgb(255, 0, 0, 96).ToWindowsColor());
    public static SolidColorBrush SuperStableColor { get; set; } = new(System.Drawing.Color.FromArgb(255, 255, 255, 255).ToWindowsColor());
    public static SolidColorBrush FrozenColor { get; set; } = new(System.Drawing.Color.FromArgb(255, 96, 0, 0).ToWindowsColor());
    public static SolidColorBrush LandedColor { get; set; } = new(System.Drawing.Color.FromArgb(255, 0, 235, 235).ToWindowsColor());

    public static SolidColorBrush Expedite { get; set; } = new(System.Drawing.Color.FromArgb(255, 0, 105, 0).ToWindowsColor());
    public static SolidColorBrush NoDelay { get; set; } = new(System.Drawing.Color.FromArgb(255, 0, 0, 96).ToWindowsColor());
    public static SolidColorBrush DelayMinor { get; set; } = new(System.Drawing.Color.FromArgb(255, 0, 235, 235).ToWindowsColor());
    public static SolidColorBrush TmaPressure { get; set; } = new(System.Drawing.Color.FromArgb(255, 255, 255, 255).ToWindowsColor());
    public static SolidColorBrush DelayMajor { get; set; } = new(System.Drawing.Color.FromArgb(255, 235, 235, 0).ToWindowsColor());

    // TODO: Support live updating font sizes
    public static FontFamily FontFamily { get; set; } = new("Terminus (TTF)");
    public static double FontSize { get; set; } = 16.0;
    public static FontWeight FontWeight { get; set; } = FontWeights.Bold;

    public static Thickness BeveledBorderThickness = new(2);
    public static double BeveledLineWidth = 4;
}
