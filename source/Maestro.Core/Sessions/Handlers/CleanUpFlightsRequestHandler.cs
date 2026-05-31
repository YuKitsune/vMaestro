using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Sessions.Contracts;
using MediatR;
using Serilog;

namespace Maestro.Core.Sessions.Handlers;

public class CleanUpFlightsRequestHandler(
    IMaestroConnectionManager connectionManager,
    ISessionManager sessionManager,
    IAirportConfigurationProvider airportConfigurationProvider,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<CleanUpFlightsRequest>
{
    public async Task Handle(CleanUpFlightsRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Debug("Skipping flight clean up for {AirportIdentifier} as we are not the master", request.AirportIdentifier);
            return;
        }

        logger.Verbose("Cleaning up flights for {AirportIdentifier}", request.AirportIdentifier);

        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(request.AirportIdentifier);
            var lostTimeout = TimeSpan.FromMinutes(airportConfiguration.LostFlightTimeoutMinutes);

            CleanUpLandedFlights(session, airportConfiguration);
            CleanUpLostFlights(session, lostTimeout);
            CleanUpStaleFlightDataRecords(session, lostTimeout);

            sessionDto = session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(session.AirportIdentifier, sessionDto),
            cancellationToken);
    }

    void CleanUpLandedFlights(Session session, AirportConfiguration airportConfiguration)
    {
        var sequence = session.Sequence;
        var landedFlights = sequence.Flights
            .Where(f => f.State == State.Landed)
            .ToArray();

        for (var i = 0; i < landedFlights.Length; i++)
        {
            var landedFlight = landedFlights[i];
            var timeSinceLanded = clock.UtcNow() - landedFlight.LandingTime;
            var landedFlightTimeout = TimeSpan.FromMinutes(airportConfiguration.LandedFlightTimeoutMinutes);
            if (i < airportConfiguration.MaxLandedFlights && timeSinceLanded < landedFlightTimeout)
                continue;

            sequence.Remove(landedFlight);
            logger.Information(
                "Removing {Callsign} from {AirportIdentifier}: exceeded landed retention limit",
                landedFlight.Callsign,
                sequence.AirportIdentifier);
        }
    }

    void CleanUpLostFlights(Session session, TimeSpan lostTimeout)
    {
        var sequencedToRemove = new List<Flight>();
        foreach (var flight in session.Sequence.Flights)
        {
            if (flight.State == State.Landed || flight.IsManuallyInserted)
                continue;

            session.FlightDataRecords.TryGetValue(flight.Callsign, out var record);
            var isLost = record is null || clock.UtcNow() - record.LastSeen > lostTimeout;
            if (isLost)
            {
                logger.Information("{Callsign} not seen within lost timeout, removing from sequence", flight.Callsign);
                sequencedToRemove.Add(flight);
            }
        }

        foreach (var flight in sequencedToRemove)
            session.Sequence.Remove(flight);

        var desequencedToRemove = new List<Flight>();
        foreach (var flight in session.DeSequencedFlights)
        {
            if (flight.State == State.Landed)
                continue;

            session.FlightDataRecords.TryGetValue(flight.Callsign, out var record);
            var isLost = record is null || clock.UtcNow() - record.LastSeen > lostTimeout;
            if (isLost)
            {
                logger.Information("{Callsign} not seen within lost timeout, removing from desequenced list", flight.Callsign);
                desequencedToRemove.Add(flight);
            }
        }

        foreach (var flight in desequencedToRemove)
            session.DeSequencedFlights.Remove(flight);
    }

    void CleanUpStaleFlightDataRecords(Session session, TimeSpan lostTimeout)
    {
        var staleCallsigns = session.FlightDataRecords
            .Where(kvp =>
                clock.UtcNow() - kvp.Value.LastSeen > lostTimeout &&
                session.Sequence.FindFlight(kvp.Key) is null &&
                !session.DeSequencedFlights.Any(f => f.Callsign == kvp.Key))
            .Select(kvp => kvp.Key)
            .ToArray();

        foreach (var callsign in staleCallsigns)
        {
            session.FlightDataRecords.Remove(callsign);
            logger.Information("{Callsign} flight data record expired, removing", callsign);
        }
    }
}
