using Maestro.Contracts.Sessions;
using Maestro.Core.Extensions;
using Maestro.Core.Sessions;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Contracts;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenInsertFlightWindowRequestHandler(
    WindowManager windowManager,
    ISessionManager sessionManager,
    IMediator mediator,
    IErrorReporter errorReporter)
    : IRequestHandler<OpenInsertFlightWindowRequest>
{
    public async Task Handle(OpenInsertFlightWindowRequest request, CancellationToken cancellationToken)
    {
        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);

        SessionDto sessionDto;
        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            sessionDto = session.Snapshot();
        }

        windowManager.FocusOrCreateWindow(
            WindowKeys.InsertFlight(request.AirportIdentifier),
            "Insert a Flight",
            windowHandle =>
            {
                var viewModel = new InsertFlightViewModel(
                    request.AirportIdentifier,
                    request.Options,
                    sessionDto,
                    windowHandle,
                    mediator,
                    errorReporter);

                return new InsertFlightView(viewModel);
            });
    }
}
