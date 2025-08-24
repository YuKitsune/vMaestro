using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class BeginSlotCreationRequestHandler(MaestroViewModel maestroViewModel) : IRequestHandler<BeginSlotCreationRequest>
{
    public Task Handle(BeginSlotCreationRequest request, CancellationToken cancellationToken)
    {
        maestroViewModel.BeginSlotCreation(request.ReferenceLandingTime, request.ReferencePoint, request.RunwayIdentifiers);
        return Task.CompletedTask;
    }
}
