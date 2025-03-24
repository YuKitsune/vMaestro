using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TFMS.Wpf
{
    [ValueConversion(typeof(Thickness), typeof(Thickness))]
    public class TopLeftBevelThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var thickness = (Thickness)value;
            return new Thickness(thickness.Left, thickness.Top, 0.0, 0.0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(Thickness), typeof(Thickness))]
    public class BottomRightBevelThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var thickness = (Thickness)value;
            return new Thickness(0.0, 0.0, thickness.Right, thickness.Bottom);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
