using Avalonia;
using Avalonia.Media;

namespace Maestro.Avalonia;

public static class Theme
{
    public static Brush LightBrush = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255));
    public static Brush DarkBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0));

    public static SolidColorBrush BackgroundColor { get; set; } = new(Color.FromArgb(255, 160, 170, 170));
    public static SolidColorBrush GenericTextColor { get; set; } = new(Color.FromArgb(255, 96, 0, 0));
    public static SolidColorBrush InteractiveTextColor { get; set; } = new(Color.FromArgb(255, 0, 0, 96));
    public static SolidColorBrush NonInteractiveTextColor { get; set; } = new(Color.FromArgb(255, 90, 90, 90));
    public static SolidColorBrush SelectedButtonColor { get; set; } = new(Color.FromArgb(255, 0, 0, 96));

    public static SolidColorBrush UnstableColor { get; set; } = new(Color.FromArgb(255, 255, 205, 105));
    public static SolidColorBrush StableColor { get; set; } = new(Color.FromArgb(255, 0, 0, 96));
    public static SolidColorBrush SuperStableColor { get; set; } = new(Color.FromArgb(255, 255, 255, 255));
    public static SolidColorBrush FrozenColor { get; set; } = new(Color.FromArgb(255, 96, 0, 0));
    public static SolidColorBrush LandedColor { get; set; } = new(Color.FromArgb(255, 0, 235, 235));

    public static SolidColorBrush Expedite { get; set; } = new(Color.FromArgb(255, 0, 105, 0));
    public static SolidColorBrush NoDelay { get; set; } = new(Color.FromArgb(255, 0, 0, 96));
    public static SolidColorBrush DelayMinor { get; set; } = new(Color.FromArgb(255, 0, 235, 235));
    public static SolidColorBrush TmaPressure { get; set; } = new(Color.FromArgb(255, 255, 255, 255));
    public static SolidColorBrush DelayMajor { get; set; } = new(Color.FromArgb(255, 235, 235, 0));

    // TODO: Support live updating font sizes
    public static FontFamily FontFamily { get; set; } = new FontFamily("SF Mono");
    public static double FontSize { get; set; } = 16.0;
    public static FontWeight FontWeight { get; set; } = FontWeight.Bold;

    public static Thickness BeveledBorderThickness = new(2);
    public static double BeveledLineWidth = 4;
}
