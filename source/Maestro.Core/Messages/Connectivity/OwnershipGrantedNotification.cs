using MediatR;

namespace Maestro.Core.Messages.Connectivity;

public record OwnershipGrantedNotification(string AirportIdentifier) : INotification;
