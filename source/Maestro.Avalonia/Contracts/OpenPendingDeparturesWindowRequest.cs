using Maestro.Contracts.Flights;
using MediatR;

namespace Maestro.Avalonia.Contracts;

public record OpenPendingDeparturesWindowRequest(string AirportIdentifier, PendingFlightDto[] PendingFlights) : IRequest;
