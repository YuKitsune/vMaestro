using System.Globalization;
using Avalonia.Data.Converters;

namespace Maestro.Avalonia.Converters;

public class RunwaysToLadderTitleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string[] { Length: > 0 } array)
        {
            return string.Join(" - ", array);
        }

        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
