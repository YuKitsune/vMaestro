using MediatR;
using MessagePack;

namespace Maestro.Contracts.Connectivity;

[MessagePackObject]
public record OwnershipGrantedNotification(
    [property: Key(0)] string AirportIdentifier)
    : INotification;
