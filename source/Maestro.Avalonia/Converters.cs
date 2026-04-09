using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Maestro.Avalonia.Controls;
using Maestro.Avalonia.ViewModels;
using Maestro.Contracts.Shared;

namespace Maestro.Avalonia;

public static class Converters
{
    public static readonly FuncValueConverter<double, double> FontSizeToLineHeight =
        new(size => size * 0.875);

    public static readonly FuncValueConverter<double, double> FontSizeToHeight =
        new(size => size * 0.715);

    public static readonly FuncValueConverter<bool, bool> InvertBool =
        new(b => !b, b => !b);

    public static readonly FuncValueConverter<WakeCategory, string> WakeCategorySymbol =
        new(wc => wc switch
        {
            WakeCategory.Light => "L",
            WakeCategory.Medium => "M",
            WakeCategory.Heavy => "H",
            WakeCategory.SuperHeavy => "J",
            _ => "?"
        });

    public static readonly FuncValueConverter<DateTimeOffset, string> Time =
        new(dt => dt.ToString("HH\\:mm"));

    public static readonly FuncValueConverter<object?, string> Minutes =
        new(value => value switch
        {
            TimeSpan ts => ts.TotalMinutes.ToString("00"),
            DateTimeOffset dt => dt.Minute.ToString("00"),
            _ => string.Empty
        });

    public static readonly FuncValueConverter<LandingRateUnit, string> LandingRateUnitSymbol =
        new(unit => unit switch
        {
            LandingRateUnit.Seconds => "s",
            LandingRateUnit.NauticalMiles => "NM",
            LandingRateUnit.AircraftPerHour => "Ac",
            _ => "s"
        });

    public static readonly FuncValueConverter<Direction, double> DirectionToRotationAngle =
        new(direction => direction switch
        {
            Direction.Left => 270,
            Direction.Up => 0,
            Direction.Right => 90,
            Direction.Down => 180,
            _ => throw new ArgumentOutOfRangeException($"Unknown direction: {direction}")
        });

    public static readonly FuncValueConverter<string[], string> RunwaysToLadderTitle =
        new(arr => arr is { Length: > 0 } ? string.Join(" - ", arr) : string.Empty);

    public static readonly FuncValueConverter<string[], string> FeederFixesToLadderTitle =
        new(arr => arr is { Length: > 0 } ? string.Join("  ", arr) : string.Empty);

    public static readonly FuncMultiValueConverter<object, bool> Equality =
        new(values =>
        {
            var list = new System.Collections.Generic.List<object?>(values);
            return list.Count == 2 && ReferenceEquals(list[0], list[1]);
        });

    public static readonly FuncValueConverter<TimeSpan, SolidColorBrush> DelayToColor =
        new(ts =>
        {
            var minutes = Math.Round(ts.TotalMinutes);
            if (minutes >= 8) return Theme.DelayMajor;
            if (minutes >= 1) return Theme.DelayMinor;
            if (minutes > -1) return Theme.NoDelay;
            return Theme.Expedite;
        });

    public static readonly FuncValueConverter<State, SolidColorBrush> StateToColor =
        new(state => state switch
        {
            State.Unstable => Theme.UnstableColor,
            State.Stable => Theme.StableColor,
            State.SuperStable => Theme.SuperStableColor,
            State.Frozen => Theme.FrozenColor,
            State.Landed => Theme.LandedColor,
            _ => throw new ArgumentOutOfRangeException()
        });

    public static readonly FuncValueConverter<TimeSpan, string> TotalSeconds =
        new(ts => $"{ts.TotalSeconds}s");

    public static readonly FuncValueConverter<object?, object?> Half =
        new(value => value switch
        {
            double d => d / 2,
            Thickness t => new Thickness(t.Left / 2, t.Top / 2, t.Right / 2, t.Bottom / 2),
            _ => AvaloniaProperty.UnsetValue
        },
        value => value switch
        {
            double d => d * 2,
            Thickness t => new Thickness(t.Left * 2, t.Top * 2, t.Right * 2, t.Bottom * 2),
            _ => AvaloniaProperty.UnsetValue
        });
}
