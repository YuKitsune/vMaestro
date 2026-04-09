using Avalonia.Data.Converters;

namespace Maestro.Avalonia.Converters;

public class FontSizeToLineHeightConverter : IValueConverter
{
    const double ScaleFactor = 0.875;

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is null)
            throw new NotSupportedException();
        
        return double.Parse(value.ToString()) * ScaleFactor;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}