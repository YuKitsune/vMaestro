using Maestro.Core.Configuration;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Core.Handlers;

public class GetApproachTypesRequestHandler(IArrivalConfigurationLookup arrivalConfigurationLookup)
    : IRequestHandler<GetApproachTypesRequest, GetApproachTypesResponse>
{
    public Task<GetApproachTypesResponse> Handle(GetApproachTypesRequest request, CancellationToken cancellationToken)
    {
        var approachTypes = arrivalConfigurationLookup
            .GetArrivals()
            .Where(a => a.AirportIdentifier == request.AirportIdentifier)
            .GroupBy(a => a.RunwayIdentifier, a => a.ApproachType)
            .Select(g => new RunwayApproachTypes(g.Key, g.Distinct().Where(a => !string.IsNullOrEmpty(a)).ToArray()))
            .ToArray();
        var response = new GetApproachTypesResponse(approachTypes);
        return Task.FromResult(response);
    }
}
