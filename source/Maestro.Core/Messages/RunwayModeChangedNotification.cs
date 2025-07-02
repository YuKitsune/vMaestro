using Maestro.Core.Handlers;
using MediatR;

namespace Maestro.Core.Messages;

public class RunwayModeChangedNotification(
    string airportIdentifier,
    RunwayModeDto currentRunwayMode,
    RunwayModeDto? nextRunwayMode,
    DateTimeOffset runwayModeChangeTime)
    : INotification
{
    public string AirportIdentifier { get; } = airportIdentifier;
    public RunwayModeDto CurrentRunwayMode { get; } = currentRunwayMode;
    public RunwayModeDto? NextRunwayMode { get; } = nextRunwayMode;
    public DateTimeOffset RunwayModeChangeTime { get; } = runwayModeChangeTime;
}