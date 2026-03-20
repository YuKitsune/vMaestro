using MediatR;

namespace Maestro.Wpf.Contracts;

public record OpenDesequencedWindowResponse;
public record OpenDesequencedWindowRequest(string AirportIdentifier, string[] Callsigns) : IRequest<OpenDesequencedWindowResponse>;