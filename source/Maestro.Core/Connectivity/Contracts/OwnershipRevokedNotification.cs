using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public record OwnershipRevokedNotification(string AirportIdentifier) : INotification;
