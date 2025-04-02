using System.Windows.Data;
using System.Windows;
using System.Globalization;

namespace Maestro.Wpf.Converters;

[ValueConversion(typeof(double), typeof(double))]
[ValueConversion(typeof(Thickness), typeof(Thickness))]
public class HalfConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            double d => d / 2,
            Thickness t => new Thickness(t.Left / 2, t.Top / 2, t.Right / 2, t.Bottom / 2),
            _ => throw new NotImplementedException()
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            double d => d * 2,
            Thickness t => new Thickness(t.Left * 2, t.Top * 2, t.Right * 2, t.Bottom * 2),
            _ => throw new NotImplementedException()
        };
    }
}
