using MediatR;

namespace Maestro.Core.Hosting.Contracts;

public record MaestroInstanceCreatedNotification(string AirportIdentifier) : INotification;