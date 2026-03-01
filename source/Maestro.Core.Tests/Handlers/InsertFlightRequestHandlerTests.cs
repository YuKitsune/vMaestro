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

// TODO: @claude, the test TODOs and stubs in this file are incomplete. Please reference InsertFlightRequestHandler.cs
//  for any test cases. Use your best judgement when determining if a test case is already implemented, or needs to be implemented.

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
    public async Task InsertExact_DummyFlight_FeederFixEstimateIsSet()
    {
        // TODO: @claude, please implement this test case
        // Arrange


        // Act
        // TODO: Insert a dummy flight at a target time

        // Assert
        // TODO: FeederFixEstimate should be TargetTime - Trajectory.TimeToGo
    }

    [Fact]
    public async Task InsertExact_PendingFlight_FeederFixEstimateIsSet()
    {
        // TODO: @claude, please implement this test case
        // Arrange
        // TODO: Create a pending flight

        // Act
        // TODO: Insert the flight at a target time

        // Assert
        // TODO: FeederFixEstimate should be TargetTime - Trajectory.TimeToGo
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
        // TODO: @claude, please change this assertion to ensure the FeederFixEstimate is sourced from the flight plan route,
        //  and the LandingEstimate is the FeederFixEstimate + Trajectory.TimeToGo
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
        // TODO: @claude, please update this test case to ensure the feeder fix times would cause the flights to be
        //  positioned different if they were positioned based on FeederFixEstimate.
        //  You'll need to use two different TTG values to ensure one flight arrives at the FF earlier but lands later.

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
    public async Task InsertExact_PendingFlightWithFeederFix_FeederFixEstimateCalculated()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var targetTime = now.AddMinutes(20);
        var arrivalInterval = TimeSpan.FromMinutes(16); // From RIVET to 34L per fixture
        var expectedFeederFixEstimate = targetTime.Subtract(arrivalInterval);

        var pendingFlight = new FlightBuilder("QFA1")
            .WithAircraftType("B738")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(now.AddMinutes(5))
            .WithLandingEstimate(now.AddMinutes(21))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        var arrivalLookup = Substitute.For<IArrivalLookup>();
        arrivalLookup.GetTrajectory(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string[]>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<AircraftCategory>())
            .Returns(new Trajectory(arrivalInterval));

        var handler = GetRequestHandler(instanceManager, arrivalLookup: arrivalLookup);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new ExactInsertionOptions(targetTime, ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.FeederFixEstimate.ShouldBe(expectedFeederFixEstimate, "Feeder fix estimate should be calculated from target time - arrival interval");
        pendingFlight.LandingEstimate.ShouldBe(targetTime, "Landing estimate should be set to the target time");
    }

    [Fact]
    public async Task InsertExact_SequencedByTargetTime()
    {
        // TODO: Adjust the estimates so we can be sure the flight isn't being sequenced by it's landing estimate

        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(11)) // Keep the estimate forward so we can assert that the flight is inserted behind flight1 based on STA and not ETA
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
            .WithLandingEstimate(now.AddMinutes(11)) // Keep the estimate forward so we can assert that the flight is inserted behind stableFlight1 based on STA and not ETA
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
    public async Task InsertRelative_DummyFlight_FeederFixEstimateIsSet()
    {
        // TODO: @claude, please implement this test case

        // Arrange
        // TODO: Create an existing flight

        // Act
        // TODO: Insert a dummy flight behind the existing flight

        // Assert
        // TODO: FeederFixEstimate should be ExistingFlight.LandingTime - AcceptanceRate - Trajectory.TimeToGo
    }

    [Fact]
    public async Task InsertRelative_PendingFlight_FeederFixEstimateIsSet()
    {
        // TODO: @claude, please implement this test case

        // Arrange
        // TODO: Create an existing flight
        // TODO: Create a pending flight

        // Act
        // TODO: Insert the flight behind the existing flight

        // Assert
        // TODO: FeederFixEstimate should be ExistingFlight.LandingTime - AcceptanceRate - Trajectory.TimeToGo
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
        // TODO: @claude, please change this assertion to ensure the FeederFixEstimate is sourced from the flight plan route,
        //  and the LandingEstimate is the FeederFixEstimate + Trajectory.TimeToGo
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
    public async Task InsertRelative_PendingFlightWithFeederFix_FeederFixEstimateCalculated()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var arrivalInterval = TimeSpan.FromMinutes(22); // From BOREE to 34R per fixture

        var stableFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(State.Stable)
            .WithRunway("34R")
            .Build();

        var targetTime = stableFlight.LandingTime.Add(airportConfigurationFixture.AcceptanceRate); // After QFA1
        var expectedFeederFixEstimate = targetTime.Subtract(arrivalInterval);

        var pendingFlight = new FlightBuilder("QFA2")
            .WithAircraftType("B738")
            .WithFeederFix("BOREE")
            .WithFeederFixEstimate(now.AddMinutes(5))
            .WithLandingEstimate(now.AddMinutes(27))
            .WithState(State.Unstable)
            .WithRunway("34R")
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(stableFlight))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        var arrivalLookup = Substitute.For<IArrivalLookup>();
        arrivalLookup.GetTrajectory(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string[]>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<AircraftCategory>())
            .Returns(new Trajectory(arrivalInterval));

        var handler = GetRequestHandler(instanceManager, arrivalLookup: arrivalLookup);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA2",
            "B738",
            new RelativeInsertionOptions("QFA1", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.FeederFixEstimate.ShouldBe(expectedFeederFixEstimate, "Feeder fix estimate should be calculated from target time - arrival interval");
        pendingFlight.LandingEstimate.ShouldBe(targetTime, "Landing estimate should be set to the target time");
    }

    [Fact]
    public async Task InsertDeparture_PendingFlightExists_DepartureInserted()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var takeoffTime = now.AddMinutes(5);

        var pendingFlight = new FlightBuilder("QFA1")
            .WithAircraftType("B738")
            .WithLandingEstimate(now.AddMinutes(40))
            .WithState(State.Unstable)
            .FromDepartureAirport()
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
            new DepartureInsertionOptions("YSCB", takeoffTime));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        instance.Session.PendingFlights.ShouldBeEmpty("Pending flight should be removed from pending list");
        sequence.Flights.ShouldContain(pendingFlight, "Pending flight should be in the sequence");
        pendingFlight.State.ShouldBe(State.Unstable, "Pending flight should remain Unstable when inserted");
    }

    [Fact]
    public async Task InsertDeparture_DummyFlight()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var takeoffTime = now.AddMinutes(5);

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new DepartureInsertionOptions("YSCB", takeoffTime));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var dummyFlight = sequence.Flights.Single();
        dummyFlight.Callsign.ShouldBe("QFA1", "Dummy flight should have the requested callsign");
        dummyFlight.AircraftType.ShouldBe("B738", "Dummy flight should have the requested aircraft type");
        dummyFlight.State.ShouldBe(State.Frozen, "Dummy flight should be Frozen");
    }

    [Fact]
    public async Task InsertDeparture_LandingEstimateIsCalculated()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var takeoffTime = now.AddMinutes(5);
        var expectedLandingEstimate = takeoffTime.AddMinutes(30); // YSCB to YSSY is 30 mins for jets

        var pendingFlight = new FlightBuilder("QFA1")
            .WithAircraftType("B738")
            .WithLandingEstimate(now.AddMinutes(40))
            .WithState(State.Unstable)
            .FromDepartureAirport()
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
            new DepartureInsertionOptions("YSCB", takeoffTime));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.LandingEstimate.ShouldBe(expectedLandingEstimate, "Landing estimate should be takeoff time + enroute time (30 mins)");
    }

    [Fact]
    public async Task InsertDeparture_PendingFlight_TargetTimeIsNull()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var takeoffTime = now.AddMinutes(5);

        var pendingFlight = new FlightBuilder("QFA1")
            .WithAircraftType("B738")
            .WithLandingEstimate(now.AddMinutes(40))
            .WithState(State.Unstable)
            .FromDepartureAirport()
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
            new DepartureInsertionOptions("YSCB", takeoffTime));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.TargetLandingTime.ShouldBeNull("Target time should be null for pending departure flights");
    }

    [Fact]
    public async Task InsertDeparture_DummyFlight_TargetTimeLandingEstimate()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var takeoffTime = now.AddMinutes(5);
        var expectedLandingEstimate = takeoffTime.AddMinutes(30); // YSCB to YSSY is 30 mins for jets

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new DepartureInsertionOptions("YSCB", takeoffTime));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var dummyFlight = sequence.Flights.Single();
        dummyFlight.TargetLandingTime.ShouldBe(expectedLandingEstimate, "Target time should be set to landing estimate for dummy departure flights");
        dummyFlight.LandingEstimate.ShouldBe(expectedLandingEstimate, "Landing estimate should be calculated from takeoff time + enroute time");
    }

    [Fact]
    public async Task InsertDeparture_PendingFlightCoupled_SystemEstimatesRemain()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var takeoffTime = now.AddMinutes(5);
        var originalEstimate = now.AddMinutes(40);

        var pendingFlight = new FlightBuilder("QFA1")
            .WithAircraftType("B738")
            .WithLandingEstimate(originalEstimate)
            .WithState(State.Unstable)
            .FromDepartureAirport()
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
            new DepartureInsertionOptions("YSCB", takeoffTime));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.LandingEstimate.ShouldBe(originalEstimate, "Landing estimate should remain unchanged for coupled flight");
    }

    [Fact]
    public async Task InsertDeparture_PositionedByCalculatedEstimate()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(flight1, flight2))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        // Takeoff at T-15 means landing at T15 (30 min flight time for jets)
        var takeoffTime = now.AddMinutes(-15);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA3",
            "B738",
            new DepartureInsertionOptions("YSCB", takeoffTime));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(flight1).ShouldBe(1, "First stable flight should remain first in sequence");

        var departureFlight = sequence.Flights[1];
        departureFlight.Callsign.ShouldBe("QFA3", "Departure flight should be second in sequence");
        sequence.NumberInSequence(departureFlight).ShouldBe(2, "Departure flight should be positioned between QFA1 and QFA2");

        sequence.NumberInSequence(flight2).ShouldBe(3, "Second stable flight should become third in sequence");
    }

    [Fact]
    public async Task InsertDeparture_SequencedByCalculatedEstimate()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(flight1, flight2))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        // Takeoff at T-19 means landing estimate at T11 (30 min flight time for jets)
        var takeoffTime = now.AddMinutes(-19);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA3",
            "B738",
            new DepartureInsertionOptions("YSCB", takeoffTime));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.LandingTime.ShouldBe(now.AddMinutes(10), "QFA1 should remain at T10");

        var departureFlight = sequence.Flights[1];
        departureFlight.Callsign.ShouldBe("QFA3");
        departureFlight.LandingTime.ShouldBe(now.AddMinutes(13), "QFA3 should be scheduled at T13 (+3 mins separation after QFA1)");

        flight2.LandingTime.ShouldBe(now.AddMinutes(16), "QFA2 should be scheduled at T16 (+3 mins separation after QFA3)");
    }

    [Fact]
    public async Task InsertDeparture_PendingFlightWithFeederFix_FeederFixEstimateCalculated()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var takeoffTime = now.AddMinutes(5);
        var arrivalInterval = TimeSpan.FromMinutes(16); // From RIVET to 34L per fixture
        var expectedLandingEstimate = takeoffTime.AddMinutes(30); // YSCB to YSSY is 30 mins for jets
        var expectedFeederFixEstimate = expectedLandingEstimate.Subtract(arrivalInterval);

        var pendingFlight = new FlightBuilder("QFA1")
            .WithAircraftType("B738")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(now.AddMinutes(5))
            .WithLandingEstimate(now.AddMinutes(40))
            .WithState(State.Unstable)
            .FromDepartureAirport()
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        var arrivalLookup = Substitute.For<IArrivalLookup>();
        arrivalLookup.GetTrajectory(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string[]>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<AircraftCategory>())
            .Returns(new Trajectory(arrivalInterval));

        var handler = GetRequestHandler(instanceManager, arrivalLookup: arrivalLookup);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new DepartureInsertionOptions("YSCB", takeoffTime));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.LandingEstimate.ShouldBe(expectedLandingEstimate, "Landing estimate should be calculated from takeoff time + enroute time");
        pendingFlight.FeederFixEstimate.ShouldBe(expectedFeederFixEstimate, "Feeder fix estimate should be calculated from landing estimate - arrival interval");
    }

    [Fact]
    public async Task InsertDeparture_SequencedBehindLastSuperStableFlight()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var superStableFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(State.SuperStable)
            .WithRunway("34L")
            .Build();

        var stableFlight = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(superStableFlight, stableFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        // Takeoff at T-25 means landing estimate at T05 (30 min flight time for jets)
        var takeoffTime = now.AddMinutes(-25);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA3",
            "B738",
            new DepartureInsertionOptions("YSCB", takeoffTime));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(superStableFlight).ShouldBe(1, "Super Stable flight should remain first in sequence");
        superStableFlight.LandingTime.ShouldBe(now.AddMinutes(10), "Super Stable flight should remain at T10");

        var departureFlight = sequence.Flights[1];
        departureFlight.Callsign.ShouldBe("QFA3", "Departure should be second in sequence");
        departureFlight.LandingTime.ShouldBe(now.AddMinutes(13), "Departure should be scheduled at T13 (Super Stable + acceptance rate)");

        sequence.NumberInSequence(stableFlight).ShouldBe(3, "Stable flight should be third in sequence");
        stableFlight.LandingTime.ShouldBe(now.AddMinutes(16), "Stable flight should be scheduled at T16 (Departure + acceptance rate)");
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

    [Theory]
    [InlineData(State.Unstable)]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    public async Task InsertExact_FlightAlreadyExistsInSequenceAndNotLanded_Throws(State existingFlightState)
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var existingFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(existingFlightState)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(existingFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new ExactInsertionOptions(now.AddMinutes(15), ["34L"]));

        // Act / Assert
        await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task InsertExact_FlightAlreadyExistsInSequenceAndLanded_RemovesLandedFlightAndInsertsNew()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var landedFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.Subtract(TimeSpan.FromMinutes(5)))
            .WithLandingTime(now.Subtract(TimeSpan.FromMinutes(5)))
            .WithState(State.Landed)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(landedFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new ExactInsertionOptions(now.AddMinutes(15), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.ShouldNotContain(landedFlight, "Landed flight should be removed from sequence");
        var newFlight = sequence.Flights.Single();
        newFlight.Callsign.ShouldBe("QFA1", "New flight should have the same callsign");
        newFlight.State.ShouldBe(State.Frozen, "New dummy flight should be Frozen");
        newFlight.LandingTime.ShouldBe(now.AddMinutes(15), "New flight should be scheduled at the requested time");
    }

    [Theory]
    [InlineData(State.Unstable)]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    public async Task InsertRelative_FlightAlreadyExistsInSequenceAndNotLanded_Throws(State existingFlightState)
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var referenceFlight = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var existingFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(existingFlightState)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(existingFlight, referenceFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new RelativeInsertionOptions("QFA2", RelativePosition.Before));

        // Act / Assert
        await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task InsertRelative_FlightAlreadyExistsInSequenceAndLanded_RemovesLandedFlightAndInsertsNew()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var referenceFlight = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var landedFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.Subtract(TimeSpan.FromMinutes(5)))
            .WithLandingTime(now.Subtract(TimeSpan.FromMinutes(5)))
            .WithState(State.Landed)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(landedFlight, referenceFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new RelativeInsertionOptions("QFA2", RelativePosition.Before));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.ShouldNotContain(landedFlight, "Landed flight should be removed from sequence");
        var newFlight = sequence.Flights.First();
        newFlight.Callsign.ShouldBe("QFA1", "New flight should have the same callsign");
        newFlight.State.ShouldBe(State.Frozen, "New dummy flight should be Frozen");
        newFlight.LandingTime.ShouldBe(now.AddMinutes(20), "New flight should be scheduled at reference flight's landing time");
    }

    [Theory]
    [InlineData(State.Unstable)]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    public async Task InsertDeparture_FlightAlreadyExistsInSequenceAndNotLanded_Throws(State existingFlightState)
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var takeoffTime = now.AddMinutes(5);

        var existingFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(35))
            .WithLandingTime(now.AddMinutes(35))
            .WithState(existingFlightState)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(existingFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new DepartureInsertionOptions("YSCB", takeoffTime));

        // Act / Assert
        await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task InsertDeparture_FlightAlreadyExistsInSequenceAndLanded_RemovesLandedFlightAndInsertsNew()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var takeoffTime = now.AddMinutes(5);
        var expectedLandingEstimate = takeoffTime.AddMinutes(30); // YSCB to YSSY is 30 mins for jets

        var landedFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.Subtract(TimeSpan.FromMinutes(5)))
            .WithLandingTime(now.Subtract(TimeSpan.FromMinutes(5)))
            .WithState(State.Landed)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(landedFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var request = new InsertFlightRequest(
            "YSSY",
            "QFA1",
            "B738",
            new DepartureInsertionOptions("YSCB", takeoffTime));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.ShouldNotContain(landedFlight, "Landed flight should be removed from sequence");
        var newFlight = sequence.Flights.Single();
        newFlight.Callsign.ShouldBe("QFA1", "New flight should have the same callsign");
        newFlight.State.ShouldBe(State.Frozen, "New dummy departure flight should be Frozen");
        newFlight.LandingEstimate.ShouldBe(expectedLandingEstimate, "New flight's landing estimate should be calculated from takeoff time");
    }

    InsertFlightRequestHandler GetRequestHandler(
        IMaestroInstanceManager? instanceManager = null,
        IMaestroConnectionManager? connectionManager = null,
        IMediator? mediator = null,
        IArrivalLookup? arrivalLookup = null)
    {
        var airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        airportConfigurationProvider.GetAirportConfigurations()
            .Returns([airportConfigurationFixture.Instance]);

        return new InsertFlightRequestHandler(
            instanceManager ?? Substitute.For<IMaestroInstanceManager>(),
            connectionManager ?? new MockLocalConnectionManager(),
            performanceLookupFixture.Instance,
            airportConfigurationProvider,
            arrivalLookup ?? Substitute.For<IArrivalLookup>(),
            new MockTrajectoryService(),
            clockFixture.Instance,
            mediator ?? Substitute.For<IMediator>(),
            Substitute.For<ILogger>());
    }
}
