using MediatR;

namespace Maestro.Wpf.Contracts;

public record OpenCoordinationWindowRequest(string AirportIdentifier, string? Callsign) : IRequest;
