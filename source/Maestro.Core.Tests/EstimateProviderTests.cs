using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using NSubstitute;
using Shouldly;

namespace Maestro.Core.Tests;

// TODO:
// - When a flight has been re-routed to a new feeder fix, estimates are derived from trajectory

public class EstimateProviderTests
{
    readonly IClock _clock;
    readonly AirportConfigurationFixture _airportConfigurationFixture;
    readonly IArrivalLookup _arrivalLookup;
    readonly IFixLookup _fixLookup;
    readonly DateTimeOffset _currentTime = new(2025, 04, 12, 12, 00, 00, TimeSpan.Zero);
    readonly TimeSpan _arrivalTimeToGo = TimeSpan.FromMinutes(12);

    public EstimateProviderTests(AirportConfigurationFixture airportConfigurationFixture)
    {
        _clock = new FixedClock(_currentTime);

        _airportConfigurationFixture = airportConfigurationFixture;

        _arrivalLookup = Substitute.For<IArrivalLookup>();
        _arrivalLookup.GetTimeToGo(Arg.Any<Flight>()).Returns(_arrivalTimeToGo);

        _fixLookup = Substitute.For<IFixLookup>();
        _fixLookup.FindFix(Arg.Is("RIVET")).Returns(new Fix("RIVET", new Coordinate(0, 0)));
    }

    [Fact]
    public void GetFeederFixEstimate_WithoutPosition_UsesSystemEstimate()
    {
        // Arrange
        var estimateProvider = new EstimateProvider(
            _arrivalLookup,
            _fixLookup,
            _clock);

        // Act
        var systemEstimate = _currentTime.AddMinutes(5);
        var estimate = estimateProvider.GetFeederFixEstimate(
            _airportConfigurationFixture.Instance,
            "RIVET",
            systemEstimate,
            null);

        // Assert
        estimate.ShouldBe(systemEstimate);
    }

    [Fact]
    public void GetFeederFixEstimate_WithinRange_UsesTrajectory()
    {
        // Arrange
        var estimateProvider = new EstimateProvider(
            _arrivalLookup,
            _fixLookup,
            _clock);

        // Act
        var systemEstimate = _currentTime.AddMinutes(65);
        var position = new FlightPosition(
            new Coordinate(1, 0), // 60 nm
            25000,
            VerticalTrack.Maintaining,
            60,
            isOnGround: false);

        var estimate = estimateProvider.GetFeederFixEstimate(
            _airportConfigurationFixture.Instance,
            "RIVET",
            systemEstimate,
            position);

        // Assert
        var expectedEstimate = _clock.UtcNow().AddHours(1).AddMinutes(2.5);
        estimate.ShouldNotBeNull();
        estimate.Value.ShouldBe(expectedEstimate, TimeSpan.FromSeconds(30));
        estimate.Value.ShouldNotBe(systemEstimate, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void GetLandingEstimate_WithoutIntervals_UsesSystemEstimate()
    {
        // Arrange
        var flight = new FlightBuilder("QFA2")
            .WithFeederFix("BOREE")
            .WithRunway("34R")
            .Build();

        var estimateProvider = new EstimateProvider(
            _arrivalLookup,
            _fixLookup,
            _clock);

        // Act
        var systemEstimate = _currentTime.AddMinutes(15);
        var estimate = estimateProvider.GetLandingEstimate(flight, systemEstimate);

        // Assert
        estimate.ShouldBe(systemEstimate);
    }

    [Fact]
    public void GetLandingEstimate_WithIntervals_UsesPresetInterval()
    {
        // Arrange
        var estimateProvider = new EstimateProvider(
            _arrivalLookup,
            _fixLookup,
            _clock);

        var feederFixEstimate = _currentTime.AddMinutes(5);
        var flight = new FlightBuilder("QFA123")
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(feederFixEstimate)
            .Build();

        // Act
        var systemEstimate = _currentTime.AddMinutes(15);
        var estimate = estimateProvider.GetLandingEstimate(
            flight,
            systemEstimate);

        // Assert
        var expectedEstimate = feederFixEstimate.Add(_arrivalTimeToGo);
        estimate.ShouldNotBeNull();
        estimate.Value.ShouldBe(expectedEstimate, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void GetLandingEstimate_AfterPassingFeederFix_UsesActualFeederFixTime()
    {
        // Arrange
        var estimateProvider = new EstimateProvider(
            _arrivalLookup,
            _fixLookup,
            _clock);

        var actualFeederFixTime = _currentTime.AddMinutes(-4);
        var flight = new FlightBuilder("QFA2")
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_currentTime.AddMinutes(-5)) // Last ETA_FF was 5 minutes ago
            .PassedFeederFixAt(actualFeederFixTime) // ATO_FF was a bit late
            .Build();

        // Act
        var systemEstimate = _currentTime.AddMinutes(10);
        var estimate = estimateProvider.GetLandingEstimate(flight, systemEstimate);

        // Assert
        var expectedEstimate = actualFeederFixTime.Add(_arrivalTimeToGo);
        estimate.ShouldNotBeNull();
        estimate.Value.ShouldBe(expectedEstimate, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void GetLandingEstimate_AfterPassingFeederFix_WithoutFeederFixTime_UsesSystemEstimate()
    {
        // Arrange
        var estimateProvider = new EstimateProvider(
            _arrivalLookup,
            _fixLookup,
            _clock);

        var flight = new FlightBuilder("QFA2")
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_currentTime.AddMinutes(-5)) // Last ETA_FF was 5 minutes ago
            .PassedFeederFixAt(DateTimeOffset.MaxValue) // logged on after passing the feeder fix
            .Build();

        // Act
        var systemEstimate = _currentTime.AddMinutes(10);
        var estimate = estimateProvider.GetLandingEstimate(flight, systemEstimate);

        // Assert
        estimate.ShouldNotBeNull();
        estimate.Value.ShouldBe(systemEstimate, TimeSpan.FromSeconds(30));
    }
}
