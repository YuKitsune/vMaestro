using System.Globalization;
using System.Windows.Data;
using TFMS.Wpf.Controls;

namespace TFMS.Wpf.Converters;

[ValueConversion(typeof(Direction), typeof(double))]
class DirectionToRotationAngleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Direction direction)
        {
            return direction switch
            {
                Direction.Left => 270,
                Direction.Up => 0,
                Direction.Right => 90,
                Direction.Down => 180,
                _ => throw new ArgumentOutOfRangeException($"Unknown direction: {direction}")
            };
        }

        throw new NotSupportedException();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
