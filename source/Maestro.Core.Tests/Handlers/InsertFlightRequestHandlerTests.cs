using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Handlers;
using Maestro.Core.Hosting;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class InsertFlightRequestHandlerTests(
    AirportConfigurationFixture airportConfigurationFixture,
    ClockFixture clockFixture,
    PerformanceLookupFixture performanceLookupFixture)
{
    [Fact]
    public async Task CallsignIsNormalizedAndTruncated()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "  qfa1withmorethan12chars  ",
            "B738",
            new ExactInsertionOptions(now.AddMinutes(10), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var insertedFlight = sequence.Flights.Single();
        insertedFlight.Callsign.ShouldBe("QFA1WITHMORE", "Callsign should be uppercased, trimmed, and truncated to 12 characters");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task WhenNoCallsignIsProvided_DummyCallsignIsUsed(string? callsign)
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            callsign,
            "B738",
            new ExactInsertionOptions(now.AddMinutes(10), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var insertedFlight = sequence.Flights.Single();
        insertedFlight.Callsign.ShouldBe("****01*", "Dummy callsign should be generated in the format ****NN*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task WhenNoAircraftTypeIsProvided_DefaultIsUsed(string? aircraftType)
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            aircraftType,
            new ExactInsertionOptions(now.AddMinutes(10), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var insertedFlight = sequence.Flights.Single();
        insertedFlight.AircraftType.ShouldBe("B738", "Default aircraft type should be B738");
    }

    [Theory]
    [InlineData(State.Unstable)]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public async Task WhenInserting_AheadOfANonFrozenFlight_NewFlightIsSequencedAheadOfExistingFlight(State leaderState)
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(leaderState)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(flight1))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA2",
            "B738",
            new ExactInsertionOptions(now.AddMinutes(8), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var flight2 = sequence.Flights[0];
        flight2.Callsign.ShouldBe("QFA2", "Inserted flight should be first in sequence");
        flight2.LandingTime.ShouldBe(now.AddMinutes(8), "Inserted flight should be scheduled at target time");

        sequence.NumberInSequence(flight1).ShouldBe(2, "Existing flight should be second in sequence");
        flight1.LandingTime.ShouldBe(now.AddMinutes(11), "Existing flight should be delayed by acceptance rate (3 mins)");
    }

    [Fact]
    public async Task WhenInserting_DummyFlight_DefaultDummyStateIsSet()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new ExactInsertionOptions(now.AddMinutes(10), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var insertedFlight = sequence.Flights.Single();
        insertedFlight.State.ShouldBe(State.Frozen, "Dummy flight should be Frozen when inserted");
    }

    [Fact]
    public async Task WhenInserting_PendingFlight_DefaultPendingStateIsSet()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var pendingFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new ExactInsertionOptions(now.AddMinutes(10), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.State.ShouldBe(State.Stable, "Pending flight should be Stable when inserted");
        sequence.Flights.ShouldContain(pendingFlight);
    }

    [Fact]
    public async Task InsertExact_DummyFlight_TargetTimeIsSet()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var targetTime = now.AddMinutes(10);

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new ExactInsertionOptions(targetTime, ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var insertedFlight = sequence.Flights.Single();
        insertedFlight.TargetLandingTime.ShouldBe(targetTime, "Target landing time should be set");
    }

    [Fact]
    public async Task InsertExact_PendingFlight_TargetTimeIsSet()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var targetTime = now.AddMinutes(10);

        var pendingFlight = new FlightBuilder("QFA1")
            .WithAircraftType("B738")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new ExactInsertionOptions(targetTime, ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.TargetLandingTime.ShouldBe(targetTime, "Target landing time should be set");
    }

    [Fact]
    public async Task InsertExact_RelevantRunwayIsAssigned()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new ExactInsertionOptions(now.AddMinutes(10), ["34R"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var insertedFlight = sequence.Flights.Single();
        insertedFlight.AssignedRunwayIdentifier.ShouldBe("34R", "Runway should be assigned to one of the requested runways");
    }

    [Fact]
    public async Task InsertExact_PendingFlightExists_PendingFlightInserted()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var pendingFlight = new FlightBuilder("QFA1")
            .WithAircraftType("B738")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new ExactInsertionOptions(now.AddMinutes(10), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        instance.Session.PendingFlights.ShouldBeEmpty("Pending flight should be removed from pending list");
        sequence.Flights.ShouldContain(pendingFlight, "Pending flight should be in the sequence");
    }

    [Fact]
    public async Task InsertExact_NoMatchingFlightsExists_DummyFlightIsInserted()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new ExactInsertionOptions(now.AddMinutes(10), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var insertedFlight = sequence.Flights.Single();
        insertedFlight.Callsign.ShouldBe("QFA1", "Dummy flight should have the requested callsign");
        insertedFlight.AircraftType.ShouldBe("B738", "Dummy flight should have the requested aircraft type");
        insertedFlight.State.ShouldBe(State.Frozen, "Dummy flight should be Frozen");
    }

    [Fact]
    public async Task InsertExact_PendingFlightCoupled_SystemEstimatesRemain()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var originalEstimate = now.AddMinutes(12);
        var targetTime = now.AddMinutes(10);

        var pendingFlight = new FlightBuilder("QFA1")
            .WithAircraftType("B738")
            .WithLandingEstimate(originalEstimate)
            .WithState(State.Unstable)
            .WithPosition(new FlightPosition(
                new Coordinate(0, 0),
                5000,
                VerticalTrack.Descending,
                250,
                false))
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new ExactInsertionOptions(targetTime, ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.LandingEstimate.ShouldBe(originalEstimate, "Landing estimate should remain unchanged for coupled flight");
        pendingFlight.TargetLandingTime.ShouldBe(targetTime, "Target landing time should be set");
    }

    [Fact]
    public async Task InsertExact_PendingFlightUncoupled_LandingEstimateIsTargetTime()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var targetTime = now.AddMinutes(10);

        var pendingFlight = new FlightBuilder("QFA1")
            .WithAircraftType("B738")
            .WithLandingEstimate(now.AddMinutes(12))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new ExactInsertionOptions(targetTime, ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.LandingEstimate.ShouldBe(targetTime, "Landing estimate should match target time for uncoupled flight");
        pendingFlight.TargetLandingTime.ShouldBe(targetTime, "Target landing time should be set");
    }

    [Fact]
    public async Task InsertExact_PositionedByTargetTime()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(flight1, flight2))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA3",
            "B738",
            new ExactInsertionOptions(now.AddMinutes(15), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA1 should remain in position 1");

        var flight3 = sequence.Flights[1];
        flight3.Callsign.ShouldBe("QFA3", "Inserted flight should be in position 2");
        sequence.NumberInSequence(flight3).ShouldBe(2, "QFA3 should be positioned between QFA1 and QFA2");

        sequence.NumberInSequence(flight2).ShouldBe(3, "QFA2 should be in position 3");
    }

    [Fact]
    public async Task InsertExact_SequencedByTargetTime()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(flight1, flight2))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA3",
            "B738",
            new ExactInsertionOptions(now.AddMinutes(11), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.LandingTime.ShouldBe(now.AddMinutes(10), "QFA1 should remain at T10");

        var flight3 = sequence.Flights[1];
        flight3.Callsign.ShouldBe("QFA3");
        flight3.LandingTime.ShouldBe(now.AddMinutes(13), "QFA3 should be scheduled at T13 (+3 mins separation after QFA1)");

        flight2.LandingTime.ShouldBe(now.AddMinutes(16), "QFA2 should be scheduled at T16 (+3 mins separation after QFA3)");
    }

    [Theory]
    [InlineData(State.Frozen)]
    [InlineData(State.Landed)]
    public async Task InsertExact_TargetTimeOccupiedByFrozenFlight_Throws(State leaderState)
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(leaderState)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(flight1))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA2",
            "B738",
            new ExactInsertionOptions(now.AddMinutes(8), ["34L"]));

        // Act / Assert
        await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task InsertExact_TargetTimeOccupiedBySlot_Throws()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var (instanceManager, instance, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        // Create a slot from T05 to T15
        instance.Session.Sequence.CreateSlot(
            now.AddMinutes(5),
            now.AddMinutes(15),
            ["34L"]);

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new ExactInsertionOptions(now.AddMinutes(10), ["34L"]));

        // Act / Assert
        await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task InsertExact_TargetTimeOccupiedByRunwayChange_Throws()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var (instanceManager, instance, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        // Schedule a runway change from T10 to T15
        instance.Session.Sequence.ChangeRunwayMode(
            new RunwayMode(airportConfigurationFixture.Instance.RunwayModes[1]),
            now.AddMinutes(10),
            now.AddMinutes(15));

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new ExactInsertionOptions(now.AddMinutes(13), ["34L"]));

        // Act / Assert
        await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task InsertRelative_BeforeFrozenFlight_Throws()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var frozenFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(State.Frozen)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(frozenFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA2",
            "B738",
            new RelativeInsertionOptions("QFA1", RelativePosition.Before));

        // Act / Assert
        await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task InsertRelative_AfterFrozenFlight_DoesNotThrow()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var frozenFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(State.Frozen)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(frozenFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA2",
            "B738",
            new RelativeInsertionOptions("QFA1", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(frozenFlight).ShouldBe(1, "Frozen flight should remain first in sequence");
        frozenFlight.LandingTime.ShouldBe(now.AddMinutes(10), "Frozen flight landing time should remain unchanged");

        var insertedFlight = sequence.Flights[1];
        insertedFlight.Callsign.ShouldBe("QFA2", "Inserted flight should be second in sequence");
        insertedFlight.LandingTime.ShouldBe(
            frozenFlight.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "Inserted flight should be scheduled after frozen flight with separation");
    }

    [Fact]
    public async Task InsertRelative_AfterFrozenFlight_WithoutSufficientSpace_Throws()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var frozenFlight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(State.Frozen)
            .WithRunway("34L")
            .Build();

        var frozenFlight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithState(State.Frozen)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(frozenFlight1, frozenFlight2))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA3",
            "B738",
            new RelativeInsertionOptions("QFA1", RelativePosition.After));

        // Act / Assert
        await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task InsertRelative_AfterFrozenFlight_WithSufficientSpace_SequencesBetweenFrozenFlights()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var frozenFlight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(State.Frozen)
            .WithRunway("34L")
            .Build();

        var frozenFlight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(16))
            .WithLandingTime(now.AddMinutes(16))
            .WithState(State.Frozen)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(frozenFlight1, frozenFlight2))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA3",
            "B738",
            new RelativeInsertionOptions("QFA1", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(frozenFlight1).ShouldBe(1, "First frozen flight should remain first in sequence");
        frozenFlight1.LandingTime.ShouldBe(now.AddMinutes(10), "First frozen flight landing time should remain unchanged");

        var insertedFlight = sequence.Flights[1];
        insertedFlight.Callsign.ShouldBe("QFA3", "Inserted flight should be second in sequence");
        insertedFlight.LandingTime.ShouldBe(
            frozenFlight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "Inserted flight should be scheduled after first frozen flight with separation");

        sequence.NumberInSequence(frozenFlight2).ShouldBe(3, "Second frozen flight should become third in sequence");
        frozenFlight2.LandingTime.ShouldBe(now.AddMinutes(16), "Second frozen flight landing time should remain unchanged");
    }

    [Fact]
    public async Task InsertRelative_BeforeAnotherFlight_SequencedBeforeReferenceFlight()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var stableFlight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var stableFlight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(stableFlight1, stableFlight2))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA3",
            "B738",
            new RelativeInsertionOptions("QFA1", RelativePosition.Before));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var insertedFlight = sequence.Flights[0];
        insertedFlight.Callsign.ShouldBe("QFA3", "Inserted flight should be first in sequence");
        insertedFlight.TargetLandingTime.ShouldBe(now.AddMinutes(10), "Inserted flight's target time should be the original landing time of the reference flight");
        insertedFlight.LandingTime.ShouldBe(now.AddMinutes(10), "Inserted flight's landing time should be the original landing time of the reference flight");

        sequence.NumberInSequence(stableFlight1).ShouldBe(2, "First stable flight should be second in sequence");
        stableFlight1.LandingTime.ShouldBe(
            insertedFlight.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "First stable flight should be delayed for separation with inserted flight");

        sequence.NumberInSequence(stableFlight2).ShouldBe(3, "Second stable flight should be third in sequence");
        stableFlight2.LandingTime.ShouldBe(
            stableFlight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "Second stable flight should be delayed for separation with first stable flight");
    }

    [Fact]
    public async Task InsertRelative_AfterAnotherFlight_SequencedAfterReferenceFlight()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var stableFlight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var stableFlight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(stableFlight1, stableFlight2))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA3",
            "B738",
            new RelativeInsertionOptions("QFA1", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(stableFlight1).ShouldBe(1, "First stable flight should remain first in sequence");
        stableFlight1.LandingTime.ShouldBe(now.AddMinutes(10), "First stable flight's landing time should remain unchanged");

        var insertedFlight = sequence.Flights[1];
        insertedFlight.Callsign.ShouldBe("QFA3", "Inserted flight should be second in sequence");
        insertedFlight.TargetLandingTime.ShouldBe(
            stableFlight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "Inserted flight's target time should be reference flight's landing time + acceptance rate");
        insertedFlight.LandingTime.ShouldBe(
            insertedFlight.TargetLandingTime!.Value,
            "Inserted flight's landing time should match target time");

        sequence.NumberInSequence(stableFlight2).ShouldBe(3, "Second stable flight should be third in sequence");
        stableFlight2.LandingTime.ShouldBe(
            insertedFlight.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "Second stable flight should be delayed for separation with inserted flight");
    }

    [Fact]
    public async Task InsertRelative_PendingFlightExists_PendingFlightInserted()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var stableFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var pendingFlight = new FlightBuilder("QFA2")
            .WithAircraftType("B738")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(stableFlight))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA2",
            "B738",
            new RelativeInsertionOptions("QFA1", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        instance.Session.PendingFlights.ShouldBeEmpty("Pending flight should be removed from pending list");
        sequence.Flights.ShouldContain(pendingFlight, "Pending flight should be in the sequence");
        pendingFlight.LandingTime.ShouldBe(
            stableFlight.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "Pending flight should be scheduled at target time");
        pendingFlight.State.ShouldBe(State.Stable, "Pending flight should have default state (Stable)");
    }

    [Fact]
    public async Task InsertRelative_DummyFlight()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var stableFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(stableFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA2",
            "B738",
            new RelativeInsertionOptions("QFA1", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var dummyFlight = sequence.Flights[1];
        dummyFlight.Callsign.ShouldBe("QFA2", "Dummy flight should be added to the sequence");
        dummyFlight.State.ShouldBe(State.Frozen, "Dummy flight should have default state (Frozen)");
        dummyFlight.TargetLandingTime.ShouldBe(
            stableFlight.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "Dummy flight target time should be reference flight's landing time + acceptance rate");
        dummyFlight.LandingEstimate.ShouldBe(
            dummyFlight.TargetLandingTime!.Value,
            "Dummy flight landing estimate should match target time");
        dummyFlight.LandingTime.ShouldBe(
            dummyFlight.TargetLandingTime!.Value,
            "Dummy flight landing time should match target time");
    }

    [Fact]
    public async Task InsertRelative_PendingFlightCoupled_SystemEstimatesRemain()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var originalEstimate = now.AddMinutes(20);

        var stableFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var pendingFlight = new FlightBuilder("QFA2")
            .WithAircraftType("B738")
            .WithLandingEstimate(originalEstimate)
            .WithState(State.Unstable)
            .WithPosition(new FlightPosition(
                new Coordinate(0, 0),
                5000,
                VerticalTrack.Descending,
                250,
                false))
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(stableFlight))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA2",
            "B738",
            new RelativeInsertionOptions("QFA1", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.TargetLandingTime.ShouldBe(
            stableFlight.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "Pending flight target time should be reference flight's landing time + acceptance rate");
        pendingFlight.LandingEstimate.ShouldBe(originalEstimate, "Pending flight landing estimate should remain unchanged for coupled flight");
        pendingFlight.LandingTime.ShouldBe(
            pendingFlight.TargetLandingTime!.Value,
            "Pending flight landing time should match target time");
    }

    [Fact]
    public async Task InsertRelative_PendingFlightUncoupled_LandingEstimateIsTargetTime()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var stableFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var pendingFlight = new FlightBuilder("QFA2")
            .WithAircraftType("B738")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(stableFlight))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA2",
            "B738",
            new RelativeInsertionOptions("QFA1", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.TargetLandingTime.ShouldBe(
            stableFlight.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "Pending flight target time should be reference flight's landing time + acceptance rate");
        pendingFlight.LandingEstimate.ShouldBe(
            pendingFlight.TargetLandingTime!.Value,
            "Pending flight landing estimate should match target time for uncoupled flight");
        pendingFlight.LandingTime.ShouldBe(
            pendingFlight.TargetLandingTime!.Value,
            "Pending flight landing time should match target time");
    }

    [Fact]
    public async Task RelaysToMaster()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();

        var handler = GetRequestHandler(instanceManager, slaveConnectionManager, mediator);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new ExactInsertionOptions(now.AddMinutes(10), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");
        sequence.Flights.ShouldBeEmpty("Flight should not be inserted locally when relaying to master");
    }

    InsertFlightRequestHandler GetRequestHandler(
        IMaestroInstanceManager? instanceManager = null,
        IMaestroConnectionManager? connectionManager = null,
        IMediator? mediator = null)
    {
        var airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        airportConfigurationProvider.GetAirportConfigurations()
            .Returns([airportConfigurationFixture.Instance]);

        var arrivalLookup = Substitute.For<IArrivalLookup>();

        return new InsertFlightRequestHandler(
            instanceManager ?? Substitute.For<IMaestroInstanceManager>(),
            connectionManager ?? new MockLocalConnectionManager(),
            performanceLookupFixture.Instance,
            airportConfigurationProvider,
            arrivalLookup,
            clockFixture.Instance,
            mediator ?? Substitute.For<IMediator>(),
            Substitute.For<ILogger>());
    }
}
