using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace TFMS.Wpf.Converters;

public class FontSizeToHeightConverter : IValueConverter
{
    public static double COEFF = 0.715;
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return (double)value * COEFF;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FontSizeToLineHeightConverter : IValueConverter
{
    public static double COEFF = 0.875;
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return double.Parse(value.ToString()) * COEFF;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
