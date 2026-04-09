using System.Globalization;
using Avalonia.Data.Converters;

namespace Maestro.Avalonia.Converters;

public class EqualityConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count != 2) return false;
        return ReferenceEquals(values[0], values[1]);
    }
}