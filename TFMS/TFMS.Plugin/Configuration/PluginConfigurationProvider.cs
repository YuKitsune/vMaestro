using System.Reflection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TFMS.Core.Configuration;
using TFMS.Core.Dtos.Configuration;
using vatsys;

namespace TFMS.Plugin.Configuration;

public class PluginConfigurationProvider : ILoggingConfigurationProvider, IAirportConfigurationProvider
{
    const string ConfigurationFileName = "Maestro.json";

    readonly Lazy<PluginConfiguration> _lazyPluginConfiguration = new Lazy<PluginConfiguration>(GetPluginConfiguration);

    public LogLevel GetLogLevel()
    {
        return _lazyPluginConfiguration.Value.Logging.LogLevel;
    }

    public string GetOutputPath()
    {
        return _lazyPluginConfiguration.Value.Logging.OutputPath;
    }

    public AirportConfigurationDTO[] GetAirportConfigurations()
    {
        return _lazyPluginConfiguration.Value.Airports;
    }

    static PluginConfiguration GetPluginConfiguration()
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
            return false;

        string[] specialFolders =
            [
                Helpers.GetFilesFolder(),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            ];

        if (specialFolders.Contains(directory.FullName))
            return false;

        return true;
    }

    static PluginConfiguration LoadConfiguration(string configurationFilePath)
    {
        var json = File.ReadAllText(configurationFilePath);
        var configuration = JsonConvert.DeserializeObject<PluginConfiguration>(json);
        if (configuration is null)
            throw new Exception($"Failed to deserialize {ConfigurationFileName}");

        return configuration;
    }
}
