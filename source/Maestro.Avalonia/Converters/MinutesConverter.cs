using System.Globalization;
using Avalonia.Data.Converters;

namespace Maestro.Avalonia.Converters;

class MinutesConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            TimeSpan timeSpan => timeSpan.TotalMinutes.ToString("00"),
            DateTimeOffset dateTime => dateTime.Minute.ToString("00"),
            _ => string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
