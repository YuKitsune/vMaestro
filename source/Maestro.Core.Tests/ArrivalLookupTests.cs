using System.Text.RegularExpressions;
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
            PreferredRunways = [],
            Arrivals =
            [
                // Matches all aircraft
                new ArrivalConfiguration
                {
                    FeederFix = "ABCDE",
                    ArrivalRegex = new Regex("ABC\\dA"),
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
                    ArrivalRegex = new Regex("ABC\\dB"),
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
                    ArrivalRegex = new Regex("ABC\\dB"),
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
                    ArrivalRegex = new Regex("ABC\\dC"),
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
                    ArrivalRegex = new Regex("ABC\\dC"),
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
                    ArrivalRegex = new Regex("ABC\\dC"),
                    Category = AircraftCategory.NonJet,
                    RunwayIntervals = new Dictionary<string, int>
                    {
                        {
                            "01", 15
                        }
                    }
                }
            ],
            MinimumRadarEstimateRange = 0,
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
            "ABC1A",
            "01",
            aircraftType,
            category);

        // Assert
        // A STAR has the same time for all types
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
            "ABC1B",
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
            "ABC1B",
            "01",
            "B738",
            AircraftCategory.Jet);

        var nonJetInterval = arrivalLookup.GetArrivalInterval(
            "YZZZ",
            "ABCDE",
            "ABC1B",
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
            "ABC1C",
            "01",
            "B738",
            AircraftCategory.Jet);

        var dash8Interval = arrivalLookup.GetArrivalInterval(
            "YZZZ",
            "ABCDE",
            "ABC1C",
            "01",
            "DH8D",
            AircraftCategory.NonJet);

        var nonJetInterval = arrivalLookup.GetArrivalInterval(
            "YZZZ",
            "ABCDE",
            "ABC1C",
            "01",
            "SF34",
            AircraftCategory.NonJet);

        // Assert
        jetInterval.ShouldBe(TimeSpan.FromMinutes(13));
        dash8Interval.ShouldBe(TimeSpan.FromMinutes(14));
        nonJetInterval.ShouldBe(TimeSpan.FromMinutes(15));
    }
}
