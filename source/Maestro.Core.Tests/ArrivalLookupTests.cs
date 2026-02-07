using Maestro.Core.Configuration;
using Maestro.Core.Model;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests;

public class ArrivalLookupTests
{
    readonly IAirportConfigurationProvider _airportConfigurationProvider;

    public ArrivalLookupTests()
    {
        var airportConfiguration = new AirportConfiguration
        {
            Identifier = "YZZZ",
            Arrivals =
            [
                // Matches all aircraft
                new ArrivalConfiguration
                {
                    FeederFix = "ABCDE",
                    RunwayIntervals = new Dictionary<string, int>
                    {
                        {
                            "01", 10
                        }
                    }
                },

                // B STAR, Jets and DH8D only
                new ArrivalConfiguration
                {
                    FeederFix = "ABCDE",
                    ApproachType = "B",
                    Category = AircraftCategory.Jet,
                    AdditionalAircraftTypes = ["DH8D"],
                    RunwayIntervals = new Dictionary<string, int>
                    {
                        {
                            "01", 11
                        }
                    }
                },

                // B STAR, Props only
                new ArrivalConfiguration
                {
                    FeederFix = "ABCDE",
                    ApproachType = "B",
                    Category = AircraftCategory.NonJet,
                    RunwayIntervals = new Dictionary<string, int>
                    {
                        {
                            "01", 12
                        }
                    }
                },

                // C STAR, Jets only
                new ArrivalConfiguration
                {
                    FeederFix = "ABCDE",
                    ApproachType = "C",
                    Category = AircraftCategory.Jet,
                    RunwayIntervals = new Dictionary<string, int>
                    {
                        {
                            "01", 13
                        }
                    }
                },

                // C STAR, DH8D only
                new ArrivalConfiguration
                {
                    FeederFix = "ABCDE",
                    ApproachType = "C",
                    AircraftType = "DH8D",
                    RunwayIntervals = new Dictionary<string, int>
                    {
                        {
                            "01", 14
                        }
                    }
                },

                // C STAR, Non-jet only
                new ArrivalConfiguration
                {
                    FeederFix = "ABCDE",
                    ApproachType = "C",
                    Category = AircraftCategory.NonJet,
                    RunwayIntervals = new Dictionary<string, int>
                    {
                        {
                            "01", 15
                        }
                    }
                },

                // D and E STARs, multiple approach types
                new ArrivalConfiguration
                {
                    FeederFix = "ABCDE",
                    ApproachType = "D",
                    AircraftType = "DINGUS",
                    RunwayIntervals = new Dictionary<string, int>
                    {
                        {
                            "01", 16
                        }
                    }
                },
                new ArrivalConfiguration
                {
                    FeederFix = "ABCDE",
                    ApproachType = "E",
                    AircraftType = "DINGUS",
                    RunwayIntervals = new Dictionary<string, int>
                    {
                        {
                            "01", 17
                        }
                    }
                },

                // Alternative STAR,
                new ArrivalConfiguration
                {
                    FeederFix = "ABCDE",
                    ApproachFix = "QUIET",
                    RunwayIntervals = new Dictionary<string, int>
                    {
                        {
                            "01", 18
                        }
                    }
                }
            ],
            FeederFixes = [],
            Runways = [],
            RunwayModes = [],
            Views = [],
            DepartureAirports = []
        };

        _airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        _airportConfigurationProvider.GetAirportConfigurations().Returns([airportConfiguration]);
    }

