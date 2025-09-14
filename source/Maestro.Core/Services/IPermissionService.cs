using Maestro.Core.Configuration;

namespace Maestro.Core.Services;

public interface IPermissionService
{
    Role CurrentRole { get; }
    IDictionary<string, Role[]> CurrentPermissions { get; }

    bool CanPerformAction(string actionKey);
    void UpdatePermissions(IDictionary<string, Role[]> permissions);
    void UpdateRole(Role role);
}
