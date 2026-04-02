using Maestro.Core.Integration;
using Maestro.Core.Model;
using Maestro.Core.Sessions;

namespace Maestro.Core.Tests.Mocks;

public class MockTrajectoryService : ITrajectoryService
{
    readonly Trajectory _defaultTrajectory;
    readonly Dictionary<string, Trajectory> _flightTrajectories = new();
    readonly List<TrajectoryConfiguration> _configurations = new();
    readonly Dictionary<(string, string?, string), string[]> _approachTypes = new();

    public MockTrajectoryService(TimeSpan? defaultTtg = null)
    {
        var ttg = defaultTtg ?? TimeSpan.FromMinutes(20);
        _defaultTrajectory = new Trajectory(ttg, ttg, ttg);
    }

    public MockTrajectoryService WithTrajectoryForFlight(Flight flight, Trajectory trajectory)
    {
        _flightTrajectories[flight.Callsign] = trajectory;
        return this;
    }

    public MockTrajectoryService WithApproachTypes(
        string airportIdentifier,
        string? feederFixIdentifier,
        string runwayIdentifier,
        params string[] approachTypes)
    {
        _approachTypes[(airportIdentifier, feederFixIdentifier, runwayIdentifier)] = approachTypes;
        return this;
    }

    public TrajectoryConfigurationBuilder WithTrajectory()
    {
        return new TrajectoryConfigurationBuilder(this);
    }

    public Trajectory GetTrajectory(Flight flight, string runwayIdentifier, string approachType, string[] fixNames, Wind upperWind)
    {
        if (_flightTrajectories.TryGetValue(flight.Callsign, out var trajectory))
            return trajectory;

        var match = FindBestMatch(
            flight.AircraftType,
            flight.DestinationIdentifier,
            flight.FeederFixIdentifier,
            runwayIdentifier,
            approachType);

        return match ?? _defaultTrajectory;
    }

    public Trajectory GetTrajectory(
        AircraftPerformanceData aircraftPerformanceData,
        string destinationIdentifier,
        string? feederFixIdentifier,
        string runwayIdentifier,
        string approachType,
        string[] fixNames,
        Wind upperWind)
    {
        var match = FindBestMatch(aircraftPerformanceData.TypeCode, destinationIdentifier, feederFixIdentifier, runwayIdentifier, approachType);
        return match ?? _defaultTrajectory;
    }

    public Trajectory GetAverageTrajectory(string airportIdentifier)
    {
        return _defaultTrajectory;
    }

    public string[] GetApproachTypes(
        string airportIdentifier,
        string? feederFixIdentifier,
        string[] fixNames,
        string runwayIdentifier,
        AircraftPerformanceData aircraftPerformanceData)
    {
        if (_approachTypes.TryGetValue((airportIdentifier, feederFixIdentifier, runwayIdentifier), out var types))
            return types;

        return [];
    }

    Trajectory? FindBestMatch(
        string aircraftType,
        string? destinationIdentifier,
        string? feederFixIdentifier,
        string runwayIdentifier,
        string approachType)
    {
        var matches = _configurations
            .Where(c => c.Matches(aircraftType, destinationIdentifier, feederFixIdentifier, runwayIdentifier, approachType))
            .ToList();

        if (matches.Count == 0)
            return null;

        // Return the most specific match (most criteria specified)
        return matches.OrderByDescending(c => c.Specificity).First().Trajectory;
    }

    internal void AddConfiguration(TrajectoryConfiguration configuration)
    {
        _configurations.Add(configuration);
    }

    public class TrajectoryConfigurationBuilder
    {
        readonly MockTrajectoryService _service;
        string? _aircraftType;
        string? _destinationIdentifier;
        string? _feederFixIdentifier;
        string? _runwayIdentifier;
        string? _approachType;

        internal TrajectoryConfigurationBuilder(MockTrajectoryService service)
        {
            _service = service;
        }

        public TrajectoryConfigurationBuilder ForAircraftType(string aircraftType)
        {
            _aircraftType = aircraftType;
            return this;
        }

        public TrajectoryConfigurationBuilder ToDestination(string destinationIdentifier)
        {
            _destinationIdentifier = destinationIdentifier;
            return this;
        }

        public TrajectoryConfigurationBuilder ViaFeederFix(string feederFixIdentifier)
        {
            _feederFixIdentifier = feederFixIdentifier;
            return this;
        }

        public TrajectoryConfigurationBuilder OnRunway(string runwayIdentifier)
        {
            _runwayIdentifier = runwayIdentifier;
            return this;
        }

        public TrajectoryConfigurationBuilder WithApproach(string approachType)
        {
            _approachType = approachType;
            return this;
        }

        public MockTrajectoryService Returns(Trajectory trajectory)
        {
            _service.AddConfiguration(new TrajectoryConfiguration(
                _aircraftType,
                _destinationIdentifier,
                _feederFixIdentifier,
                _runwayIdentifier,
                _approachType,
                trajectory));

            return _service;
        }
    }

    internal class TrajectoryConfiguration
    {
        public string? AircraftType { get; }
        public string? DestinationIdentifier { get; }
        public string? FeederFixIdentifier { get; }
        public string? RunwayIdentifier { get; }
        public string? ApproachType { get; }
        public Trajectory Trajectory { get; }
        public int Specificity { get; }

        public TrajectoryConfiguration(
            string? aircraftType,
            string? destinationIdentifier,
            string? feederFixIdentifier,
            string? runwayIdentifier,
            string? approachType,
            Trajectory trajectory)
        {
            AircraftType = aircraftType;
            DestinationIdentifier = destinationIdentifier;
            FeederFixIdentifier = feederFixIdentifier;
            RunwayIdentifier = runwayIdentifier;
            ApproachType = approachType;
            Trajectory = trajectory;

            // Calculate specificity (how many criteria are specified)
            Specificity = 0;
            if (!string.IsNullOrEmpty(aircraftType)) Specificity++;
            if (!string.IsNullOrEmpty(destinationIdentifier)) Specificity++;
            if (!string.IsNullOrEmpty(feederFixIdentifier)) Specificity++;
            if (!string.IsNullOrEmpty(runwayIdentifier)) Specificity++;
            if (!string.IsNullOrEmpty(approachType)) Specificity++;
        }

        public bool Matches(
            string aircraftType,
            string? destinationIdentifier,
            string? feederFixIdentifier,
            string runwayIdentifier,
            string approachType)
        {
            if (AircraftType != null && AircraftType != aircraftType)
                return false;

            if (DestinationIdentifier != null && DestinationIdentifier != destinationIdentifier)
                return false;

            if (FeederFixIdentifier != null && FeederFixIdentifier != feederFixIdentifier)
                return false;

            if (RunwayIdentifier != null && RunwayIdentifier != runwayIdentifier)
                return false;

            if (ApproachType != null && ApproachType != approachType)
                return false;

            return true;
        }
    }
}
