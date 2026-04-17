using Maestro.Contracts.Connectivity;
using Maestro.Contracts.Coordination;
using Maestro.Contracts.Flights;
using Maestro.Contracts.Runway;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;
using Maestro.Contracts.Slots;
using static Maestro.Contracts.Tests.SnapshotTestHelper;

namespace Maestro.Contracts.Tests;

public static class TestBuilders
{
    // Shared types
    public static Coordinate CreateCoordinate() =>
        new(-33.9461, 151.1772);

    public static FixEstimate CreateFixEstimate() =>
        new("RIVET", FixedTime);

    public static FlightPosition CreateFlightPosition() =>
        new(CreateCoordinate(), 15000, VerticalTrack.Descending, 250.5, false);

    // Connectivity
    public static PeerInfo CreatePeerInfo() =>
        new("SY_APP", Role.Approach);

    public static ServerResponse CreateServerResponse() =>
        ServerResponse.CreateFailure("Test error message");

    public static RequestEnvelope CreateRequestEnvelope() =>
        new()
        {
            OriginatingCallsign = "SY_APP",
            OriginatingConnectionId = "conn-123",
            OriginatingRole = Role.Approach,
            Request = CreateChangeRunwayRequest()
        };

    public static RelayRequest CreateRelayRequest() =>
        new()
        {
            Envelope = CreateRequestEnvelope(),
            ActionKey = "ChangeRunway"
        };

    public static InitializeConnectionRequest CreateInitializeConnectionRequest() =>
        new();

    public static InitializeConnectionResponse CreateInitializeConnectionResponse() =>
        new(
            "conn-123",
            "PROD",
            "YSSY",
            true,
            CreateSessionDto(),
            [CreatePeerInfo()]);

    public static OwnershipGrantedNotification CreateOwnershipGrantedNotification() =>
        new("YSSY");

    public static OwnershipRevokedNotification CreateOwnershipRevokedNotification() =>
        new("YSSY");

    public static PeerConnectedNotification CreatePeerConnectedNotification() =>
        new("YSSY", "SY_APP", Role.Approach);

    public static PeerDisconnectedNotification CreatePeerDisconnectedNotification() =>
        new("YSSY", "SY_APP");

    // Coordination
    public static CoordinationDestination CreateCoordinationDestinationBroadcast() =>
        new CoordinationDestination.Broadcast();

    public static CoordinationDestination CreateCoordinationDestinationController() =>
        new CoordinationDestination.Controller("ENR_W");

    public static SendCoordinationMessageRequest CreateSendCoordinationMessageRequest() =>
        new("YSSY", FixedTime, "Request delay for QFA123", CreateCoordinationDestinationController());

    public static CoordinationMessageReceivedNotification CreateCoordinationMessageReceivedNotification() =>
        new("YSSY", FixedTime, "SY_APP", "Delay approved", CreateCoordinationDestinationBroadcast());

    // Runway
    public static RunwayDto CreateRunwayDto() =>
        new("34L", "ILS", 90, ["RIVET", "BOREE"]);

    public static RunwayModeDto CreateRunwayModeDto() =>
        new("SODPROPS",
            [
                new RunwayDto("34L", "ILS", 90, ["RIVET", "BOREE"]),
                new RunwayDto("34R", "VISUAL", 90, ["MARUB", "SOSIJ"])
            ],
            120,
            180);

    public static ChangeRunwayModeRequest CreateChangeRunwayModeRequest() =>
        new("YSSY", CreateRunwayModeDto(), FixedTime.AddHours(1), FixedTime.AddHours(1).AddMinutes(5));

    public static CancelRunwayModeChangeRequest CreateCancelRunwayModeChangeRequest() =>
        new("YSSY");

    // Slots
    public static SlotDto CreateSlotDto() =>
        new(Guid.Parse("12345678-1234-1234-1234-123456789012"),
            FixedTime,
            FixedTime.AddMinutes(30),
            ["34L", "34R"]);

    public static CreateSlotRequest CreateCreateSlotRequest() =>
        new("YSSY", FixedTime, FixedTime.AddMinutes(30), ["34L", "34R"]);

    public static ModifySlotRequest CreateModifySlotRequest() =>
        new("YSSY", Guid.Parse("12345678-1234-1234-1234-123456789012"), FixedTime, FixedTime.AddMinutes(45));

    public static DeleteSlotRequest CreateDeleteSlotRequest() =>
        new("YSSY", Guid.Parse("12345678-1234-1234-1234-123456789012"));

    // Flights
    public static FlightDataRecord CreateFlightDataRecord() =>
        new("QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            FixedTime.AddHours(-2),
            CreateFlightPosition(),
            [CreateFixEstimate()],
            FixedTime);

    public static PendingFlightDto CreatePendingFlightDto() =>
        new()
        {
            Callsign = "QFA123",
            AircraftType = "B738",
            OriginIdentifier = "YMML",
            DestinationIdentifier = "YSSY",
            IsFromDepartureAirport = false,
            IsHighPriority = false
        };

