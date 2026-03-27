using Maestro.Contracts.Flights;
using MediatR;

namespace Maestro.Wpf.Contracts;

public record OpenInsertFlightWindowRequest(
    string AirportIdentifier,
    IInsertFlightOptions Options,
    FlightDto[] LandedFlights,
    PendingFlightDto[] PendingFlights)
    : IRequest;
