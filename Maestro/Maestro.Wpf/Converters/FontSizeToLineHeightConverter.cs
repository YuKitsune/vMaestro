using System.Windows.Data;

namespace Maestro.Wpf.Converters;

public class FontSizeToLineHeightConverter : IValueConverter
{
    const double ScaleFactor = 0.875;

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return double.Parse(value.ToString()) * ScaleFactor;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}