using Maestro.Core.Integration;
using Maestro.Core.Model;
using Maestro.Core.Sessions;

namespace Maestro.Core.Tests.Mocks;

public class MockTrajectoryService : ITrajectoryService
{
    readonly TerminalTrajectory _defaultTerminalTrajectory;
    readonly Dictionary<string, TerminalTrajectory> _flightTrajectories = new();
    readonly List<TrajectoryConfiguration> _configurations = new();
    readonly Dictionary<(string, string?, string), string[]> _approachTypes = new();

    public MockTrajectoryService(TimeSpan? defaultTtg = null)
    {
        var ttg = defaultTtg ?? TimeSpan.FromMinutes(20);
        _defaultTerminalTrajectory = new TerminalTrajectory(ttg, ttg, ttg);
    }

    public MockTrajectoryService WithTrajectoryForFlight(Flight flight, TerminalTrajectory terminalTrajectory)
    {
        _flightTrajectories[flight.Callsign] = terminalTrajectory;
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

    public TerminalTrajectory GetTrajectory(Flight flight, string runwayIdentifier, string approachType, string[] fixNames, Wind upperWind)
    {
        if (_flightTrajectories.TryGetValue(flight.Callsign, out var trajectory))
            return trajectory;

        var match = FindBestMatch(
            flight.AircraftType,
            flight.DestinationIdentifier,
            flight.FeederFixIdentifier,
            runwayIdentifier,
            approachType);

        return match ?? _defaultTerminalTrajectory;
    }

    public TerminalTrajectory GetTrajectory(
        AircraftPerformanceData aircraftPerformanceData,
        string destinationIdentifier,
        string? feederFixIdentifier,
        string runwayIdentifier,
        string approachType,
        string[] fixNames,
        Wind upperWind)
    {
        var match = FindBestMatch(aircraftPerformanceData.TypeCode, destinationIdentifier, feederFixIdentifier, runwayIdentifier, approachType);
        return match ?? _defaultTerminalTrajectory;
    }

    public EnrouteTrajectory GetEnrouteTrajectory(string airportIdentifier, string[] waypointNames, string feederFixIdentifier)
    {
        return new EnrouteTrajectory(TimeSpan.FromMinutes(8), TimeSpan.Zero);
    }

    public TerminalTrajectory GetAverageTrajectory(string airportIdentifier)
    {
        return _defaultTerminalTrajectory;
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

    TerminalTrajectory? FindBestMatch(
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
        return matches.OrderByDescending(c => c.Specificity).First().TerminalTrajectory;
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

        public MockTrajectoryService Returns(TerminalTrajectory terminalTrajectory)
        {
            _service.AddConfiguration(new TrajectoryConfiguration(
                _aircraftType,
                _destinationIdentifier,
                _feederFixIdentifier,
                _runwayIdentifier,
                _approachType,
                terminalTrajectory));

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
        public TerminalTrajectory TerminalTrajectory { get; }
        public int Specificity { get; }

        public TrajectoryConfiguration(
            string? aircraftType,
            string? destinationIdentifier,
            string? feederFixIdentifier,
            string? runwayIdentifier,
            string? approachType,
            TerminalTrajectory terminalTrajectory)
        {
            AircraftType = aircraftType;
            DestinationIdentifier = destinationIdentifier;
            FeederFixIdentifier = feederFixIdentifier;
            RunwayIdentifier = runwayIdentifier;
            ApproachType = approachType;
            TerminalTrajectory = terminalTrajectory;

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
