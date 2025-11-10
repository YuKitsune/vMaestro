using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public record ConnectionDestroyedNotification(string AirportIdentifier) : INotification;