using MediatR;

namespace Maestro.Wpf.Messages;

public record OpenTerminalConfigurationRequest(string AirportIdentifier) : IRequest;