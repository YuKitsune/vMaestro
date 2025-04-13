using System.Reflection;
using Maestro.Core.Configuration;
using Maestro.Core.Dtos.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using vatsys;

namespace Maestro.Plugin.Configuration;

public class PluginConfigurationProvider :
    ILoggingConfigurationProvider,
    IEstimateConfiguration,
    IAirportConfigurationProvider,
    ISeparationConfigurationProvider
{
    const string ConfigurationFileName = "Maestro.json";

    readonly Lazy<PluginConfiguration> _lazyPluginConfiguration = new(GetPluginConfiguration);

    public LogLevel GetLogLevel()
    {
        return _lazyPluginConfiguration.Value.Logging.LogLevel;
    }

    public string GetOutputPath()
    {
        return _lazyPluginConfiguration.Value.Logging.OutputPath;
    }

    public FeederFixEstimateSource FeederFixEstimateSource()
    {
        return _lazyPluginConfiguration.Value.FeederFixEstimateSource;
    }

    public LandingEstimateSource LandingEstimateSource()
    {
        return _lazyPluginConfiguration.Value.LandingEstimateSource;
    }

    public AirportConfiguration[] GetAirportConfigurations()
    {
        return _lazyPluginConfiguration.Value.Airports;
    }

    public SeparationRule[] GetSeparationRules()
    {
        return _lazyPluginConfiguration.Value.SeparationRules;
    }

    static PluginConfiguration GetPluginConfiguration()
    {
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;

        var directory = new FileInfo(assemblyLocation).Directory;
        while (directory is not null && ContinueSearch(directory))
        {
            var configFilePath = Path.Combine(directory.FullName, ConfigurationFileName);
            if (File.Exists(configFilePath))
                return LoadConfiguration(configFilePath);
            
            directory = directory.Parent;
        }

        throw new Exception($"Couldn't find {ConfigurationFileName}");
    }

    static bool ContinueSearch(DirectoryInfo directory)
    {
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
