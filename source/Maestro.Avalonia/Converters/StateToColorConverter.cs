using System.Globalization;
using Avalonia.Data.Converters;
using Maestro.Contracts.Shared;

namespace Maestro.Avalonia.Converters;

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