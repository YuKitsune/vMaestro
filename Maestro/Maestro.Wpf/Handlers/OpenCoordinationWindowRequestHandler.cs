using Maestro.Core.Messages;
using Maestro.Wpf.Messages;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class OpenCoordinationWindowRequestHandler : IRequestHandler<OpenCoordinationWindowRequest, OpenCoordinationWindowResponse>
{
    public Task<OpenCoordinationWindowResponse> Handle(OpenCoordinationWindowRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}