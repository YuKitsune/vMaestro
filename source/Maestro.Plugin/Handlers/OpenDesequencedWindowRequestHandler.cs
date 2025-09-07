using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenDesequencedWindowRequestHandler(WindowManager windowManager, IMediator mediator, IErrorReporter errorReporter)
    : IRequestHandler<OpenDesequencedWindowRequest, OpenDesequencedWindowResponse>
{
    public Task<OpenDesequencedWindowResponse> Handle(OpenDesequencedWindowRequest request, CancellationToken cancellationToken)
    {
        windowManager.FocusOrCreateWindow(
            WindowKeys.Desequenced(request.AirportIdentifier),
            "De-sequenced",
            windowHandle => new DesequencedView(
                new DesequencedViewModel(
                    mediator,
                    errorReporter,
                    request.AirportIdentifier,
                    request.Callsigns)));

        return Task.FromResult(new OpenDesequencedWindowResponse());
    }
}
