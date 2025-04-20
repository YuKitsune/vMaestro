using Maestro.Core.Messages;
using Maestro.Wpf.Messages;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class OpenInsertSlotWindowRequestHandler : IRequestHandler<OpenInsertSlotWindowRequest, OpenInsertSlotWindowResponse>
{
    public Task<OpenInsertSlotWindowResponse> Handle(OpenInsertSlotWindowRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}