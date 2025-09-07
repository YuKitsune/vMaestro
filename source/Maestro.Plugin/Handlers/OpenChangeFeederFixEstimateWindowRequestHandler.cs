using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenChangeFeederFixEstimateWindowRequestHandler(WindowManager windowManager, IMediator mediator, IErrorReporter errorReporter)
    : IRequestHandler<OpenChangeFeederFixEstimateWindowRequest>
{
    public Task Handle(OpenChangeFeederFixEstimateWindowRequest request, CancellationToken cancellationToken)
    {
        windowManager.FocusOrCreateWindow(
            WindowKeys.ChangeFeederFixEstimate(request.AirportIdentifier),
            "Change ETA_FF",
            windowHandle =>
            {
                var viewModel = new ChangeFeederFixEstimateViewModel(
                    request.AirportIdentifier,
                    request.Callsign,
                    request.FeederFix,
                    request.OriginalFeederFixEstimate,
                    windowHandle,
                    mediator,
                    errorReporter);

                return new ChangeFeederFixEstimateView(viewModel);
            });

        return Task.CompletedTask;
    }
}
