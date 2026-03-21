using MediatR;

namespace Maestro.Wpf.Contracts;

public record OpenTerminalConfigurationRequest(string AirportIdentifier) : IRequest;