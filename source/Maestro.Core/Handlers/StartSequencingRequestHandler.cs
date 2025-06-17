using Maestro.Core.Configuration;
using Maestro.Core.Messages;
using Maestro.Core.Scheduling;
using MediatR;

namespace Maestro.Core.Handlers;

public class StartSequencingRequestHandler(SchedulerBackgroundService schedulerBackgroundService, IAirportConfigurationProvider airportConfigurationProvider)
    : IRequestHandler<StartSequencingRequest>, IRequestHandler<StartSequencingAllRequest>
{
    public async Task Handle(StartSequencingRequest request, CancellationToken cancellationToken)
    {
        await schedulerBackgroundService.Start(request.AirportIdentifier, cancellationToken);
    }
    
    public async Task Handle(StartSequencingAllRequest request, CancellationToken cancellationToken)
    {
        foreach (var airportConfiguration in airportConfigurationProvider.GetAirportConfigurations())
        {
            await schedulerBackgroundService.Start(airportConfiguration.Identifier, cancellationToken);
        }
    }
}

// TODO: Stop sequencing on shutdown