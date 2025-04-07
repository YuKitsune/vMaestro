using System.Globalization;
using System.Windows.Data;

namespace Maestro.Wpf.Converters;

[ValueConversion(typeof(DateTimeOffset), typeof(string))]
[ValueConversion(typeof(TimeSpan), typeof(string))]
class MinutesConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            TimeSpan timeSpan => timeSpan.TotalMinutes.ToString("00"),
            DateTimeOffset dateTime => dateTime.Minute.ToString("00"),
            _ => throw new NotSupportedException()
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
