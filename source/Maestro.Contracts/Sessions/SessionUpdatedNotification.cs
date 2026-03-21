using MediatR;
using MessagePack;

namespace Maestro.Contracts.Sessions;

[MessagePackObject]
public record SessionUpdatedNotification(
    [property: Key(0)] string AirportIdentifier,
    [property: Key(1)] SessionDto Session)
    : INotification;
