using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public record ConnectionCreatedNotification(string AirportIdentifier) : INotification;