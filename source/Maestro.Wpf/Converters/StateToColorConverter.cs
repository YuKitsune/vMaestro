using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Maestro.Core.Model;

namespace Maestro.Wpf.Converters;

[ValueConversion(typeof(State), typeof(Color))]
public class StateToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not State state)
            throw new NotSupportedException();

        return state switch
        {
            State.Unstable => Theme.UnstableColor,
            State.Stable => Theme.StableColor,
            State.SuperStable => Theme.SuperStableColor,
            State.Frozen => Theme.FrozenColor,
            State.Landed => Theme.LandedColor,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}