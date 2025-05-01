using System.Windows.Data;

namespace Maestro.Wpf.Converters;

public class FontSizeToHeightConverter : IValueConverter
{
    const double ScaleFactor = 0.715;

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is not double size)
            throw new NotSupportedException();
        
        return size * ScaleFactor;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}