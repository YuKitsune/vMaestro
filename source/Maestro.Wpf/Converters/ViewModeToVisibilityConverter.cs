
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Maestro.Core.Configuration;

namespace Maestro.Wpf.Converters;

[ValueConversion(typeof(ViewMode), typeof(Visibility))]
public class ViewModeToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ViewMode currentViewMode &&
            parameter is ViewMode expectedViewMode)
        {
            return currentViewMode == expectedViewMode
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        
        return Visibility.Hidden;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}