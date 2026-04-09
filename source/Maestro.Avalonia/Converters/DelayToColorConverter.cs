using System.Globalization;
using Avalonia.Data.Converters;

namespace Maestro.Avalonia.Converters;

public class DelayToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TimeSpan timeSpan)
            throw new NotSupportedException();

        var minutes = Math.Round(timeSpan.TotalMinutes);

        // TODO: Make these thresholds configurable

        if (minutes >= 8)
            return Theme.DelayMajor;

        if (minutes >= 1)
            return Theme.DelayMinor;

        if (minutes > -1)
            return Theme.NoDelay;

        return Theme.Expedite;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
