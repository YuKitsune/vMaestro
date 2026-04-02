namespace Maestro.Core.Model;

public record Trajectory(TimeSpan TimeToGo, TimeSpan Pressure = default, TimeSpan MaxPressure = default);
