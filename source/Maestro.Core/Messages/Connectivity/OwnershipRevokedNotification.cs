using MediatR;

namespace Maestro.Core.Messages.Connectivity;

public record OwnershipRevokedNotification(string AirportIdentifier) : INotification;
