using System.Globalization;
using System.Windows.Data;
using Maestro.Wpf.ViewModels;

namespace Maestro.Wpf.Converters;

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
