using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.Messages;

public record OpenPendingDeparturesWindowRequest(string AirportIdentifier, FlightMessage[] PendingFlights) : IRequest;
