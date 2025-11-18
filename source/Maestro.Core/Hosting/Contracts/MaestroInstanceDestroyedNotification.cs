using MediatR;

namespace Maestro.Core.Hosting.Contracts;

public record MaestroInstanceDestroyedNotification(string AirportIdentifier) : INotification;