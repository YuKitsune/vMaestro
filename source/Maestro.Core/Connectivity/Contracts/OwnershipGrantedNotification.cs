using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public record OwnershipGrantedNotification(string AirportIdentifier) : INotification;
