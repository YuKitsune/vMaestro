using Maestro.Core.Connectivity.Contracts;
using MediatR;

namespace Maestro.Core.Messages;

public record ChangeRunwayModeRequest(
    string AirportIdentifier,
    RunwayModeDto RunwayMode,
    DateTimeOffset LastLandingTimeForOldMode,
    DateTimeOffset FirstLandingTimeForNewMode)
    : IRequest, IRelayableRequest;
