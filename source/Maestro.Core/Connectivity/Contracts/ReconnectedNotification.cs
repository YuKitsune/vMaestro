using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public record ReconnectedNotification(string AirportIdentifier) : INotification;