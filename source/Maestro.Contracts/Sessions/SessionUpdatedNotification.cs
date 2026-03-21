using MediatR;

namespace Maestro.Contracts.Sessions;

public record SessionUpdatedNotification(
    string AirportIdentifier,
    SessionDto Session)
    : INotification;
