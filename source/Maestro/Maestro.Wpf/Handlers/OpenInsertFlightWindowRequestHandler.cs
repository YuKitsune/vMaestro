using Maestro.Core.Messages;
using Maestro.Wpf.Messages;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class OpenInsertFlightWindowRequestHandler : IRequestHandler<OpenInsertFlightWindowRequest, OpenInsertFlightWindowResponse>
{
    public Task<OpenInsertFlightWindowResponse> Handle(OpenInsertFlightWindowRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}