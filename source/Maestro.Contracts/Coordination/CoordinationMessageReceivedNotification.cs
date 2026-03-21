using MediatR;
using MessagePack;

namespace Maestro.Contracts.Coordination;

[MessagePackObject]
public record CoordinationMessageReceivedNotification(
    [property: Key(0)] string AirportIdentifier,
    [property: Key(1)] DateTimeOffset Time,
    [property: Key(2)] string Sender,
    [property: Key(3)] string Message,
    [property: Key(4)] CoordinationDestination Destination)
    : INotification;
