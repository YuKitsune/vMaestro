using System.Reflection;

namespace Maestro.Core;

public static class AssemblyVersionHelper
{
    public static string GetVersion(Assembly assembly)
    {
        var version = assembly.GetName().Version;
        if (version is null)
            return "0.0.0";

        return $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
