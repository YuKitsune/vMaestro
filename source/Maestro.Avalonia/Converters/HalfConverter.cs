using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Maestro.Avalonia.Converters;

public class HalfConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            double d => d / 2,
            Thickness t => new Thickness(t.Left / 2, t.Top / 2, t.Right / 2, t.Bottom / 2),
            _ => throw new NotImplementedException()
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            double d => d * 2,
            Thickness t => new Thickness(t.Left * 2, t.Top * 2, t.Right * 2, t.Bottom * 2),
            _ => throw new NotImplementedException()
        };
    }
}
