using MediatR;

namespace Maestro.Core.Messages;

public record CleanUpFlightsRequest(string AirportIdentifier) : IRequest;
