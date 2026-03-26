using Maestro.Contracts.Flights;
using MediatR;

namespace Maestro.Wpf.Contracts;

public record OpenPendingDeparturesWindowRequest(string AirportIdentifier, PendingFlightDto[] PendingFlights) : IRequest;
