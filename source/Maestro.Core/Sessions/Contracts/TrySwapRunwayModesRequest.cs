using MediatR;

namespace Maestro.Core.Sessions.Contracts;

public record TrySwapRunwayModesRequest(string AirportIdentifier) : IRequest;
