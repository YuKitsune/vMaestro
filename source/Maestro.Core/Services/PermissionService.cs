using Maestro.Core.Configuration;

namespace Maestro.Core.Services;

public class PermissionService : IPermissionService
{
    private Role _currentRole = Role.Observer;
    private IDictionary<string, Role[]> _currentPermissions = PermissionHelper.FullAccess();

    public Role CurrentRole => _currentRole;
    public IDictionary<string, Role[]> CurrentPermissions => _currentPermissions;

    public bool CanPerformAction(string actionKey)
    {
        if (!_currentPermissions.TryGetValue(actionKey, out var allowedRoles))
            return false;

        return allowedRoles.Contains(_currentRole);
    }

    public void UpdatePermissions(IDictionary<string, Role[]> permissions)
    {
        _currentPermissions = permissions;
    }

    public void UpdateRole(Role role)
    {
        _currentRole = role;
    }
}