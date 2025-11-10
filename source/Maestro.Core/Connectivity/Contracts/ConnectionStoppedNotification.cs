using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public record ConnectionStoppedNotification(string AirportIdentifier) : INotification;