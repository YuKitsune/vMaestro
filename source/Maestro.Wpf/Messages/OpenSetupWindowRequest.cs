using MediatR;

namespace Maestro.Wpf.Messages;

public record OpenConnectionWindowRequest(string AirportIdentifier) : IRequest;
