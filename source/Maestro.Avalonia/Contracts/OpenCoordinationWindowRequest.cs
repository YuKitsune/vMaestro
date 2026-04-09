using MediatR;

namespace Maestro.Avalonia.Contracts;

public record OpenCoordinationWindowRequest(string AirportIdentifier, string? Callsign) : IRequest;
