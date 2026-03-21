using System.Diagnostics;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Contracts;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenWindWindowRequestHandler(WindowManager windowManager, IMediator mediator, IErrorReporter errorReporter)
    : IRequestHandler<OpenWindWindowRequest>
{
    public Task Handle(OpenWindWindowRequest request, CancellationToken cancellationToken)
    {
        try
        {
            windowManager.FocusOrCreateWindow(
                WindowKeys.Wind(request.AirportIdentifier),
                "Wind Configuration",
                windowHandle =>
                {
                    var viewModel = new WindViewModel(
                        request.AirportIdentifier,
                        request.SurfaceWind,
                        request.UpperWind,
                        request.UpperWindAltitude,
                        mediator,
                        windowHandle,
                        errorReporter);

                    return new WindView(viewModel);
                });
        }
        catch (Exception ex)
        {
            Debugger.Break();
        }

        return Task.CompletedTask;
    }
}
