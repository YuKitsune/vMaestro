using System.Globalization;
using Avalonia.Data.Converters;

namespace Maestro.Avalonia.Converters;

class TotalSecondsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan timeSpan)
            return $"{timeSpan.TotalSeconds}s";

        throw new NotSupportedException();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
