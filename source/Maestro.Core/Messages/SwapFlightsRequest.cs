using MediatR;

namespace Maestro.Core.Messages;

public record SwapFlightsRequest(
    string AirportIdentifier,
    string FirstFlightCallsign,
    string SecondFlightCallsign)
    : IRequest;
