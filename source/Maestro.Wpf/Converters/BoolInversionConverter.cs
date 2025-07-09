using System.Globalization;
using System.Windows.Data;

namespace Maestro.Wpf.Converters;

[ValueConversion(typeof(bool), typeof(bool))]
public class BoolInversionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;

        throw new NotSupportedException();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;

        throw new NotSupportedException();
    }
}
