using Maestro.Core.Messages;
using Maestro.Wpf.Messages;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class OpenInsertFlightWindowRequestHandler : IRequestHandler<OpenInsertFlightWindowRequest>
{
    public Task Handle(OpenInsertFlightWindowRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
