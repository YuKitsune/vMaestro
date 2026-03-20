using System.Text.Json;
using Maestro.Contracts.Connectivity;
using Maestro.Contracts.Flights;
using Maestro.Contracts.Runway;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;
using Maestro.Contracts.Slots;
using MessagePack;
using MessagePack.Resolvers;
using Shouldly;

namespace Maestro.Contracts.Tests;

/// <summary>
/// Snapshot-based serialization tests to detect breaking changes in the API contract.
/// These tests serialize DTOs and compare against stored snapshots, then deserialize
/// the snapshot and verify all properties match the original.
/// </summary>
public class SerializationTests
{
    static readonly DateTimeOffset FixedTime = new(2024, 6, 15, 12, 30, 0, TimeSpan.Zero);

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    static readonly MessagePackSerializerOptions MessagePackOptions =
        MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);

    static readonly string SnapshotsDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "Snapshots");

    [Fact]
    public void FlightDto_Serialization_Json()
    {
        var original = CreateFlightDto();
        VerifyJsonSnapshot(original, "FlightDto.json", AssertFlightDtoEquals);
    }

    [Fact]
    public void FlightDto_Serialization_MessagePack()
    {
        var original = CreateFlightDto();
        VerifyMessagePackSnapshot(original, "FlightDto.msgpack", AssertFlightDtoEquals);
    }

    [Fact]
    public void SessionDto_Serialization_Json()
    {
        var original = CreateSessionDto();
        VerifyJsonSnapshot(original, "SessionDto.json", AssertSessionDtoEquals);
    }

    [Fact]
    public void SessionDto_Serialization_MessagePack()
    {
        var original = CreateSessionDto();
        VerifyMessagePackSnapshot(original, "SessionDto.msgpack", AssertSessionDtoEquals);
    }

    [Fact]
    public void RunwayModeDto_Serialization_Json()
    {
        var original = CreateRunwayModeDto();
        VerifyJsonSnapshot(original, "RunwayModeDto.json", AssertRunwayModeDtoEquals);
    }

    [Fact]
    public void RunwayModeDto_Serialization_MessagePack()
    {
        var original = CreateRunwayModeDto();
        VerifyMessagePackSnapshot(original, "RunwayModeDto.msgpack", AssertRunwayModeDtoEquals);
    }

    [Fact]
    public void SlotDto_Serialization_Json()
    {
        var original = CreateSlotDto();
        VerifyJsonSnapshot(original, "SlotDto.json", AssertSlotDtoEquals);
    }

    [Fact]
    public void SlotDto_Serialization_MessagePack()
    {
        var original = CreateSlotDto();
        VerifyMessagePackSnapshot(original, "SlotDto.msgpack", AssertSlotDtoEquals);
    }

    [Fact]
    public void PeerInfo_Serialization_Json()
    {
        var original = new PeerInfo("SY_APP", Role.Approach);
        VerifyJsonSnapshot(original, "PeerInfo.json", AssertPeerInfoEquals);
    }

    [Fact]
    public void PeerInfo_Serialization_MessagePack()
    {
        var original = new PeerInfo("SY_APP", Role.Approach);
        VerifyMessagePackSnapshot(original, "PeerInfo.msgpack", AssertPeerInfoEquals);
    }

    [Fact]
    public void Coordinate_Serialization_Json()
    {
        var original = new Coordinate(-33.9461, 151.1772);
        VerifyJsonSnapshot(original, "Coordinate.json", AssertCoordinateEquals);
    }

    [Fact]
    public void Coordinate_Serialization_MessagePack()
    {
        var original = new Coordinate(-33.9461, 151.1772);
        VerifyMessagePackSnapshot(original, "Coordinate.msgpack", AssertCoordinateEquals);
    }

    [Fact]
    public void FixEstimate_Serialization_Json()
    {
        var original = new FixEstimate("RIVET", FixedTime, FixedTime.AddMinutes(-5));
        VerifyJsonSnapshot(original, "FixEstimate.json", AssertFixEstimateEquals);
    }

    [Fact]
    public void FixEstimate_Serialization_MessagePack()
    {
        var original = new FixEstimate("RIVET", FixedTime, FixedTime.AddMinutes(-5));
        VerifyMessagePackSnapshot(original, "FixEstimate.msgpack", AssertFixEstimateEquals);
    }

    [Fact]
    public void FlightPosition_Serialization_Json()
    {
        var original = new FlightPosition(
            new Coordinate(-33.9461, 151.1772),
            15000,
            VerticalTrack.Descending,
            250.5,
            false);
        VerifyJsonSnapshot(original, "FlightPosition.json", AssertFlightPositionEquals);
    }

    [Fact]
    public void FlightPosition_Serialization_MessagePack()
    {
        var original = new FlightPosition(
            new Coordinate(-33.9461, 151.1772),
            15000,
            VerticalTrack.Descending,
            250.5,
            false);
        VerifyMessagePackSnapshot(original, "FlightPosition.msgpack", AssertFlightPositionEquals);
    }

    void VerifyJsonSnapshot<T>(T original, string snapshotName, Action<T, T> assertEquals)
    {
        var snapshotPath = Path.Combine(SnapshotsDirectory, snapshotName);
        var serialized = JsonSerializer.Serialize(original, JsonOptions);

        if (!File.Exists(snapshotPath))
        {
            Directory.CreateDirectory(SnapshotsDirectory);
            File.WriteAllText(snapshotPath, serialized);
            Assert.Fail($"Snapshot '{snapshotName}' did not exist and was created. Re-run the test.");
        }

        var snapshot = File.ReadAllText(snapshotPath);

        // Verify serialization matches snapshot
        serialized.ShouldBe(snapshot, $"Serialized output does not match snapshot '{snapshotName}'");

        // Verify deserialization from snapshot produces equivalent object
        var deserialized = JsonSerializer.Deserialize<T>(snapshot, JsonOptions);
        deserialized.ShouldNotBe(default);
        assertEquals(original, deserialized);
    }

    void VerifyMessagePackSnapshot<T>(T original, string snapshotName, Action<T, T> assertEquals)
    {
        var snapshotPath = Path.Combine(SnapshotsDirectory, snapshotName);
        var serialized = MessagePackSerializer.Serialize(original, MessagePackOptions);

        if (!File.Exists(snapshotPath))
        {
            Directory.CreateDirectory(SnapshotsDirectory);
            File.WriteAllBytes(snapshotPath, serialized);
            Assert.Fail($"Snapshot '{snapshotName}' did not exist and was created. Re-run the test.");
        }

        var snapshot = File.ReadAllBytes(snapshotPath);

        // Verify serialization matches snapshot
        serialized.ShouldBe(snapshot, $"Serialized output does not match snapshot '{snapshotName}'");

        // Verify deserialization from snapshot produces equivalent object
        var deserialized = MessagePackSerializer.Deserialize<T>(snapshot, MessagePackOptions);
        deserialized.ShouldNotBe(default);
        assertEquals(original, deserialized!);
    }

    static FlightDto CreateFlightDto() => new()
    {
        Callsign = "QFA123",
        AircraftType = "B738",
        WakeCategory = WakeCategory.Medium,
        AircraftCategory = AircraftCategory.Jet,
        OriginIdentifier = "YMML",
        DestinationIdentifier = "YSSY",
        IsFromDepartureAirport = false,
        State = State.Stable,
        ActivatedTime = FixedTime.AddHours(-1),
        HighPriority = false,
        MaximumDelay = TimeSpan.FromMinutes(10),
        NumberInSequence = 5,
        FeederFixIdentifier = "RIVET",
        EstimatedDepartureTime = FixedTime.AddHours(-2),
        InitialFeederFixEstimate = FixedTime.AddMinutes(-15),
        FeederFixEstimate = FixedTime.AddMinutes(-10),
        ManualFeederFixEstimate = false,
        FeederFixTime = FixedTime.AddMinutes(-8),
        ActualFeederFixTime = null,
        AssignedRunwayIdentifier = "34L",
        NumberToLandOnRunway = 3,
        ApproachType = "ILS",
        InitialLandingEstimate = FixedTime.AddMinutes(5),
        LandingEstimate = FixedTime.AddMinutes(7),
        TargetLandingTime = FixedTime.AddMinutes(8),
        LandingTime = FixedTime.AddMinutes(8),
        InitialDelay = TimeSpan.FromMinutes(2),
        RemainingDelay = TimeSpan.FromMinutes(1),
        FlowControls = FlowControls.ReduceSpeed,
        LastSeen = FixedTime,
        Fixes =
        [
            new FixEstimate("RIVET", FixedTime.AddMinutes(-10)),
            new FixEstimate("SOSIJ", FixedTime.AddMinutes(-5))
        ],
        Position = new FlightPosition(
            new Coordinate(-33.9461, 151.1772),
            15000,
            VerticalTrack.Descending,
            250.5,
            false),
        IsManuallyInserted = false,
        TimeToGo = TimeSpan.FromMinutes(18)
    };

    static SessionDto CreateSessionDto() => new()
    {
        AirportIdentifier = "YSSY",
        PendingFlights = [CreateFlightDto()],
        DeSequencedFlights = [],
        Sequence = CreateSequenceDto(),
        DummyCounter = 42
    };

    static SequenceDto CreateSequenceDto() => new()
    {
        CurrentRunwayMode = CreateRunwayModeDto(),
        NextRunwayMode = null,
        LastLandingTimeForCurrentMode = FixedTime.AddHours(2),
        FirstLandingTimeForNextMode = FixedTime.AddHours(3),
        Flights = [CreateFlightDto()],
        Slots = [CreateSlotDto()]
    };

    static RunwayModeDto CreateRunwayModeDto() => new(
        "SODPROPS",
        [
            new RunwayDto("34L", "ILS", 90, ["RIVET", "BOREE"]),
            new RunwayDto("34R", "VISUAL", 90, ["MARUB", "SOSIJ"])
        ],
        120,
        180);

    static SlotDto CreateSlotDto() => new(
        Guid.Parse("12345678-1234-1234-1234-123456789012"),
        FixedTime,
        FixedTime.AddMinutes(30),
        ["34L", "34R"]);

    static void AssertFlightDtoEquals(FlightDto expected, FlightDto actual)
    {
        actual.Callsign.ShouldBe(expected.Callsign);
        actual.AircraftType.ShouldBe(expected.AircraftType);
        actual.WakeCategory.ShouldBe(expected.WakeCategory);
        actual.AircraftCategory.ShouldBe(expected.AircraftCategory);
        actual.OriginIdentifier.ShouldBe(expected.OriginIdentifier);
        actual.DestinationIdentifier.ShouldBe(expected.DestinationIdentifier);
        actual.IsFromDepartureAirport.ShouldBe(expected.IsFromDepartureAirport);
        actual.State.ShouldBe(expected.State);
        actual.ActivatedTime.ShouldBe(expected.ActivatedTime);
        actual.HighPriority.ShouldBe(expected.HighPriority);
        actual.MaximumDelay.ShouldBe(expected.MaximumDelay);
        actual.NumberInSequence.ShouldBe(expected.NumberInSequence);
        actual.FeederFixIdentifier.ShouldBe(expected.FeederFixIdentifier);
        actual.EstimatedDepartureTime.ShouldBe(expected.EstimatedDepartureTime);
        actual.InitialFeederFixEstimate.ShouldBe(expected.InitialFeederFixEstimate);
        actual.FeederFixEstimate.ShouldBe(expected.FeederFixEstimate);
        actual.ManualFeederFixEstimate.ShouldBe(expected.ManualFeederFixEstimate);
        actual.FeederFixTime.ShouldBe(expected.FeederFixTime);
        actual.ActualFeederFixTime.ShouldBe(expected.ActualFeederFixTime);
        actual.AssignedRunwayIdentifier.ShouldBe(expected.AssignedRunwayIdentifier);
        actual.NumberToLandOnRunway.ShouldBe(expected.NumberToLandOnRunway);
        actual.ApproachType.ShouldBe(expected.ApproachType);
        actual.InitialLandingEstimate.ShouldBe(expected.InitialLandingEstimate);
        actual.LandingEstimate.ShouldBe(expected.LandingEstimate);
        actual.TargetLandingTime.ShouldBe(expected.TargetLandingTime);
        actual.LandingTime.ShouldBe(expected.LandingTime);
        actual.InitialDelay.ShouldBe(expected.InitialDelay);
        actual.RemainingDelay.ShouldBe(expected.RemainingDelay);
        actual.FlowControls.ShouldBe(expected.FlowControls);
        actual.LastSeen.ShouldBe(expected.LastSeen);
        actual.IsManuallyInserted.ShouldBe(expected.IsManuallyInserted);
        actual.TimeToGo.ShouldBe(expected.TimeToGo);

        actual.Fixes.Length.ShouldBe(expected.Fixes.Length);
        for (var i = 0; i < expected.Fixes.Length; i++)
        {
            AssertFixEstimateEquals(expected.Fixes[i], actual.Fixes[i]);
        }

        if (expected.Position is not null)
        {
            actual.Position.ShouldNotBeNull();
            AssertFlightPositionEquals(expected.Position, actual.Position);
        }
        else
        {
            actual.Position.ShouldBeNull();
        }
    }

    static void AssertSessionDtoEquals(SessionDto expected, SessionDto actual)
    {
        actual.AirportIdentifier.ShouldBe(expected.AirportIdentifier);
        actual.DummyCounter.ShouldBe(expected.DummyCounter);

        actual.PendingFlights.Length.ShouldBe(expected.PendingFlights.Length);
        for (var i = 0; i < expected.PendingFlights.Length; i++)
        {
            AssertFlightDtoEquals(expected.PendingFlights[i], actual.PendingFlights[i]);
        }

        actual.DeSequencedFlights.Length.ShouldBe(expected.DeSequencedFlights.Length);

        AssertSequenceDtoEquals(expected.Sequence, actual.Sequence);
    }

    static void AssertSequenceDtoEquals(SequenceDto expected, SequenceDto actual)
    {
        AssertRunwayModeDtoEquals(expected.CurrentRunwayMode, actual.CurrentRunwayMode);

        if (expected.NextRunwayMode is not null)
        {
            actual.NextRunwayMode.ShouldNotBeNull();
            AssertRunwayModeDtoEquals(expected.NextRunwayMode, actual.NextRunwayMode);
        }
        else
        {
            actual.NextRunwayMode.ShouldBeNull();
        }

        actual.LastLandingTimeForCurrentMode.ShouldBe(expected.LastLandingTimeForCurrentMode);
        actual.FirstLandingTimeForNextMode.ShouldBe(expected.FirstLandingTimeForNextMode);

        actual.Flights.Length.ShouldBe(expected.Flights.Length);
        for (var i = 0; i < expected.Flights.Length; i++)
        {
            AssertFlightDtoEquals(expected.Flights[i], actual.Flights[i]);
        }

        actual.Slots.Length.ShouldBe(expected.Slots.Length);
        for (var i = 0; i < expected.Slots.Length; i++)
        {
            AssertSlotDtoEquals(expected.Slots[i], actual.Slots[i]);
        }
    }

    static void AssertRunwayModeDtoEquals(RunwayModeDto expected, RunwayModeDto actual)
    {
        actual.Identifier.ShouldBe(expected.Identifier);
        actual.DependencyRateSeconds.ShouldBe(expected.DependencyRateSeconds);
        actual.OffModeSeparationSeconds.ShouldBe(expected.OffModeSeparationSeconds);

        actual.Runways.Length.ShouldBe(expected.Runways.Length);
        for (var i = 0; i < expected.Runways.Length; i++)
        {
            actual.Runways[i].Identifier.ShouldBe(expected.Runways[i].Identifier);
            actual.Runways[i].ApproachType.ShouldBe(expected.Runways[i].ApproachType);
            actual.Runways[i].AcceptanceRateSeconds.ShouldBe(expected.Runways[i].AcceptanceRateSeconds);
            actual.Runways[i].FeederFixes.ShouldBe(expected.Runways[i].FeederFixes);
        }
    }

    static void AssertSlotDtoEquals(SlotDto expected, SlotDto actual)
    {
        actual.Id.ShouldBe(expected.Id);
        actual.StartTime.ShouldBe(expected.StartTime);
        actual.EndTime.ShouldBe(expected.EndTime);
        actual.RunwayIdentifiers.ShouldBe(expected.RunwayIdentifiers);
    }

    static void AssertPeerInfoEquals(PeerInfo expected, PeerInfo actual)
    {
        actual.Callsign.ShouldBe(expected.Callsign);
        actual.Role.ShouldBe(expected.Role);
    }

    static void AssertCoordinateEquals(Coordinate expected, Coordinate actual)
    {
        actual.Latitude.ShouldBe(expected.Latitude);
        actual.Longitude.ShouldBe(expected.Longitude);
    }

    static void AssertFixEstimateEquals(FixEstimate expected, FixEstimate actual)
    {
        actual.FixIdentifier.ShouldBe(expected.FixIdentifier);
        actual.Estimate.ShouldBe(expected.Estimate);
        actual.ActualTimeOver.ShouldBe(expected.ActualTimeOver);
    }

    static void AssertFlightPositionEquals(FlightPosition expected, FlightPosition actual)
    {
        actual.Coordinate.Latitude.ShouldBe(expected.Coordinate.Latitude);
        actual.Coordinate.Longitude.ShouldBe(expected.Coordinate.Longitude);
        actual.Altitude.ShouldBe(expected.Altitude);
        actual.VerticalTrack.ShouldBe(expected.VerticalTrack);
        actual.GroundSpeed.ShouldBe(expected.GroundSpeed);
        actual.IsOnGround.ShouldBe(expected.IsOnGround);
    }
}
