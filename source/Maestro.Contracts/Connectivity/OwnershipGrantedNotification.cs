using MediatR;

namespace Maestro.Contracts.Connectivity;

public record OwnershipGrantedNotification(string AirportIdentifier) : INotification;
