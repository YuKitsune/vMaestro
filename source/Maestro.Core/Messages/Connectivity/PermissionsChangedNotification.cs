using Maestro.Core.Configuration;
using MediatR;

namespace Maestro.Core.Messages.Connectivity;

public record PermissionsChangedNotification(string AirportIdentifier, IReadOnlyDictionary<string, Role[]> Permissions) : INotification;
public record PermissionSetChangedNotification(string AirportIdentifier, PermissionSet PermissionSet) : INotification;
