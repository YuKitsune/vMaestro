using System.Reflection;
using Maestro.Core;
using vatsys;

namespace Maestro.Plugin.Configuration;

public static class ConfigurationLocator
{
    public static string LocateConfigurationFile()
    {
        const string configFileName = "Maestro.yaml";

        var searchDirectories = new List<string>();

        // Search the profile first
        if (TryFindProfileDirectory(out var profileDirectory))
        {
            searchDirectories.AddRange([
                Path.Combine(profileDirectory.FullName, "Plugins", "Configs", "Maestro"),
                Path.Combine(profileDirectory.FullName, "Plugins", "Configs"),
                Path.Combine(profileDirectory.FullName, "Plugins"),
                profileDirectory.FullName
            ]);
        }

        // Search the assembly directory last
        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        searchDirectories.Add(assemblyDirectory);

        foreach (var searchDirectory in searchDirectories)
        {
            var filePath = Path.Combine(searchDirectory, configFileName);
            if (!File.Exists(filePath))
                continue;

            return filePath;
        }

        throw new MaestroException($"Unable to locate {configFileName}");
    }

    // Thanks Max!
    static bool TryFindProfileDirectory(out DirectoryInfo? directoryInfo)
    {
        directoryInfo = null;
        if (!Profile.Loaded)
            return false;

        var shortNameObject = typeof(Profile).GetField("shortName", BindingFlags.Static | BindingFlags.NonPublic);
        var shortName = (string)shortNameObject.GetValue(shortNameObject);

        directoryInfo = new DirectoryInfo(Path.Combine(Helpers.GetFilesFolder(), "Profiles", shortName));
        return true;
    }
}
