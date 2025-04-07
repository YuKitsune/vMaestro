using System.Globalization;
using System.Windows.Data;
using Maestro.Core.Model;

namespace Maestro.Wpf.Converters;

[ValueConversion(typeof(WakeCategory), typeof(string))]
public class WakeCategoryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is WakeCategory wakeCategory)
        {
            return wakeCategory switch
            {
                WakeCategory.Light => "L",
                WakeCategory.Medium => "M",
                WakeCategory.Heavy => "H",
                WakeCategory.SuperHeavy => "S",
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        throw new NotSupportedException();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}