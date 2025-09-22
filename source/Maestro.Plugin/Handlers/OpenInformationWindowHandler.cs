using Maestro.Plugin.Infrastructure;
using Maestro.Wpf;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;


public class OpenInformationWindowHandler(WindowManager windowManager)
    : IRequestHandler<OpenInformationWindowRequest, OpenInformationWindowResponse>
{
    public Task<OpenInformationWindowResponse> Handle(OpenInformationWindowRequest request, CancellationToken cancellationToken)
    {
        windowManager.FocusOrCreateWindow(
            WindowKeys.Information(request.Flight.Callsign),
            request.Flight.Callsign,
            _ => new InformationView(new FlightInformationViewModel(request.Flight)));

        return Task.FromResult(new OpenInformationWindowResponse());
    }
}
