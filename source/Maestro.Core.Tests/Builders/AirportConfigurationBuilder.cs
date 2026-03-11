using Maestro.Core.Configuration;

namespace Maestro.Core.Tests.Builders;

public class AirportConfigurationBuilder(string identifier)
{
    string[] _runways = [];
    string[] _feederFixes = [];
    List<RunwayModeConfiguration> _runwayModes = [];
    List<TrajectoryConfiguration> _trajectories = [];
    List<DepartureConfiguration> _departures = [];

    public AirportConfigurationBuilder WithRunways(params string[] runways)
    {
        _runways = runways;
        return this;
    }

    public AirportConfigurationBuilder WithFeederFixes(params string[] feederFixes)
    {
        _feederFixes = feederFixes;
        return this;
    }

    public AirportConfigurationBuilder WithRunwayMode(
        string identifier,
        params RunwayConfiguration[] runways)
    {
        _runwayModes.Add(new RunwayModeConfiguration
        {
            Identifier = identifier,
            Runways = runways
        });

        return this;
    }

    public AirportConfigurationBuilder WithRunwayMode(RunwayModeConfiguration runwayMode)
    {
        _runwayModes.Add(runwayMode);
        return this;
    }

    public AirportConfigurationBuilder WithTrajectory(
        string feederFix,
        IAircraftDescriptor[] aircraftDescriptors,
        string approachType,
        string approachFix,
        string runwayIdentifier,
        int timeToGoMinutes)
    {
        _trajectories.Add(new TrajectoryConfiguration
        {
            FeederFix = feederFix,
            Aircraft = aircraftDescriptors,
            ApproachType = approachType,
            ApproachFix = approachFix,
            RunwayIdentifier = runwayIdentifier,
            TimeToGoMinutes = timeToGoMinutes
        });

        return this;
    }

    public AirportConfigurationBuilder WithTrajectory(
        string feederFix,
        IAircraftDescriptor[] aircraftDescriptors,
        string approachType,
        string runwayIdentifier,
        int timeToGoMinutes)
    {
        _trajectories.Add(new TrajectoryConfiguration
        {
            FeederFix = feederFix,
            Aircraft = aircraftDescriptors,
            ApproachType = approachType,
            RunwayIdentifier = runwayIdentifier,
            TimeToGoMinutes = timeToGoMinutes
        });

        return this;
    }

    public AirportConfigurationBuilder WithTrajectory(
        string feederFix,
        IAircraftDescriptor[] aircraftDescriptors,
        string runwayIdentifier,
        int timeToGoMinutes)
    {
        _trajectories.Add(new TrajectoryConfiguration
        {
            FeederFix = feederFix,
            Aircraft = aircraftDescriptors,
            RunwayIdentifier = runwayIdentifier,
            TimeToGoMinutes = timeToGoMinutes
        });

        return this;
    }

    public AirportConfigurationBuilder WithTrajectory(
        string feederFix,
        string runwayIdentifier,
        int timeToGoMinutes)
    {
        _trajectories.Add(new TrajectoryConfiguration
        {
            FeederFix = feederFix,
            Aircraft = [new AllAircraftTypesDescriptor()],
            RunwayIdentifier = runwayIdentifier,
            TimeToGoMinutes = timeToGoMinutes
        });

        return this;
    }

    public AirportConfigurationBuilder WithTrajectory(TrajectoryConfiguration trajectory)
    {
        _trajectories.Add(trajectory);
        return this;
    }

    public AirportConfigurationBuilder WithDepartureAirport(
        string identifier,
        IAircraftDescriptor[] aircraft,
        int flightTimeMinutes)
    {
        _departures.Add(new DepartureConfiguration
        {
            Identifier = identifier,
            Distance = 0.0,
            Aircraft = aircraft,
            EstimatedFlightTimeMinutes = flightTimeMinutes
        });

        return this;
    }

    public AirportConfiguration Build()
    {
        var airportConfiguration = new AirportConfiguration
        {
            Identifier = identifier,
            Runways = _runways,
            FeederFixes = _feederFixes,
            RunwayModes = _runwayModes.ToArray(),
            Trajectories = _trajectories.ToArray(),
            DepartureAirports = _departures.ToArray(),
            Views = [],
            GlobalCoordinationMessages = [],
            FlightCoordinationMessages = []
        };

        return airportConfiguration;
    }
}
