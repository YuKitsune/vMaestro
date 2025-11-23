using System.Reflection;

namespace Maestro.Core;

public static class AssemblyVersionHelper
{
    public static string GetVersion(Assembly assembly)
    {
        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? string.Empty;

#if RELEASE
        return version.Split('+').First();
#else
        return version;
#endif
    }
}
