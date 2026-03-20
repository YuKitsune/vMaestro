using Maestro.Contracts.Connectivity;
using MediatR;

namespace Maestro.Contracts.Runway;

public record ChangeRunwayModeRequest(
    string AirportIdentifier,
    RunwayModeDto RunwayMode,
    DateTimeOffset LastLandingTimeForOldMode,
    DateTimeOffset FirstLandingTimeForNewMode)
    : IRequest, IRelayableRequest;