    [Theory]
    [InlineData("DH8D", AircraftCategory.NonJet)]
    [InlineData("SF34", AircraftCategory.NonJet)]
    [InlineData("B738", AircraftCategory.Jet)]
    [InlineData("CONC", AircraftCategory.Jet)]
    public void WhenMinimalFiltersAreUsed_CorrectIntervalIsReturned(string aircraftType, AircraftCategory category)
    {
        // Arrange
        var arrivalLookup = new ArrivalLookup(
            _airportConfigurationProvider,
            Substitute.For<ILogger>());

        // Act
        var interval = arrivalLookup.GetArrivalInterval(
            "YZZZ",
            "ABCDE",
            [],
            "",
            "01",
            aircraftType,
            category);

        // Assert
        interval.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void JetOnlyArrivals_WithAdditionalAircraftTypes_CorrectIntervalIsReturned()
    {
        // Arrange
        var arrivalLookup = new ArrivalLookup(
            _airportConfigurationProvider,
            Substitute.For<ILogger>());

        // Act
        var interval = arrivalLookup.GetArrivalInterval(
            "YZZZ",
            "ABCDE",
            [],
            "B",
            "01",
            "DH8D",
            AircraftCategory.NonJet);

        // Assert
        // Despite DH8D being non-jet, it should still match based on aircraft type
        interval.ShouldBe(TimeSpan.FromMinutes(11));
    }

    [Fact]
    public void WhenJetAndNonJetTimesExist_CorrectIntervalIsReturned()
    {
        // Arrange
        var arrivalLookup = new ArrivalLookup(
            _airportConfigurationProvider,
            Substitute.For<ILogger>());

        // Act
        var jetInterval = arrivalLookup.GetArrivalInterval(
            "YZZZ",
            "ABCDE",
            [],
            "B",
            "01",
            "B738",
            AircraftCategory.Jet);

        var nonJetInterval = arrivalLookup.GetArrivalInterval(
            "YZZZ",
            "ABCDE",
            [],
            "B",
            "01",
            "SF34",
            AircraftCategory.NonJet);

        // Assert
        jetInterval.ShouldBe(TimeSpan.FromMinutes(11));
        nonJetInterval.ShouldBe(TimeSpan.FromMinutes(12));
    }

    [Fact]
    public void WhenSpecificAircraftTypeIsSpecified_CorrectIntervalIsReturned()
    {
        // Arrange
        var arrivalLookup = new ArrivalLookup(
            _airportConfigurationProvider,
            Substitute.For<ILogger>());

        // Act
        var jetInterval = arrivalLookup.GetArrivalInterval(
            "YZZZ",
            "ABCDE",
            [],
            "C",
            "01",
            "B738",
            AircraftCategory.Jet);

        var dash8Interval = arrivalLookup.GetArrivalInterval(
            "YZZZ",
            "ABCDE",
            [],
            "C",
            "01",
            "DH8D",
            AircraftCategory.NonJet);

        var nonJetInterval = arrivalLookup.GetArrivalInterval(
            "YZZZ",
            "ABCDE",
            [],
            "C",
            "01",
            "SF34",
            AircraftCategory.NonJet);

        // Assert
        jetInterval.ShouldBe(TimeSpan.FromMinutes(13));
        dash8Interval.ShouldBe(TimeSpan.FromMinutes(14));
        nonJetInterval.ShouldBe(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void WhenMultipleApproachTypesExist_CorrectIntervalIsReturned()
    {
        // Arrange
        var arrivalLookup = new ArrivalLookup(
            _airportConfigurationProvider,
            Substitute.For<ILogger>());

        // Act
        var deltaInterval = arrivalLookup.GetArrivalInterval(
            "YZZZ",
            "ABCDE",
            [],
            "D",
            "01",
            "DINGUS",
            AircraftCategory.Jet);

        var echoInterval = arrivalLookup.GetArrivalInterval(
            "YZZZ",
            "ABCDE",
            [],
            "E",
            "01",
            "DINGUS",
            AircraftCategory.Jet);

        // Assert
        deltaInterval.ShouldBe(TimeSpan.FromMinutes(16));
        echoInterval.ShouldBe(TimeSpan.FromMinutes(17));
    }

    [Theory]
    [InlineData("DH8D", AircraftCategory.NonJet)]
    [InlineData("SF34", AircraftCategory.NonJet)]
    [InlineData("B738", AircraftCategory.Jet)]
    [InlineData("CONC", AircraftCategory.Jet)]
    public void WhenApproachFixExists_CorrectIntervalIsReturned(string aircraftType, AircraftCategory category)
    {
        // Arrange
        var arrivalLookup = new ArrivalLookup(
            _airportConfigurationProvider,
            Substitute.For<ILogger>());

        // Act
        var interval = arrivalLookup.GetArrivalInterval(
            "YZZZ",
            "ABCDE",
            ["QUIET"],
            "",
            "01",
            aircraftType,
            category);

        // Assert
        interval.ShouldBe(TimeSpan.FromMinutes(18));
    }

    // TODO: Test cases
    // - When different approach types are specified, correct interval is returned (add a Z approach type)
    // - When approach fix is specified, approach fix must match
}
