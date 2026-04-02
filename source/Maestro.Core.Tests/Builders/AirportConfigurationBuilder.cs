using Maestro.Core.Configuration;

namespace Maestro.Core.Tests.Builders;

public class AirportConfigurationBuilder(string identifier)
{
    // Default approach speed used by TrajectoryService when no performance data is found.
    // Segments created by helper overloads use this speed so TTG round-trips correctly at zero wind.
    const double DefaultDescentSpeedKnots = 150.0;

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

    /// <summary>
    /// Creates a trajectory with a single segment whose distance produces exactly <paramref name="timeToGoMinutes"/>
    /// of TTG at <see cref="DefaultDescentSpeedKnots"/> knots with zero wind.
    /// </summary>
    public AirportConfigurationBuilder WithTrajectory(
        string feederFix,
        string runwayIdentifier,
        int timeToGoMinutes)
    {
        _trajectories.Add(new TrajectoryConfiguration
        {
            FeederFix = feederFix,
            RunwayIdentifier = runwayIdentifier,
            Segments = [SingleSegment(timeToGoMinutes)]
        });

        return this;
    }

    /// <summary>
    /// Creates a trajectory with a specific approach type and a single segment whose distance produces
    /// exactly <paramref name="timeToGoMinutes"/> of TTG at <see cref="DefaultDescentSpeedKnots"/> knots
    /// with zero wind.
    /// </summary>
    public AirportConfigurationBuilder WithTrajectory(
        string feederFix,
        string approachType,
        string runwayIdentifier,
        int timeToGoMinutes)
    {
        _trajectories.Add(new TrajectoryConfiguration
        {
            FeederFix = feederFix,
            ApproachType = approachType,
            RunwayIdentifier = runwayIdentifier,
            Segments = [SingleSegment(timeToGoMinutes)]
        });

        return this;
    }

    /// <summary>
    /// Overload that accepts (and ignores) an aircraft descriptor array for test compatibility.
    /// Aircraft no longer influences trajectory lookup — use <see cref="WithTrajectory(string, string, int)"/>
    /// or <see cref="WithTrajectory(string, string, string, int)"/> for new tests.
    /// </summary>
    public AirportConfigurationBuilder WithTrajectory(
        string feederFix,
        IAircraftDescriptor[] aircraftDescriptors,
        string runwayIdentifier,
        int timeToGoMinutes)
    {
        return WithTrajectory(feederFix, runwayIdentifier, timeToGoMinutes);
    }

    /// <summary>
    /// Overload that accepts (and ignores) an aircraft descriptor array for test compatibility.
    /// Aircraft no longer influences trajectory lookup.
    /// </summary>
    public AirportConfigurationBuilder WithTrajectory(
        string feederFix,
        IAircraftDescriptor[] aircraftDescriptors,
        string approachType,
        string runwayIdentifier,
        int timeToGoMinutes)
    {
        return WithTrajectory(feederFix, approachType, runwayIdentifier, timeToGoMinutes);
    }

    /// <summary>
    /// Overload that accepts (and ignores) an aircraft descriptor array and approach fix for test compatibility.
    /// Aircraft and approach fix no longer influence trajectory lookup.
    /// </summary>
    public AirportConfigurationBuilder WithTrajectory(
        string feederFix,
        IAircraftDescriptor[] aircraftDescriptors,
        string approachType,
        string approachFix,
        string runwayIdentifier,
        int timeToGoMinutes)
    {
        return WithTrajectory(feederFix, approachType, runwayIdentifier, timeToGoMinutes);
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

    static TrajectorySegmentConfiguration SingleSegment(int timeToGoMinutes)
    {
        // distance = speed * time; at DefaultDescentSpeedKnots and zero wind, ETI = distance / speed = timeToGoMinutes
        return new TrajectorySegmentConfiguration
        {
            Track = 0,
            DistanceNM = DefaultDescentSpeedKnots * timeToGoMinutes / 60.0
        };
    }
}
