using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Maestro.Avalonia.Controls;

/// <summary>
/// Interaction logic for TimeControl.xaml
/// </summary>
public partial class TimeControl : UserControl, INotifyPropertyChanged
{
    public static readonly StyledProperty<DateTimeOffset?> TimeProperty =
        AvaloniaProperty.Register<TimeControl, DateTimeOffset?>(nameof(Time));
        // DependencyProperty.Register(
        //     nameof(Time),
        //     typeof(DateTimeOffset?),
        //     typeof(TimeControl),
        //     new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTimeChanged));

    public DateTimeOffset? Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

public string TimeText => Time?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "--:--";

    public event PropertyChangedEventHandler? PropertyChanged;

    public TimeControl()
    {
        InitializeComponent();
    }

    private void MinuteUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (Time.HasValue)
        {
            Time = Time.Value.AddMinutes(1);
        }
        else
        {
            Time = DateTimeOffset.Now.AddMinutes(1);
        }
    }

    private void MinuteDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (Time.HasValue)
        {
            Time = Time.Value.AddMinutes(-1);
        }
        else
        {
            Time = DateTimeOffset.Now.AddMinutes(-1);
        }
    }

    private void FiveMinuteUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (Time.HasValue)
        {
            Time = Time.Value.AddMinutes(5);
        }
        else
        {
            Time = DateTimeOffset.Now.AddMinutes(5);
        }
    }

    private void FiveMinuteDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (Time.HasValue)
        {
            Time = Time.Value.AddMinutes(-5);
        }
        else
        {
            Time = DateTimeOffset.Now.AddMinutes(-5);
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