    public static FlightDto CreateFlightDto() =>
        new()
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
            AssignedRunwayIdentifier = "34L",
            NumberToLandOnRunway = 3,
            ApproachType = "ILS",
            InitialLandingEstimate = FixedTime.AddMinutes(5),
            LandingEstimate = FixedTime.AddMinutes(7),
            TargetLandingTime = FixedTime.AddMinutes(8),
            LandingTime = FixedTime.AddMinutes(8),
            RequiredEnrouteDelay = TimeSpan.FromMinutes(2),
            RemainingEnrouteDelay = TimeSpan.FromMinutes(1),
            HighSpeed = false,
            LastSeen = FixedTime,
            Position = CreateFlightPosition(),
            IsManuallyInserted = false,
            TerminalNormalTimeToGo = TimeSpan.FromMinutes(18),
            RequiredControlAction = ControlAction.NoDelay
        };

    public static RelativeInsertionOptions CreateRelativeInsertionOptions() =>
        new("QFA456", RelativePosition.Before);

    public static ExactInsertionOptions CreateExactInsertionOptions() =>
        new(FixedTime.AddMinutes(30), ["34L", "34R"]);

    public static DepartureInsertionOptions CreateDepartureInsertionOptions() =>
        new("YMML", FixedTime.AddHours(-2));

    public static InsertFlightRequest CreateInsertFlightRequest() =>
        new("YSSY", "QFA123", "B738", CreateRelativeInsertionOptions());

    public static ChangeRunwayRequest CreateChangeRunwayRequest() =>
        new("YSSY", "QFA123", "34R");

    public static ChangeApproachTypeRequest CreateChangeApproachTypeRequest() =>
        new("YSSY", "QFA123", "VISUAL");

    public static ChangeFeederFixEstimateRequest CreateChangeFeederFixEstimateRequest() =>
        new("YSSY", "QFA123", FixedTime.AddMinutes(5));

    public static MoveFlightRequest CreateMoveFlightRequest() =>
        new("YSSY", "QFA123", "34L", FixedTime.AddMinutes(15));

    public static SwapFlightsRequest CreateSwapFlightsRequest() =>
        new("YSSY", "QFA123", "QFA456");

    public static RemoveRequest CreateRemoveRequest() =>
        new("YSSY", "QFA123");

    public static DesequenceRequest CreateDesequenceRequest() =>
        new("YSSY", "QFA123");

    public static MakePendingRequest CreateMakePendingRequest() =>
        new("YSSY", "QFA123");

    public static MakeStableRequest CreateMakeStableRequest() =>
        new("YSSY", "QFA123");

    public static RecomputeRequest CreateRecomputeRequest() =>
        new("YSSY", "QFA123");

    public static ResumeSequencingRequest CreateResumeSequencingRequest() =>
        new("YSSY", "QFA123");

    public static ManualDelayRequest CreateManualDelayRequest() =>
        new("YSSY", "QFA123", 15);

    public static FlightUpdatedNotification CreateFlightUpdatedNotification() =>
        new("QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            FixedTime.AddHours(-2),
            TimeSpan.FromHours(1),
            CreateFlightPosition(),
            [CreateFixEstimate()]);

    // Sessions
    public static WindDto CreateWindDto() =>
        new(270, 15);

    public static NoDeviationDto CreateNoDeviationDto() => new();

    public static AchievedRateDto CreateAchievedRateDto() =>
        new(TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(-5));

    public static RunwayLandingTimesDto CreateRunwayLandingTimesDto() =>
        new(
            "34L",
            [
                FixedTime.AddMinutes(-10),
                FixedTime.AddMinutes(-8),
                FixedTime.AddMinutes(-6)
            ],
            CreateAchievedRateDto());

    public static LandingStatisticsDto CreateLandingStatisticsDto() =>
        new()
        {
            RunwayLandingTimes = new Dictionary<string, RunwayLandingTimesDto>
            {
                {
                    "34L",
                    CreateRunwayLandingTimesDto()
                },
                {
                    "34R",
                    new RunwayLandingTimesDto(
                        "34R",
                        [FixedTime.AddMinutes(-5)],
                        CreateNoDeviationDto())
                }
            }
        };

    public static SequenceDto CreateSequenceDto() =>
        new()
        {
            CurrentRunwayMode = CreateRunwayModeDto(),
            NextRunwayMode = null,
            LastLandingTimeForCurrentMode = FixedTime.AddHours(2),
            FirstLandingTimeForNextMode = FixedTime.AddHours(3),
            Flights = [CreateFlightDto()],
            Slots = [CreateSlotDto()],
            SurfaceWind = CreateWindDto(),
            UpperWind = new WindDto(280, 25),
            ManualWind = false
        };

    public static SessionDto CreateSessionDto() =>
        new()
        {
            AirportIdentifier = "YSSY",
            PendingFlights = [CreatePendingFlightDto()],
            DeSequencedFlights = [],
            Sequence = CreateSequenceDto(),
            DummyCounter = 42,
            LandingStatistics = CreateLandingStatisticsDto(),
            FlightDataRecords = [CreateFlightDataRecord()]
        };

    public static RestoreSessionRequest CreateRestoreSessionRequest() =>
        new("YSSY", CreateSessionDto());

    public static SessionUpdatedNotification CreateSessionUpdatedNotification() =>
        new("YSSY", CreateSessionDto());

    public static ModifyWindRequest CreateModifyWindRequest() =>
        new("YSSY", CreateWindDto(), new WindDto(280, 25), ManualWind: true);
}
