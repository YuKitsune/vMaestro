using Maestro.Core.Infrastructure;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenInsertFlightWindowRequestHandler(WindowManager windowManager, IMessageDispatcher messageDispatcher, IErrorReporter errorReporter)
    : IRequestHandler<OpenInsertFlightWindowRequest>
{
    public Task Handle(OpenInsertFlightWindowRequest request, CancellationToken cancellationToken)
    {
        windowManager.FocusOrCreateWindow(
            WindowKeys.InsertFlight(request.AirportIdentifier),
            "Insert a Flight",
            windowHandle =>
            {
                var viewModel = new InsertFlightViewModel(
                    request.AirportIdentifier,
                    request.Options,
                    request.LandedFlights,
                    request.PendingFlights,
                    windowHandle,
                    messageDispatcher,
                    errorReporter);

                return new InsertFlightView(viewModel);
            });

        return Task.CompletedTask;
    }
}
