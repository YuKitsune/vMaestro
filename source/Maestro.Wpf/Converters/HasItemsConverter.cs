using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace Maestro.Wpf.Converters;

[ValueConversion(typeof(ICollection), typeof(bool))]
public class HasItemsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ICollection collection)
            return collection.Count > 0;

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}