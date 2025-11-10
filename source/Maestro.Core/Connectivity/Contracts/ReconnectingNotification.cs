using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public record ReconnectingNotification(string AirportIdentifier) : INotification;