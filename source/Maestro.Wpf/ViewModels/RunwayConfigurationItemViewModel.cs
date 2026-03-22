using CommunityToolkit.Mvvm.ComponentModel;
using Maestro.Contracts.Sessions;
using Maestro.Core.Configuration;

namespace Maestro.Wpf.ViewModels;

public partial class RunwayConfigurationItemViewModel : ObservableObject
{
    readonly AirportConfiguration _airportConfiguration;
    readonly WindDto _surfaceWind;

    public string Identifier { get; }
    public string ApproachType { get; }
    public string[] FeederFixes { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NauticalMiles), nameof(AircraftPerHour))]
    int _landingRateSeconds;

    public double NauticalMiles
    {
        get
        {
            if (LandingRateSeconds == 0)
                return 0;

            var runwayDirection = int.Parse(Identifier.Substring(0, 2)) * 10;
            var angle = Math.Abs(_surfaceWind.Direction - runwayDirection) * Math.PI / 180.0;
            var headwindComponent = _surfaceWind.Speed * Math.Cos(angle);

            var groundSpeed = _airportConfiguration.AverageLandingSpeed - headwindComponent;
            var interval = TimeSpan.FromSeconds(LandingRateSeconds);
            var distance = groundSpeed * interval.TotalHours;

            return distance;
        }
    }

    public int AircraftPerHour
    {
        get
        {
            if (LandingRateSeconds == 0)
                return 0;

            var interval = TimeSpan.FromSeconds(LandingRateSeconds);
            return (int)(1 / interval.TotalHours);
        }
    }

    public RunwayConfigurationItemViewModel(
        string identifier,
        string approachType,
        int landingRateSeconds,
        string[] feederFixes,
        AirportConfiguration airportConfiguration,
        WindDto surfaceWind)
    {
        Identifier = identifier;
        ApproachType = approachType;
        FeederFixes = feederFixes;
        LandingRateSeconds = landingRateSeconds;

        _airportConfiguration = airportConfiguration;
        _surfaceWind = surfaceWind;
    }
}
