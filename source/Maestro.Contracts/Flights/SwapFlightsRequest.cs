using Maestro.Contracts.Connectivity;
using MediatR;

namespace Maestro.Contracts.Flights;

public record SwapFlightsRequest(
    string AirportIdentifier,
    string FirstFlightCallsign,
    string SecondFlightCallsign)
    : IRequest, IRelayableRequest;
