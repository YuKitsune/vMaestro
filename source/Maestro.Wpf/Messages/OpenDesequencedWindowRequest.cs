using MediatR;

namespace Maestro.Wpf.Messages;

public record OpenDesequencedWindowResponse;
public record OpenDesequencedWindowRequest(string AirportIdentifier, string[] Callsigns) : IRequest<OpenDesequencedWindowResponse>;