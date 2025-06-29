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

                // B STAR, All other props
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
                }
            ],
            MinimumRadarEstimateRange = 0,
            FeederFixes = [],
            Runways = [],
            RunwayModes = [],
            Views = [],
            RunwayAssignmentRules = []
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
            new AircraftPerformanceData
            {
                Type = aircraftType,
                AircraftCategory = category,
                WakeCategory = WakeCategory.Medium
            });

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
            new AircraftPerformanceData
            {
                Type = "DH8D",
                AircraftCategory = AircraftCategory.NonJet,
                WakeCategory = WakeCategory.Medium
            });

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
            new AircraftPerformanceData
            {
                Type = "B738",
                AircraftCategory = AircraftCategory.Jet,
                WakeCategory = WakeCategory.Medium
            });
        
        var nonJetInterval = arrivalLookup.GetArrivalInterval(
            "YZZZ",
            "ABCDE",
            "ABC1B",
            "01",
            new AircraftPerformanceData
            {
                Type = "SF34",
                AircraftCategory = AircraftCategory.NonJet,
                WakeCategory = WakeCategory.Medium
            });

        // Assert
        jetInterval.ShouldBe(TimeSpan.FromMinutes(11));
        nonJetInterval.ShouldBe(TimeSpan.FromMinutes(12));
    }
}