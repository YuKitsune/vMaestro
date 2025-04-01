using System.Reflection;
using Newtonsoft.Json;
using TFMS.Core.Configuration;
using vatsys;

namespace TFMS.Plugin;

public class ProfileConfigurationProvider : IConfigurationProvider
{
    const string ConfigurationFileName = "Maestro.json";
    readonly Lazy<MaestroConfiguration> _lazyLoadConfiguration = new Lazy<MaestroConfiguration>(GetConfigurationInternal);

    public MaestroConfiguration GetConfiguration()
    {
        return _lazyLoadConfiguration.Value;
    }

    static MaestroConfiguration GetConfigurationInternal()
    {
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;

        var directory = new FileInfo(assemblyLocation).Directory;
        while (ContinueSearch(directory))
        {
            var configFilePath = Path.Combine(directory.FullName, ConfigurationFileName);
            if (!File.Exists(configFilePath))
            {
                directory = directory.Parent;
                continue;
            }

            return LoadConfiguration(configFilePath);
        }

        throw new Exception($"Couldn't find {ConfigurationFileName}");
    }

    static bool ContinueSearch(DirectoryInfo? directory)
    {
        if (directory is null)
        {
            return false;
        }

        string[] specialFolders =
            [
                Helpers.GetFilesFolder(),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            ];

        if (specialFolders.Contains(directory.FullName))
        {
            return false;
        }

        return true;
    }

    static MaestroConfiguration LoadConfiguration(string configurationFilePath)
    {
        var json = File.ReadAllText(configurationFilePath);
        var maestroConfiguration = JsonConvert.DeserializeObject<MaestroConfiguration>(json);
        if (maestroConfiguration is null)
            throw new Exception($"Failed to deserialize {ConfigurationFileName}");

        return maestroConfiguration;
    }
}
