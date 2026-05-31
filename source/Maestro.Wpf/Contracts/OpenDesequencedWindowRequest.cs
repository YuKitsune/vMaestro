using MediatR;

namespace Maestro.Wpf.Contracts;

public record OpenDesequencedWindowResponse;
public record OpenDesequencedWindowRequest(string AirportIdentifier) : IRequest<OpenDesequencedWindowResponse>;