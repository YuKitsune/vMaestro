using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using NSubstitute;
using Shouldly;

namespace Maestro.Core.Tests;

public class EstimateProviderTests
{
    readonly Flight _flight;
    
    readonly IClock _clock;
    readonly IArrivalLookup _arrivalLookup;
    readonly IFixLookup _fixLookup;
    readonly DateTimeOffset _currentTime = new(2025, 04, 12, 12, 00, 00, TimeSpan.Zero);
    readonly DateTimeOffset _feederFixSystemEstimate = new(2025, 04, 12, 12, 05, 00, TimeSpan.Zero);
    readonly DateTimeOffset _landingSystemEstimate = new(2025, 04, 12, 12, 15, 00, TimeSpan.Zero);
    
    public EstimateProviderTests()
    {
        _clock = new FixedClock(_currentTime);
        
        _arrivalLookup = Substitute.For<IArrivalLookup>();
        _arrivalLookup.GetArrivalInterval(Arg.Is("YSSY"), Arg.Is("RIVET4"), Arg.Is("34L"))
            .Returns(TimeSpan.FromMinutes(12));

        _fixLookup = Substitute.For<IFixLookup>();
        _fixLookup.FindFix(Arg.Is("RIVET")).Returns(new Fix("RIVET", new Coordinate(0, 0)));
        
        _flight = new Flight
        {
            Callsign = "QFA123",
            AircraftType = "B738",
            WakeCategory = WakeCategory.Medium,
            OriginIdentifier = "YMML",
            DestinationIdentifier = "YSSY",
            FeederFixIdentifier = "RIVET",
            AssignedRunwayIdentifier = "34L",
            AssignedStarIdentifier = "RIVET4"
        };

        _flight.UpdatePosition(
            new FlightPosition(
                new Coordinate(1, 0),
                25000,
                VerticalTrack.Maintaining,
                60), // 60 kts = 1 degree of latitude per hour
            [
                new FixEstimate("RIVET", _feederFixSystemEstimate),
                new FixEstimate("TESAT", _landingSystemEstimate)
            ],
            _clock);
        
        _flight.UpdateFeederFixEstimate(_feederFixSystemEstimate);
    }

    [Fact]
    public void GetFeederFixEstimate_UsingSystemEstimate()
    {
        var config = Substitute.For<IMaestroConfiguration>();
        config.FeederFixEstimateSource.Returns(FeederFixEstimateSource.SystemEstimate);

        var estimateProvider = new EstimateProvider(config, _arrivalLookup, _fixLookup, _clock);
        var estimate = estimateProvider.GetFeederFixEstimate(_flight);
        
        estimate.ShouldBe(_feederFixSystemEstimate);
    }

    [Fact]
    public void GetFeederFixEstimate_UsingTrajectory()
    {
        var config = Substitute.For<IMaestroConfiguration>();
        config.FeederFixEstimateSource.Returns(FeederFixEstimateSource.Trajectory);

        var estimateProvider = new EstimateProvider(config, _arrivalLookup, _fixLookup, _clock);
        var estimate = estimateProvider.GetFeederFixEstimate(_flight);

        var expectedEstimate = _clock.UtcNow().AddHours(1);
        estimate.ShouldNotBeNull();
        estimate.Value.ShouldBe(expectedEstimate, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void GetLandingEstimate_UsingSystemEstimate()
    {
        var config = Substitute.For<IMaestroConfiguration>();
        config.LandingEstimateSource.Returns(LandingEstimateSource.SystemEstimate);

        var estimateProvider = new EstimateProvider(config, _arrivalLookup, _fixLookup, _clock);
        var estimate = estimateProvider.GetLandingEstimate(_flight);
        
        estimate.ShouldBe(_landingSystemEstimate);
    }

    [Fact]
    public void GetLandingEstimate_UsingPresetInterval()
    {
        var estimateConfiguration = Substitute.For<IMaestroConfiguration>();
        estimateConfiguration.LandingEstimateSource.Returns(LandingEstimateSource.PresetInterval);
        
        var estimateProvider = new EstimateProvider(
            estimateConfiguration,
            _arrivalLookup,
            _fixLookup,
            _clock);
        var estimate = estimateProvider.GetLandingEstimate(_flight);

        var expectedEstimate = _flight.EstimatedFeederFixTime!.Value.Add(TimeSpan.FromMinutes(12));
        estimate.ShouldNotBeNull();
        estimate.Value.ShouldBe(expectedEstimate, TimeSpan.FromSeconds(30));
    }
}