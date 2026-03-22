using Maestro.Contracts.Sessions;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using Maestro.Core.Tests.Fixtures;
using Shouldly;

namespace Maestro.Core.Tests.Sessions;

public class LandingStatisticsTests(ClockFixture clockFixture)
{
    readonly DateTimeOffset _now = clockFixture.Instance.UtcNow();
    readonly TimeSpan _acceptanceRate = TimeSpan.FromSeconds(180);

    [Fact]
    public void RecordLandingTime_FirstLanding_RecordsTimeAndCalculatesNoDeviation()
    {
        // Arrange
        var statistics = new LandingStatistics();
        var runway = CreateRunway("34L", _acceptanceRate);
        var landingTime = _now.AddMinutes(-5);

        // Act
        statistics.RecordLandingTime(runway, landingTime, clockFixture.Instance);

        // Assert
        statistics.AchievedLandingRates.ShouldContainKey("34L");
        statistics.AchievedLandingRates["34L"].ShouldBeOfType<NoDeviation>();

        var snapshot = statistics.Snapshot();
        snapshot.RunwayLandingTimes["34L"].ActualLandingTimes.Length.ShouldBe(1);
        snapshot.RunwayLandingTimes["34L"].ActualLandingTimes[0].ShouldBe(landingTime);
    }

