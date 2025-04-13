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
// public class FlightUpdatedHandlerTests(AirportConfigurationFixture airportConfigurationFixture)
// {
//     [Fact]
//     public async Task WhenANewFlightIsUpdated_AndOutOfRangeOfFeederFix_TheFlightIsNotTracked()
//     {
//         // Arrange
//         var clock = new FixedClock(DateTimeOffset.UtcNow);
//         var estimateProvider = Substitute.For<IEstimateProvider>();
//         
//         var notification = new FlightUpdatedNotification(
//             "QFA123",
//             "B738",
//             WakeCategory.Medium,
//             "YMML",
//             "YSSY",
//             "34L",
//             "RIVET1",
//             false,
//             null,
//             [new FixEstimate("RIVET", clock.UtcNow().AddHours(3))]);
//
//         var mediator = Substitute.For<IMediator>();
//         
//         var sequenceProvider = Substitute.For<ISequenceProvider>();
//         var sequence = CreateTestSequence(mediator, clock);
//         sequenceProvider.TryGetSequence(Arg.Is("YSSY")).Returns(sequence);
//
//         var handler = new FlightUpdatedHandler(
//             sequenceProvider,
//             estimateProvider,
//             mediator,
//             clock);
//         
//         // Act
//         await handler.Handle(notification, CancellationToken.None);
//         
//         // Assert
//         sequence.Flights.ShouldBeEmpty();
//     }
//     
//     [Fact]
//     public async Task WhenAnActivatedFlightIsUpdated_AndOutOfRangeOfFeederFix_TheFlightIsNotTracked()
//     {
//         // Arrange
//         var clock = new FixedClock(DateTimeOffset.UtcNow);
//         var estimateProvider = Substitute.For<IEstimateProvider>();
//         
//         var notification = new FlightUpdatedNotification(
//             "QFA123",
//             "B738",
//             WakeCategory.Medium,
//             "YMML",
//             "YSSY",
//             "34L",
//             "RIVET1",
//             true,
//             null,
//             [new FixEstimate("RIVET", clock.UtcNow().AddHours(3))]);
//
//         var mediator = Substitute.For<IMediator>();
//         
//         var sequenceProvider = Substitute.For<ISequenceProvider>();
//         var sequence = CreateTestSequence(mediator, clock);
//         sequenceProvider.TryGetSequence(Arg.Is("YSSY")).Returns(sequence);
//
//
//         var handler = new FlightUpdatedHandler(
//             sequenceProvider,
//             estimateProvider,
//             mediator,
//             clock);
//         
//         // Act
//         await handler.Handle(notification, CancellationToken.None);
//         
//         // Assert
//         sequence.Flights.ShouldBeEmpty();
//     }
//     
//     [Fact]
//     public async Task WhenANewFlightIsUpdated_AndWithinRangeOfFeederFix_TheFlightIsTracked()
//     {
//         // Arrange
//         var clock = new FixedClock(DateTimeOffset.UtcNow);
//         var estimateProvider = Substitute.For<IEstimateProvider>();
//         
//         var notification = new FlightUpdatedNotification(
//             "QFA123",
//             "B738",
//             WakeCategory.Medium,
//             "YMML",
//             "YSSY",
//             "34L",
//             "RIVET1",
//             false,
//             null,
//             [new FixEstimate("RIVET", clock.UtcNow().AddHours(1))]);
//
//         var mediator = Substitute.For<IMediator>();
//         
//         var sequenceProvider = Substitute.For<ISequenceProvider>();
//         var sequence = CreateTestSequence(mediator, clock);
//         sequenceProvider.TryGetSequence(Arg.Is("YSSY")).Returns(sequence);
//
//         var handler = new FlightUpdatedHandler(
//             sequenceProvider,
//             estimateProvider,
//             mediator,
//             clock);
//         
//         // Act
//         await handler.Handle(notification, CancellationToken.None);
//         
//         // Assert
//         var flight = sequence.Flights.ShouldHaveSingleItem();
//         flight.Callsign.ShouldBe("QFA123");
//         flight.Activated.ShouldBe(false);
//     }
//     
//     [Fact]
//     public async Task WhenAnActivatedFlightIsUpdated_AndWithinRangeOfFeederFix_TheFlightIsTracked()
//     {
//         // Arrange
//         var clock = new FixedClock(DateTimeOffset.UtcNow);
//         var estimateProvider = Substitute.For<IEstimateProvider>();
//         
//         var notification = new FlightUpdatedNotification(
//             "QFA123",
//             "B738",
//             WakeCategory.Medium,
//             "YMML",
//             "YSSY",
//             "34L",
//             "RIVET1",
//             true,
//             null,
//             [new FixEstimate("RIVET", clock.UtcNow().AddHours(1))]);
//
//         var mediator = Substitute.For<IMediator>();
//         
//         var sequenceProvider = Substitute.For<ISequenceProvider>();
//         var sequence = CreateTestSequence(mediator, clock);
//         sequenceProvider.TryGetSequence(Arg.Is("YSSY")).Returns(sequence);
//
//         var handler = new FlightUpdatedHandler(
//             sequenceProvider,
//             estimateProvider,
//             mediator,
//             clock);
//         
//         // Act
//         await handler.Handle(notification, CancellationToken.None);
//         
//         // Assert
//         var flight = sequence.Flights.ShouldHaveSingleItem();
//         flight.Callsign.ShouldBe("QFA123");
//         flight.Activated.ShouldBe(true);
//     }
//     
//     [Fact]
//     public async Task WhenAnExistingFlightIsActivated_TheFlightIsActivated()
//     {
//         // Arrange
//         var clock = new FixedClock(DateTimeOffset.UtcNow);
//         var estimateProvider = Substitute.For<IEstimateProvider>();
//         
//         var notification = new FlightUpdatedNotification(
//             "QFA123",
//             "B738",
//             WakeCategory.Medium,
//             "YMML",
//             "YSSY",
//             "34L",
//             "RIVET1",
//             false,
//             null,
//             [new FixEstimate("RIVET", clock.UtcNow().AddHours(1))]);
//
//         var mediator = Substitute.For<IMediator>();
//         
//         var sequenceProvider = Substitute.For<ISequenceProvider>();
//         var sequence = CreateTestSequence(mediator, clock);
//         sequenceProvider.TryGetSequence(Arg.Is("YSSY")).Returns(sequence);
//
//         var handler = new FlightUpdatedHandler(
//             sequenceProvider,
//             estimateProvider,
//             mediator,
//             clock);
//         
//         await handler.Handle(notification, CancellationToken.None);
//         
//         // Sanity check
//         var flight = sequence.Flights.ShouldHaveSingleItem();
//         flight.Callsign.ShouldBe("QFA123");
//         flight.Activated.ShouldBe(false);
//         
//         // Act
//         var activatedTime = DateTimeOffset.UtcNow;
//         clock.SetTime(activatedTime);
//         notification = notification with
//         {
//             Activated = true
//         };
//         
//         await handler.Handle(notification, CancellationToken.None);
//         
//         // Assert
//         flight = sequence.Flights.ShouldHaveSingleItem();
//         flight.Callsign.ShouldBe("QFA123");
//         flight.Activated.ShouldBe(true);
//         flight.ActivatedTime.ShouldBe(activatedTime);
//     }
//     
//     [Fact]
//     public void WhenAnInactiveFlightIsUpdated_ItIsNotRecomputed()
//     {
//         Assert.Fail("Stub");
//     }
//     
//     [Fact]
//     public void WhenAnActiveFlightIsUpdated_ItIsRecomputed()
//     {
//         Assert.Fail("Stub");
//     }
//
//     Sequence CreateTestSequence(IMediator mediator, IClock clock)
//     {
//         var landingRateProvider = Substitute.For<ISeparationRuleProvider>();
//         landingRateProvider.GetRequiredSpacing(Arg.Any<Flight>(), Arg.Any<Flight>(), Arg.Any<RunwayModeConfiguration>()).Returns(TimeSpan.FromMinutes(2));
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