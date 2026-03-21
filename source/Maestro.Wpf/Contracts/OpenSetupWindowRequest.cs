using MediatR;

namespace Maestro.Wpf.Contracts;

public record OpenConnectionWindowRequest(string AirportIdentifier) : IRequest;
