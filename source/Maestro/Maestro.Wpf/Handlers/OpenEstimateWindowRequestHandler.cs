using Maestro.Core.Messages;
using Maestro.Wpf.Messages;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class OpenEstimateWindowRequestHandler : IRequestHandler<OpenEstimateWindowRequest, OpenEstimateWindowResponse>
{
    public Task<OpenEstimateWindowResponse> Handle(OpenEstimateWindowRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}