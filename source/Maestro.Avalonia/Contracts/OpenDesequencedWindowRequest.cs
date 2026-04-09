using MediatR;

namespace Maestro.Avalonia.Contracts;

public record OpenDesequencedWindowResponse;
public record OpenDesequencedWindowRequest(string AirportIdentifier, string[] Callsigns) : IRequest<OpenDesequencedWindowResponse>;