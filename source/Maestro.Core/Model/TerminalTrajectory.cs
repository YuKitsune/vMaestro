namespace Maestro.Core.Model;

public record EnrouteTrajectory(
    TimeSpan MaxLinearEnrouteDelay,
    TimeSpan ShortcutTimeToGain);

public record TerminalTrajectory(
    TimeSpan NormalTimeToGo,
    TimeSpan PressureTimeToGo = default,
    TimeSpan MaxPressureTimeToGo = default);
