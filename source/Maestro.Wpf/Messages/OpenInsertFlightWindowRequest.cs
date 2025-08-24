using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.Messages;

public record OpenInsertFlightWindowRequest(
    string AirportIdentifier,
    IInsertFlightOptions Options,
    FlightMessage[] LandedFlights,
    FlightMessage[] PendingFlights)
    : IRequest;
