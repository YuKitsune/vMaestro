using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Maestro.Wpf.Converters;

[ValueConversion(typeof(TimeSpan), typeof(Color))]
public class DelayToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TimeSpan timeSpan)
            throw new NotSupportedException();

        // TODO: Make these thresholds configurable
        
        if (timeSpan.Minutes < 0)
            return Theme.Expedite;
        
        if (timeSpan.Minutes == 0)
            return Theme.NoDelay;
        
        if (timeSpan.Minutes > 0)
            return Theme.DelayMinor;
        
        if (timeSpan.Minutes >= 8)
            return Theme.DelayMajor;
        
        return Theme.NoDelay;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}