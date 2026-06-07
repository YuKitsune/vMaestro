using MediatR;

namespace Maestro.Core.Sessions.Contracts;

public record TrySwapConfigurationRequest(string AirportIdentifier) : IRequest;
