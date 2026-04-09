using MediatR;

namespace Maestro.Avalonia.Contracts;

public record OpenConnectionWindowRequest(string AirportIdentifier) : IRequest;
