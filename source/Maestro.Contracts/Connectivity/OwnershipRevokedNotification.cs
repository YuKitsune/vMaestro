using MediatR;

namespace Maestro.Contracts.Connectivity;

public record OwnershipRevokedNotification(string AirportIdentifier) : INotification;
