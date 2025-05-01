using Maestro.Core.Configuration;
using Maestro.Core.Model;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Maestro.Core.Handlers;

public record ChangeRunwayModeResponse;

public record ChangeRunwayModeRequest(string AirportIdentifier, string RunwayModeIdentifier, DateTimeOffset StartTime, bool ReAssignRunways) : IRequest<ChangeRunwayModeResponse>;

public class ChangeRunwayModeRequestHandler(ISequenceProvider sequenceProvider, IAirportConfigurationProvider airportConfigurationProvider, ILogger<RecomputeRequestHandler> logger)
    : IRequestHandler<ChangeRunwayModeRequest, ChangeRunwayModeResponse>
{
    public async Task<ChangeRunwayModeResponse> Handle(ChangeRunwayModeRequest request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        
        var sequence = sequenceProvider.TryGetSequence(request.AirportIdentifier);
        if (sequence == null)
        {
            logger.LogWarning("Sequence not found for airport {AirportIdentifier}.", request.AirportIdentifier);
            return new ChangeRunwayModeResponse();
        }
        
        var airportConfiguration = airportConfigurationProvider.GetAirportConfigurations().SingleOrDefault(c => c.Identifier == request.AirportIdentifier);
        if (airportConfiguration == null)
        {
            logger.LogWarning("Airport configuration not found for {AirportIdentifier}.", request.AirportIdentifier);
            return new ChangeRunwayModeResponse();
        }
        
        // TODO: Support changing rates as well as modes
        var runwayMode = airportConfiguration.RunwayModes.SingleOrDefault(r => r.Identifier == request.RunwayModeIdentifier);
        if (runwayMode == null)
        {
            logger.LogWarning("Runway Mode {RunwayModeIdentifier} not found for {AirportIdentifier}.", request.RunwayModeIdentifier, request.AirportIdentifier);
            return new ChangeRunwayModeResponse();
        }
        
        sequence.ChangeRunwayMode(runwayMode);

        if (request.ReAssignRunways)
        {
            throw new NotImplementedException("Reassign runways is not implemented.");
        }

        return new ChangeRunwayModeResponse();
    }
}