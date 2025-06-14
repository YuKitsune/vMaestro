using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using NSubstitute;
using Shouldly;

namespace Maestro.Core.Tests;

public class EstimateProviderTests
{
    readonly IClock _clock;
    readonly IPerformanceLookup _performanceLookup;
    readonly IArrivalLookup _arrivalLookup;
    readonly IFixLookup _fixLookup;
    readonly DateTimeOffset _currentTime = new(2025, 04, 12, 12, 00, 00, TimeSpan.Zero);
    readonly TimeSpan _arrivalInterval = TimeSpan.FromMinutes(12);
    
    public EstimateProviderTests()
    {
        _clock = new FixedClock(_currentTime);
        
        _performanceLookup = Substitute.For<IPerformanceLookup>();
        
        _arrivalLookup = Substitute.For<IArrivalLookup>();
        _arrivalLookup.GetArrivalInterval(
                Arg.Is("YSSY"),
                Arg.Is("RIVET"),
                Arg.Is("RIVET4"),
                Arg.Is("34L"),
                Arg.Is(AircraftType.Jet))
            .Returns(_arrivalInterval);

        _fixLookup = Substitute.For<IFixLookup>();
        _fixLookup.FindFix(Arg.Is("RIVET")).Returns(new Fix("RIVET", new Coordinate(0, 0)));
    }

    [Fact]
    public void GetFeederFixEstimate_UsingSystemEstimate()
    {
        // Arrange
        var config = Substitute.For<IMaestroConfiguration>();
        config.FeederFixEstimateSource.Returns(FeederFixEstimateSource.SystemEstimate);
        var estimateProvider = new EstimateProvider(config, _performanceLookup, _arrivalLookup, _fixLookup, _clock);
        
        // Act
        var systemEstimate = _currentTime.AddMinutes(5);
        var estimate = estimateProvider.GetFeederFixEstimate(
            null,
            systemEstimate,
            null);
        
        // Assert
        estimate.ShouldBe(systemEstimate);
    }

    [Fact]
    public void GetFeederFixEstimate_UsingTrajectory()
    {
        // Arrange
        var config = Substitute.For<IMaestroConfiguration>();
        config.FeederFixEstimateSource.Returns(FeederFixEstimateSource.Trajectory);

        var estimateProvider = new EstimateProvider(config, _performanceLookup, _arrivalLookup, _fixLookup, _clock);
        
        // Act
        var systemEstimate = _currentTime.AddMinutes(65);
        var position = new FlightPosition(
            new Coordinate(1, 0),
            25000,
            VerticalTrack.Maintaining,
            60);
        var estimate = estimateProvider.GetFeederFixEstimate(
            "RIVET",
            systemEstimate,
            position);

        // Assert
        var expectedEstimate = _clock.UtcNow().AddHours(1);
        estimate.ShouldNotBeNull();
        estimate.Value.ShouldBe(expectedEstimate, TimeSpan.FromSeconds(30));
        estimate.Value.ShouldNotBe(systemEstimate, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void GetLandingEstimate_WithoutIntervals_UsesSystemEstimate()
    {
        // Arrange
        var config = Substitute.For<IMaestroConfiguration>();

        var flight = new FlightBuilder("QFA2")
            .WithFeederFix("BOREE")
            .WithRunway("34R")
            .Build();

        var estimateProvider = new EstimateProvider(config, _performanceLookup, _arrivalLookup, _fixLookup, _clock);
        
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
        var estimateConfiguration = Substitute.For<IMaestroConfiguration>();
        var estimateProvider = new EstimateProvider(
            estimateConfiguration,
            _performanceLookup,
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
        var expectedEstimate = feederFixEstimate.Add(_arrivalInterval);
        estimate.ShouldNotBeNull();
        estimate.Value.ShouldBe(expectedEstimate, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void GetLandingEstimate_AfterPassingFeederFix_UsesActualFeederFixTime()
    {
        // Arrange
        var estimateConfiguration = Substitute.For<IMaestroConfiguration>();
        var estimateProvider = new EstimateProvider(
            estimateConfiguration,
            _performanceLookup,
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
        var expectedEstimate = actualFeederFixTime.Add(_arrivalInterval);
        estimate.ShouldNotBeNull();
        estimate.Value.ShouldBe(expectedEstimate, TimeSpan.FromSeconds(30));
    }
}