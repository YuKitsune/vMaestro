using MediatR;

namespace Maestro.Wpf.Messages;

public record OpenCoordinationWindowRequest(string AirportIdentifier, string? Callsign) : IRequest;
