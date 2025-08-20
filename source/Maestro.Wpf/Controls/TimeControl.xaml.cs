using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Maestro.Wpf.Controls;

/// <summary>
/// Interaction logic for TimeControl.xaml
/// </summary>
public partial class TimeControl : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty TimeProperty =
        DependencyProperty.Register(
            nameof(Time),
            typeof(DateTimeOffset?),
            typeof(TimeControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTimeChanged));

    public DateTimeOffset? Time
    {
        get => (DateTimeOffset?)GetValue(TimeProperty);
        set => SetValue(TimeProperty, RoundToNextMinute(value));
    }

    private static DateTimeOffset? RoundToNextMinute(DateTimeOffset? time)
    {
        if (!time.HasValue) return time;
        
        var rounded = time.Value;
        if (rounded.Second > 0 || rounded.Millisecond > 0)
        {
            rounded = rounded.AddMinutes(1);
        }
        return new DateTimeOffset(rounded.Year, rounded.Month, rounded.Day, 
                                 rounded.Hour, rounded.Minute, 0, 0, rounded.Offset);
    }

    private static void OnTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TimeControl control)
        {
            control.OnPropertyChanged(nameof(TimeText));
        }
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
