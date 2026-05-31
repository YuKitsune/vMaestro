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

public class OpenDesequencedWindowRequestHandler(
    WindowManager windowManager,
    ISessionManager sessionManager,
    IMediator mediator,
    IErrorReporter errorReporter)
    : IRequestHandler<OpenDesequencedWindowRequest, OpenDesequencedWindowResponse>
{
    public async Task<OpenDesequencedWindowResponse> Handle(OpenDesequencedWindowRequest request, CancellationToken cancellationToken)
    {
        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);

        SessionDto sessionDto;
        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            sessionDto = session.Snapshot();
        }

        windowManager.FocusOrCreateWindow(
            WindowKeys.Desequenced(request.AirportIdentifier),
            "De-sequenced",
            windowHandle => new DesequencedView(
                new DesequencedViewModel(
                    mediator,
                    windowHandle,
                    errorReporter,
                    request.AirportIdentifier,
                    sessionDto)));

        return new OpenDesequencedWindowResponse();
    }
}
