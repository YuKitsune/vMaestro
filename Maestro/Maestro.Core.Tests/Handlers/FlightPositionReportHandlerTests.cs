using Maestro.Core.Dtos;
using Maestro.Core.Dtos.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using MediatR;
using NSubstitute;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class FlightPositionReportHandlerTests
{
    [Fact]
    public async Task WhenAFlightIsNotActivated_PositionReportsAreIgnored()
    {
        var mediator = Substitute.For<IMediator>();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        
        var flight = new Flight
        {
            Callsign = "QFA123",
            AircraftType = "B738",
            WakeCategory = WakeCategory.Medium,
            OriginIdentifier = "YMML",
            DestinationIdentifier = "YSSY",
            FeederFixIdentifier = "RIVET"
        };

        var sequence = CreateSequence(mediator, clock);
        await sequence.Add(flight, CancellationToken.None);
        
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        sequenceProvider.TryGetSequence(Arg.Is("YSSY")).Returns(sequence);
        
        // Act
        var notification = new FlightPositionReport(
            "QFA123",
            "YSSY",
            new FlightPosition(1, 1, 35_000, VerticalTrack.Maintaining, 450),
            [new FixDto("RIVET", new Coordinate(0, 0), clock.UtcNow().AddHours(1))]);

        var handler = new FlightPositionReportHandler(sequenceProvider, mediator, clock);
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert
        flight.PositionUpdated.ShouldBeNull();
        flight.LastKnownPosition.ShouldBeNull();
    }

    [Fact]
    public async Task WhenAFlightIsFarAway_PositionReportsAreProcessedLessFrequently()
    {
        var mediator = Substitute.For<IMediator>();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        
        var flight = new Flight
        {
            Callsign = "QFA123",
            AircraftType = "B738",
            WakeCategory = WakeCategory.Medium,
            OriginIdentifier = "YMML",
            DestinationIdentifier = "YSSY",
            FeederFixIdentifier = "RIVET"
        };
        
        flight.Activate(clock);

        var sequence = CreateSequence(mediator, clock);
        await sequence.Add(flight, CancellationToken.None);
        
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        sequenceProvider.TryGetSequence(Arg.Is("YSSY")).Returns(sequence);
        
        // Act
        var notification = new FlightPositionReport(
            "QFA123",
            "YSSY",
            new FlightPosition(50, 50, 35_000, VerticalTrack.Maintaining, 450),
            [new FixDto("RIVET", new Coordinate(0, 0), clock.UtcNow().AddHours(3))]);

        var handler = new FlightPositionReportHandler(sequenceProvider, mediator, clock);
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert
        var firstPositionUpdateTime = flight.PositionUpdated.ShouldNotBeNull();
        var firstPosition = flight.LastKnownPosition.ShouldNotBeNull();
        
        firstPositionUpdateTime.ShouldBe(clock.UtcNow());
        firstPosition.Latitude.ShouldBe(50);
        firstPosition.Longitude.ShouldBe(50);
        firstPosition.Altitude.ShouldBe(35_000);
        
        // Advance 45 seconds, position report should be ignored because it's too far away
        clock.SetTime(clock.UtcNow().AddSeconds(45));
        notification = notification with
        {
            Position = new FlightPosition(49, 49, 25_000, VerticalTrack.Maintaining, 450),
        };
        
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert: No changes
        flight.PositionUpdated.ShouldBe(firstPositionUpdateTime);
        flight.LastKnownPosition.Value.Latitude.ShouldBe(firstPosition.Latitude);
        flight.LastKnownPosition.Value.Longitude.ShouldBe(firstPosition.Longitude);
        flight.LastKnownPosition.Value.Altitude.ShouldBe(firstPosition.Altitude);
    }

    [Fact]
    public async Task WhenAFlightIsClose_PositionReportsAreProcessedMoreFrequently()
    {
        var mediator = Substitute.For<IMediator>();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        
        var flight = new Flight
        {
            Callsign = "QFA123",
            AircraftType = "B738",
            WakeCategory = WakeCategory.Medium,
            OriginIdentifier = "YMML",
            DestinationIdentifier = "YSSY",
            FeederFixIdentifier = "RIVET"
        };
        
        flight.Activate(clock);

        var sequence = CreateSequence(mediator, clock);
        await sequence.Add(flight, CancellationToken.None);
        
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        sequenceProvider.TryGetSequence(Arg.Is("YSSY")).Returns(sequence);
        
        // Act
        var notification = new FlightPositionReport(
            "QFA123",
            "YSSY",
            new FlightPosition(2, 2, 35_000, VerticalTrack.Maintaining, 450),
            [new FixDto("RIVET", new Coordinate(0, 0), clock.UtcNow().AddHours(3))]);

        var handler = new FlightPositionReportHandler(sequenceProvider, mediator, clock);
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert
        var firstPositionUpdateTime = flight.PositionUpdated.ShouldNotBeNull();
        var firstPosition = flight.LastKnownPosition.ShouldNotBeNull();
        
        firstPositionUpdateTime.ShouldBe(clock.UtcNow());
        firstPosition.Latitude.ShouldBe(2);
        firstPosition.Longitude.ShouldBe(2);
        firstPosition.Altitude.ShouldBe(35_000);
        
        // Advance 45 seconds, new position should be recorded
        clock.SetTime(clock.UtcNow().AddSeconds(45));
        notification = notification with
        {
            Position = new FlightPosition(1, 1, 25_000, VerticalTrack.Maintaining, 450),
        };
        
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert: No changes
        
        flight.PositionUpdated.ShouldBe(clock.UtcNow());
        flight.LastKnownPosition.Value.Latitude.ShouldBe(1);
        flight.LastKnownPosition.Value.Longitude.ShouldBe(1);
        flight.LastKnownPosition.Value.Altitude.ShouldBe(25_000);
    }

    Sequence CreateSequence(IMediator mediator, IClock clock)
    {
        return new Sequence(
            new AirportConfigurationDto(
                "YSSY",
                [
                    new RunwayConfigurationDto("34L", 180),
                    new RunwayConfigurationDto("34R", 180)
                ],
                [
                    new RunwayModeConfigurationDto(
                        "34IVA",
                        TimeSpan.FromSeconds(30),
                        [
                            new RunwayConfigurationDto("34L", 180),
                            new RunwayConfigurationDto("34R", 180)
                        ],
                        new Dictionary<string, TimeSpan>
                        {
                            {"34L", TimeSpan.FromSeconds(180)},
                            {"34R", TimeSpan.FromSeconds(180)}
                        },
                        [])
                ],
                [new ViewConfigurationDto("BIK", null, null, LadderReferenceTime.FeederFixTime)],
                ["RIVET"]),
            new FixedSeparationProvider(TimeSpan.FromMinutes(2)),
            new FixedPerformanceLookup(),
            mediator,
            clock);
    }
}