// using Maestro.Core.Configuration;
// using Maestro.Core.Handlers;
// using Maestro.Core.Infrastructure;
// using Maestro.Core.Model;
// using Maestro.Core.Tests.Fixtures;
// using MediatR;
// using NSubstitute;
// using Shouldly;
//
// namespace Maestro.Core.Tests.Handlers;
//
// public class FlightPositionReportHandlerTests(AirportConfigurationFixture airportConfigurationFixture)
// {
//     [Fact]
//     public async Task WhenAFlightIsNotActivated_PositionReportsAreIgnored()
//     {
//         var mediator = Substitute.For<IMediator>();
//         var clock = new FixedClock(DateTimeOffset.UtcNow);
//         var fixProvider = Substitute.For<IFixLookup>();
//         fixProvider.FindFix(Arg.Is("RIVET")).Returns(new Fix("RIVET", new Coordinate(0, 0)));
//         
//         var flight = new Flight
//         {
//             Callsign = "QFA123",
//             AircraftType = "B738",
//             WakeCategory = WakeCategory.Medium,
//             OriginIdentifier = "YMML",
//             DestinationIdentifier = "YSSY",
//             FeederFixIdentifier = "RIVET"
//         };
//
//         var sequence = CreateSequence(mediator, clock);
//         await sequence.Add(flight, CancellationToken.None);
//         
//         var sequenceProvider = Substitute.For<ISequenceProvider>();
//         sequenceProvider.TryGetSequence(Arg.Is("YSSY")).Returns(sequence);
//         
//         // Act
//         var notification = new FlightPositionReport(
//             "QFA123",
//             "YSSY",
//             new FlightPosition(new Coordinate(1, 1), 35_000, VerticalTrack.Maintaining, 450),
//             [new FixEstimate("RIVET", clock.UtcNow().AddHours(1))]);
//
//         var handler = new FlightPositionReportHandler(
//             sequenceProvider,
//             fixProvider,
//             mediator,
//             clock);
//         await handler.Handle(notification, CancellationToken.None);
//         
//         // Assert
//         flight.PositionUpdated.ShouldBeNull();
//         flight.LastKnownPosition.ShouldBeNull();
//     }
//
//     [Fact]
//     public async Task WhenAFlightIsFarAway_PositionReportsAreProcessedLessFrequently()
//     {
//         var mediator = Substitute.For<IMediator>();
//         var clock = new FixedClock(DateTimeOffset.UtcNow);
//         var fixProvider = Substitute.For<IFixLookup>();
//         fixProvider.FindFix(Arg.Is("RIVET")).Returns(new Fix("RIVET", new Coordinate(0, 0)));
//         
//         var flight = new Flight
//         {
//             Callsign = "QFA123",
//             AircraftType = "B738",
//             WakeCategory = WakeCategory.Medium,
//             OriginIdentifier = "YMML",
//             DestinationIdentifier = "YSSY",
//             FeederFixIdentifier = "RIVET"
//         };
//         
//         flight.Activate(clock);
//
//         var sequence = CreateSequence(mediator, clock);
//         await sequence.Add(flight, CancellationToken.None);
//         
//         var sequenceProvider = Substitute.For<ISequenceProvider>();
//         sequenceProvider.TryGetSequence(Arg.Is("YSSY")).Returns(sequence);
//         
//         // Act
//         var notification = new FlightPositionReport(
//             "QFA123",
//             "YSSY",
//             new FlightPosition(new Coordinate(50, 50), 35_000, VerticalTrack.Maintaining, 450),
//             [new FixEstimate("RIVET", clock.UtcNow().AddHours(3))]);
//
//         var handler = new FlightPositionReportHandler(
//             sequenceProvider,
//             fixProvider,
//             mediator,
//             clock);
//         await handler.Handle(notification, CancellationToken.None);
//         
//         // Assert
//         var firstPositionUpdateTime = flight.PositionUpdated.ShouldNotBeNull();
//         var firstPosition = flight.LastKnownPosition.ShouldNotBeNull();
//         
//         firstPositionUpdateTime.ShouldBe(clock.UtcNow());
//         firstPosition.Coordinate.Latitude.ShouldBe(50);
//         firstPosition.Coordinate.Longitude.ShouldBe(50);
//         firstPosition.Altitude.ShouldBe(35_000);
//         
//         // Advance 45 seconds, position report should be ignored because it's too far away
//         clock.SetTime(clock.UtcNow().AddSeconds(45));
//         notification = notification with
//         {
//             Position = new FlightPosition(new Coordinate(49, 49), 25_000, VerticalTrack.Maintaining, 450),
//         };
//         
//         await handler.Handle(notification, CancellationToken.None);
//         
//         // Assert: No changes
//         flight.PositionUpdated.ShouldBe(firstPositionUpdateTime);
//         flight.LastKnownPosition.Coordinate.Latitude.ShouldBe(firstPosition.Coordinate.Latitude);
//         flight.LastKnownPosition.Coordinate.Longitude.ShouldBe(firstPosition.Coordinate.Longitude);
//         flight.LastKnownPosition.Altitude.ShouldBe(firstPosition.Altitude);
//     }
//
//     [Fact]
//     public async Task WhenAFlightIsClose_PositionReportsAreProcessedMoreFrequently()
//     {
//         var mediator = Substitute.For<IMediator>();
//         var clock = new FixedClock(DateTimeOffset.UtcNow);
//         var fixProvider = Substitute.For<IFixLookup>();
//         fixProvider.FindFix(Arg.Is("RIVET")).Returns(new Fix("RIVET", new Coordinate(0, 0)));
//         
//         var flight = new Flight
//         {
//             Callsign = "QFA123",
//             AircraftType = "B738",
//             WakeCategory = WakeCategory.Medium,
//             OriginIdentifier = "YMML",
//             DestinationIdentifier = "YSSY",
//             FeederFixIdentifier = "RIVET"
//         };
//         
//         flight.Activate(clock);
//
//         var sequence = CreateSequence(mediator, clock);
//         await sequence.Add(flight, CancellationToken.None);
//         
//         var sequenceProvider = Substitute.For<ISequenceProvider>();
//         sequenceProvider.TryGetSequence(Arg.Is("YSSY")).Returns(sequence);
//         
//         // Act
//         var notification = new FlightPositionReport(
//             "QFA123",
//             "YSSY",
//             new FlightPosition(new Coordinate(2, 2), 35_000, VerticalTrack.Maintaining, 450),
//             [new FixEstimate("RIVET", clock.UtcNow().AddHours(3))]);
//
//         var handler = new FlightPositionReportHandler(
//             sequenceProvider,
//             fixProvider,
//             mediator,
//             clock);
//         await handler.Handle(notification, CancellationToken.None);
//         
//         // Assert
//         var firstPositionUpdateTime = flight.PositionUpdated.ShouldNotBeNull();
//         var firstPosition = flight.LastKnownPosition.ShouldNotBeNull();
//         
//         firstPositionUpdateTime.ShouldBe(clock.UtcNow());
//         firstPosition.Coordinate.Latitude.ShouldBe(2);
//         firstPosition.Coordinate.Longitude.ShouldBe(2);
//         firstPosition.Altitude.ShouldBe(35_000);
//         
//         // Advance 45 seconds, new position should be recorded
//         clock.SetTime(clock.UtcNow().AddSeconds(45));
//         notification = notification with
//         {
//             Position = new FlightPosition(new Coordinate(1, 1), 25_000, VerticalTrack.Maintaining, 450),
//         };
//         
//         await handler.Handle(notification, CancellationToken.None);
//         
//         // Assert: No changes
//         
//         flight.PositionUpdated.ShouldBe(clock.UtcNow());
//         flight.LastKnownPosition.Coordinate.Latitude.ShouldBe(1);
//         flight.LastKnownPosition.Coordinate.Longitude.ShouldBe(1);
//         flight.LastKnownPosition.Altitude.ShouldBe(25_000);
//     }
//
//     Sequence CreateSequence(IMediator mediator, IClock clock)
//     {
//         var landingRateProvider = Substitute.For<ISeparationRuleProvider>();
//         landingRateProvider.GetRequiredSpacing(Arg.Any<Flight>(), Arg.Any<Flight>(), Arg.Any<RunwayModeConfiguration>())
//             .Returns(TimeSpan.FromMinutes(2));
//         
//         var estimateProvider = Substitute.For<IEstimateProvider>();
//         
//         return new Sequence(
//             airportConfigurationFixture.Instance,
//             landingRateProvider,
//             mediator,
//             clock,
//             estimateProvider);
//     }
// }