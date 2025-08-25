using System.Globalization;
using System.Windows.Data;

namespace Maestro.Wpf.Converters;

[ValueConversion(typeof(DateTimeOffset), typeof(string))]
public class TimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToString("HH\\:mm");
        }

        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
