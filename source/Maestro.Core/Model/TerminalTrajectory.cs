namespace Maestro.Core.Model;

public record EnrouteTrajectory(
    TimeSpan MaxLinearEnrouteDelay,
    TimeSpan ShortCutTimeToGain);

public record TerminalTrajectory(
    TimeSpan NormalTimeToGo,
    TimeSpan PressureTimeToGo = default,
    TimeSpan MaxPressureTimeToGo = default);
