using Maestro.Core.Configuration;
using MediatR;

namespace Maestro.Core.Messages.Connectivity;

public record OwnershipGrantedNotification(string AirportIdentifier, Role Role) : INotification;
