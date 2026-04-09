using MediatR;

namespace Maestro.Avalonia.Contracts;

public record OpenTerminalConfigurationRequest(string AirportIdentifier) : IRequest;