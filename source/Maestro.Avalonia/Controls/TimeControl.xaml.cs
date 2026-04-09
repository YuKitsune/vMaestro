using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Maestro.Avalonia.Controls;

public partial class TimeControl : UserControl
{
    public static readonly StyledProperty<DateTimeOffset?> TimeProperty =
        AvaloniaProperty.Register<TimeControl, DateTimeOffset?>(nameof(Time));

    public static readonly DirectProperty<TimeControl, string> TimeTextProperty =
        AvaloniaProperty.RegisterDirect<TimeControl, string>(nameof(TimeText), o => o.TimeText);

    public DateTimeOffset? Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    private string _timeText = "--:--";
    public string TimeText
    {
        get => _timeText;
        private set => SetAndRaise(TimeTextProperty, ref _timeText, value);
    }

    public TimeControl()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TimeProperty)
            TimeText = Time?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "--:--";
    }

    private void MinuteUpButton_Click(object sender, RoutedEventArgs e)
    {
        Time = (Time ?? DateTimeOffset.Now).AddMinutes(1);
    }

    private void MinuteDownButton_Click(object sender, RoutedEventArgs e)
    {
        Time = (Time ?? DateTimeOffset.Now).AddMinutes(-1);
    }

    private void FiveMinuteUpButton_Click(object sender, RoutedEventArgs e)
    {
        Time = (Time ?? DateTimeOffset.Now).AddMinutes(5);
    }

    private void FiveMinuteDownButton_Click(object sender, RoutedEventArgs e)
    {
        Time = (Time ?? DateTimeOffset.Now).AddMinutes(-5);
    }
}
