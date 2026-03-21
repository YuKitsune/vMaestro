using MediatR;
using MessagePack;

namespace Maestro.Contracts.Connectivity;

[MessagePackObject]
public record OwnershipRevokedNotification(
    [property: Key(0)] string AirportIdentifier)
    : INotification;