    [Fact]
    public void RecordLandingTime_MultipleLandingsWithinAcceptableGap_CalculatesAchievedRate()
    {
        // Arrange
        var statistics = new LandingStatistics();
        var runway = CreateRunway("34L", _acceptanceRate);

        // Act - Record three landings separated by the acceptance rate
        statistics.RecordLandingTime(runway, _now.AddMinutes(-10), clockFixture.Instance);
        statistics.RecordLandingTime(runway, _now.AddMinutes(-7), clockFixture.Instance);
        statistics.RecordLandingTime(runway, _now.AddMinutes(-4), clockFixture.Instance);

        // Assert
        statistics.AchievedLandingRates["34L"].ShouldBeOfType<AchievedRate>();
        var achievedRate = (AchievedRate)statistics.AchievedLandingRates["34L"];
        achievedRate.AverageLandingInterval.ShouldBe(TimeSpan.FromMinutes(3));
        achievedRate.LandingIntervalDeviation.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void RecordLandingTime_MultipleLandingsWithVariableGaps_CalculatesAverageInterval()
    {
        // Arrange
        var statistics = new LandingStatistics();
        var runway = CreateRunway("34L", _acceptanceRate);

        // Act - Record landings with different separations: 2min, 3min, 4min
        statistics.RecordLandingTime(runway, _now.AddMinutes(-10), clockFixture.Instance);
        statistics.RecordLandingTime(runway, _now.AddMinutes(-8), clockFixture.Instance);
        statistics.RecordLandingTime(runway, _now.AddMinutes(-5), clockFixture.Instance);
        statistics.RecordLandingTime(runway, _now.AddMinutes(-1), clockFixture.Instance);

        // Assert
        statistics.AchievedLandingRates["34L"].ShouldBeOfType<AchievedRate>();
        var achievedRate = (AchievedRate)statistics.AchievedLandingRates["34L"];

        // Average of 2, 3, 4 minutes is 3 minutes
        achievedRate.AverageLandingInterval.ShouldBe(TimeSpan.FromMinutes(3));

        // Deviation from 3 minute acceptance rate
        achievedRate.LandingIntervalDeviation.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void RecordLandingTime_LandingGapExceedsTwiceAcceptanceRate_ReturnsNoDeviation()
    {
        // Arrange
        var statistics = new LandingStatistics();
        var runway = CreateRunway("34L", _acceptanceRate);

        // Act - Record landings with a gap of 7 minutes (more than 2x the 3-minute acceptance rate)
        statistics.RecordLandingTime(runway, _now.AddMinutes(-10), clockFixture.Instance);
        statistics.RecordLandingTime(runway, _now.AddMinutes(-3), clockFixture.Instance);

        // Assert
        statistics.AchievedLandingRates["34L"].ShouldBeOfType<NoDeviation>();
    }

    [Fact]
    public void RecordLandingTime_RemovesStaleTimesOlderThanAveragingPeriod()
    {
        // Arrange
        var statistics = new LandingStatistics();
        var runway = CreateRunway("34L", _acceptanceRate);

        // Record a landing more than 1 hour ago
        statistics.RecordLandingTime(runway, _now.AddHours(-2), clockFixture.Instance);

        // Record a recent landing
        statistics.RecordLandingTime(runway, _now.AddMinutes(-5), clockFixture.Instance);

        // Act - Record another recent landing which should trigger stale time removal
        statistics.RecordLandingTime(runway, _now.AddMinutes(-2), clockFixture.Instance);

        // Assert
        var snapshot = statistics.Snapshot();
        snapshot.RunwayLandingTimes["34L"].ActualLandingTimes.Length.ShouldBe(2);
        snapshot.RunwayLandingTimes["34L"].ActualLandingTimes.ShouldNotContain(_now.AddHours(-2));
    }

    [Fact]
    public void RecordLandingTime_DifferentRunways_TracksIndependently()
    {
        // Arrange
        var statistics = new LandingStatistics();
        var runway34L = CreateRunway("34L", _acceptanceRate);
        var runway34R = CreateRunway("34R", _acceptanceRate);

        // Act
        statistics.RecordLandingTime(runway34L, _now.AddMinutes(-10), clockFixture.Instance);
        statistics.RecordLandingTime(runway34L, _now.AddMinutes(-7), clockFixture.Instance);
        statistics.RecordLandingTime(runway34R, _now.AddMinutes(-5), clockFixture.Instance);

        // Assert
        statistics.AchievedLandingRates.ShouldContainKey("34L");
        statistics.AchievedLandingRates.ShouldContainKey("34R");

        var snapshot = statistics.Snapshot();
        snapshot.RunwayLandingTimes["34L"].ActualLandingTimes.Length.ShouldBe(2);
        snapshot.RunwayLandingTimes["34R"].ActualLandingTimes.Length.ShouldBe(1);
    }

    [Fact]
    public void RecordLandingTime_CalculatesDeviationFromAcceptanceRate()
    {
        // Arrange
        var statistics = new LandingStatistics();
        var acceptanceRate = TimeSpan.FromMinutes(3);
        var runway = CreateRunway("34L", acceptanceRate);

        // Act - Record landings with 2-minute intervals (1 minute better than 3-minute rate)
        statistics.RecordLandingTime(runway, _now.AddMinutes(-10), clockFixture.Instance);
        statistics.RecordLandingTime(runway, _now.AddMinutes(-8), clockFixture.Instance);
        statistics.RecordLandingTime(runway, _now.AddMinutes(-6), clockFixture.Instance);

        // Assert
        statistics.AchievedLandingRates["34L"].ShouldBeOfType<AchievedRate>();
        var achievedRate = (AchievedRate)statistics.AchievedLandingRates["34L"];
        achievedRate.AverageLandingInterval.ShouldBe(TimeSpan.FromMinutes(2));
        achievedRate.LandingIntervalDeviation.ShouldBe(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Snapshot_CreatesCorrectDto()
    {
        // Arrange
        var statistics = new LandingStatistics();
        var runway = CreateRunway("34L", _acceptanceRate);
        var landingTime1 = _now.AddMinutes(-10);
        var landingTime2 = _now.AddMinutes(-7);

        statistics.RecordLandingTime(runway, landingTime1, clockFixture.Instance);
        statistics.RecordLandingTime(runway, landingTime2, clockFixture.Instance);

        // Act
        var snapshot = statistics.Snapshot();

        // Assert
        snapshot.RunwayLandingTimes.ShouldContainKey("34L");
        snapshot.RunwayLandingTimes["34L"].RunwayIdentifier.ShouldBe("34L");
        snapshot.RunwayLandingTimes["34L"].ActualLandingTimes.ShouldBe(new[] { landingTime1, landingTime2 });
        snapshot.RunwayLandingTimes["34L"].AchievedRate.ShouldBeOfType<AchievedRateDto>();

        var achievedRateDto = (AchievedRateDto)snapshot.RunwayLandingTimes["34L"].AchievedRate;
        achievedRateDto.AverageLandingInterval.ShouldBe(TimeSpan.FromMinutes(3));
        achievedRateDto.LandingIntervalDeviation.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Snapshot_WithNoDeviation_CreatesNoDeviationDto()
    {
        // Arrange
        var statistics = new LandingStatistics();
        var runway = CreateRunway("34L", _acceptanceRate);

        statistics.RecordLandingTime(runway, _now.AddMinutes(-5), clockFixture.Instance);

        // Act
        var snapshot = statistics.Snapshot();

        // Assert
        snapshot.RunwayLandingTimes["34L"].AchievedRate.ShouldBeOfType<NoDeviationDto>();
    }

    [Fact]
    public void Restore_RestoresLandingTimesAndAchievedRates()
    {
        // Arrange
        var statistics = new LandingStatistics();
        var landingTime1 = _now.AddMinutes(-10);
        var landingTime2 = _now.AddMinutes(-7);

        var dto = new LandingStatisticsDto
        {
            RunwayLandingTimes = new Dictionary<string, RunwayLandingTimesDto>
            {
                ["34L"] = new RunwayLandingTimesDto(
                    RunwayIdentifier: "34L",
                    ActualLandingTimes: [landingTime1, landingTime2],
                    AchievedRate: new AchievedRateDto(
                        TimeSpan.FromMinutes(3),
                        TimeSpan.Zero))
            }
        };

        // Act
        statistics.Restore(dto);

        // Assert
        statistics.AchievedLandingRates.ShouldContainKey("34L");
        statistics.AchievedLandingRates["34L"].ShouldBeOfType<AchievedRate>();

        var achievedRate = (AchievedRate)statistics.AchievedLandingRates["34L"];
        achievedRate.AverageLandingInterval.ShouldBe(TimeSpan.FromMinutes(3));
        achievedRate.LandingIntervalDeviation.ShouldBe(TimeSpan.Zero);

        var snapshot = statistics.Snapshot();
        snapshot.RunwayLandingTimes["34L"].ActualLandingTimes.ShouldBe(new[] { landingTime1, landingTime2 });
    }

    [Fact]
    public void Restore_WithNoDeviation_RestoresNoDeviation()
    {
        // Arrange
        var statistics = new LandingStatistics();
        var landingTime = _now.AddMinutes(-5);

        var dto = new LandingStatisticsDto
        {
            RunwayLandingTimes = new Dictionary<string, RunwayLandingTimesDto>
            {
                ["34L"] = new RunwayLandingTimesDto(
                    RunwayIdentifier: "34L",
                    ActualLandingTimes: [landingTime],
                    AchievedRate: new NoDeviationDto())
            }
        };

        // Act
        statistics.Restore(dto);

        // Assert
        statistics.AchievedLandingRates["34L"].ShouldBeOfType<NoDeviation>();
    }

    [Fact]
    public void Restore_ClearsPreviousData()
    {
        // Arrange
        var statistics = new LandingStatistics();
        var runway = CreateRunway("34L", _acceptanceRate);
        statistics.RecordLandingTime(runway, _now.AddMinutes(-5), clockFixture.Instance);

        var dto = new LandingStatisticsDto
        {
            RunwayLandingTimes = new Dictionary<string, RunwayLandingTimesDto>
            {
                ["34R"] = new RunwayLandingTimesDto(
                    RunwayIdentifier: "34R",
                    ActualLandingTimes: [_now.AddMinutes(-3)],
                    AchievedRate: new NoDeviationDto())
            }
        };

        // Act
        statistics.Restore(dto);

        // Assert
        statistics.AchievedLandingRates.ShouldNotContainKey("34L");
        statistics.AchievedLandingRates.ShouldContainKey("34R");
    }

    static Runway CreateRunway(string identifier, TimeSpan acceptanceRate)
    {
        return new Runway(identifier, "", acceptanceRate, []);
    }
}
