using System.Globalization;
using Avalonia.Data.Converters;
using Maestro.Avalonia.ViewModels;

namespace Maestro.Avalonia.Converters;

public class LandingRateUnitSymbolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not LandingRateUnit unit)
            return "?";

        return unit switch
        {
            LandingRateUnit.Seconds => "s",
            LandingRateUnit.NauticalMiles => "NM",
            LandingRateUnit.AircraftPerHour => "Ac",
            _ => "s"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
