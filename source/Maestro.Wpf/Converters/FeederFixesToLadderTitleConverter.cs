using System.Globalization;
using System.Windows.Data;

namespace Maestro.Wpf.Converters;

[ValueConversion(typeof(string[]), typeof(string))]
public class FeederFixesToLadderTitleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string[] { Length: > 0 } array)
        {
            return string.Join("  ", array);
        }

        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
