using Maestro.Plugin.Infrastructure;
using Maestro.Wpf;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;


public class OpenInformationWindowHandler(WindowManager windowManager, GuiInvoker guiInvoker)
    : IRequestHandler<OpenInformationWindowRequest, OpenInformationWindowResponse>
{
    public Task<OpenInformationWindowResponse> Handle(OpenInformationWindowRequest request, CancellationToken cancellationToken)
    {
        windowManager.FocusOrCreateWindow(
            WindowKeys.Information(request.Flight.Callsign),
            request.Flight.Callsign,
            windowHandle => new InformationView(request.Flight));

        return Task.FromResult(new OpenInformationWindowResponse());
    }
}
