using MediatR;

namespace Maestro.Core.Messages;

public record InsertOvershootFlightRequest(
    string AirportIdentifier,
    string Callsign,
    InsertionPoint InsertionPoint,
    string ReferenceCallsign) : IRequest;

public record InsertPendingFlightRequest(
    string AirportIdentifier,
    string Callsign,
    InsertionPoint InsertionPoint,
    string ReferenceCallsign) : IRequest;
