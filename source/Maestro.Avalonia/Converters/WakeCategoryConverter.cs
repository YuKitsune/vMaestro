using System.Globalization;
using Avalonia.Data.Converters;
using Maestro.Contracts.Shared;

namespace Maestro.Avalonia.Converters;

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
                WakeCategory.SuperHeavy => "J",
                _ => "?"
            };
        }

        throw new NotSupportedException();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
