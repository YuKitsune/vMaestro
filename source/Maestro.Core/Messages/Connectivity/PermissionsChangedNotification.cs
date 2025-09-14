using Maestro.Core.Configuration;
using MediatR;

namespace Maestro.Core.Messages.Connectivity;

public record PermissionsChangedNotification(string AirportIdentifier, IDictionary<string, Role[]> Permissions) : INotification;
