using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class FlightLandedNotificationHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IMediator mediator,
    ILogger logger)
    : INotificationHandler<FlightLandedNotification>
{
    public async Task Handle(FlightLandedNotification notification, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(notification.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying FlightLandedNotification for {AirportIdentifier}", notification.AirportIdentifier);
            await connection.Send(notification, cancellationToken);
            return;
        }

        // TODO: Maybe move this up to avoid telling the master about irrelevant flights?
        var instance = await instanceManager.GetInstance(notification.AirportIdentifier, cancellationToken);
        var flight = instance.Session.Sequence.FindFlight(notification.Callsign);
        if (flight is null)
        {
            logger.Verbose("FlightLandedNotification received for a {Callsign} who is not in the {AirportIdentifier} sequence", notification.Callsign,  notification.AirportIdentifier);
            return;
        }

        var runway = instance.Session.Sequence.CurrentRunwayMode.Runways.FirstOrDefault(r => r.Identifier == flight.AssignedRunwayIdentifier);
        if (runway is null)
        {
            logger.Verbose("{Callsign} landed on an off-mode runway, cannot update achieved rates", notification.Callsign);
            return;
        }

        using (instance.Semaphore.LockAsync(cancellationToken))
        {
            instance.Session.LandingStatistics.RecordLandingTime(
                runway,
                notification.ActualLandingTime,
                TimeProvider.System); // TODO: Inject time provider
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                instance.Session.Snapshot()),
            cancellationToken);
    }
}

public class LandingStatistics
{
    readonly TimeSpan _averagingPeriod = TimeSpan.FromHours(1); // TODO: Add to configuration

    readonly Dictionary<string, List<DateTimeOffset>> _actualLandingTimesPerRunway = new();

    public Dictionary<string, IAchievedRate> AchievedLandingRates { get; } = new();

    public void RecordLandingTime(Runway runway, DateTimeOffset actualLandingTime, TimeProvider timeProvider)
    {
        if (_actualLandingTimesPerRunway.TryGetValue(runway.Identifier, out var landingTimes))
        {
            landingTimes.Add(actualLandingTime);
            RemoveStaleTimes(timeProvider.GetUtcNow(), _actualLandingTimesPerRunway[runway.Identifier]);
        }
        else
        {
            landingTimes = _actualLandingTimesPerRunway[runway.Identifier] = [actualLandingTime];
        }

        AchievedLandingRates[runway.Identifier] = CalculateAchievedRate(runway, landingTimes);
        void RemoveStaleTimes(DateTimeOffset referenceTime, List<DateTimeOffset> times)
        {
            var oldestTime = referenceTime.Subtract(_averagingPeriod);
            times.Where(t => t.IsSameOrBefore(oldestTime))
                .ToList()
                .ForEach(t => times.Remove(t));
        }
    }

    IAchievedRate CalculateAchievedRate(Runway runway, IReadOnlyList<DateTimeOffset> actualLandingTimes)
    {
        if (actualLandingTimes.Count == 0)
        {
            // No samples, no deviation
            return new NoDeviation();
        }

        var diffs = new List<TimeSpan>();
        for (var i = 1; i < actualLandingTimes.Count; i++)
        {
            var previous = actualLandingTimes[i - 1];
            var current = actualLandingTimes[i];

            var diff = current - previous;
            diffs.Add(diff);
        }

        // If any two flights are separated by more than 2x the desired landing rate, then it's not busy enough
        var doubleRate = TimeSpan.FromSeconds(runway.AcceptanceRate.TotalSeconds * 2);
        if (diffs.Any(d => d >= doubleRate))
        {
            return new NoDeviation();
        }

        var averageSeconds = diffs.Average(t => t.TotalSeconds);
        var averageInterval = TimeSpan.FromSeconds(averageSeconds);
        var deviation = runway.AcceptanceRate - averageInterval;

        return new AchievedRate(averageInterval, deviation);
    }

    public LandingStatisticsDto Snapshot()
    {
        return new LandingStatisticsDto
        {
            RunwayLandingTimes = AchievedLandingRates.ToDictionary(
                x => x.Key,
                x => new RunwayLandingTimesDto(
                    RunwayIdentifier: x.Key,
                    ActualLandingTimes: _actualLandingTimesPerRunway[x.Key].ToArray(),
                    AchievedRate: x.Value switch
                    {
                        NoDeviation => new NoDeviationDto(),
                        AchievedRate rate => new AchievedRateDto(
                            rate.AverageLandingInterval,
                            rate.LandingIntervalDeviation),
                        _ => new NoDeviationDto()
                    }))
        };
    }

    public void Restore(LandingStatisticsDto dto)
    {
        _actualLandingTimesPerRunway.Clear();
        AchievedLandingRates.Clear();

        foreach (var kvp in dto.RunwayLandingTimes)
        {
            var runway = kvp.Key;
            var runwayLandingTimesDto = kvp.Value;
            _actualLandingTimesPerRunway[runway] = new List<DateTimeOffset>(runwayLandingTimesDto.ActualLandingTimes);

            AchievedLandingRates[runway] = runwayLandingTimesDto.AchievedRate switch
            {
                NoDeviationDto => new NoDeviation(),
                AchievedRateDto achievedRateDto => new AchievedRate(
                    achievedRateDto.AverageLandingInterval,
                    achievedRateDto.LandingIntervalDeviation),
                _ => new NoDeviation()
            };
        }
    }
}

public interface IAchievedRate;
public record NoDeviation : IAchievedRate;
public record AchievedRate(
    TimeSpan AverageLandingInterval,
    TimeSpan LandingIntervalDeviation)
    : IAchievedRate;
