using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public record ConnectionStartedNotification(string AirportIdentifier) : INotification;