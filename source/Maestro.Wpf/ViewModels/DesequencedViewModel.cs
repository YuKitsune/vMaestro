using System.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class DesequencedViewModel : ObservableObject
{
    readonly IMediator _mediator;
    readonly IWindowHandle _windowHandle;
    readonly IErrorReporter _errorReporter;

    [ObservableProperty]
    List<string> _callsigns = [];

    public DesequencedViewModel(
        IMediator mediator,
        IWindowHandle windowHandle,
        IErrorReporter errorReporter,
        string airportIdentifier,
        SessionDto session)
    {
        AirportIdentifier = airportIdentifier;
        _errorReporter = errorReporter;
        _mediator = mediator;
        _windowHandle = windowHandle;

        ApplySession(session);

        WeakReferenceMessenger.Default.Register<SessionUpdatedNotification>(this, (_, notification) =>
        {
            if (notification.AirportIdentifier != AirportIdentifier)
                return;

            ApplySession(notification.Session);
        });
    }

    void ApplySession(SessionDto session)
    {
        Callsigns = session.DeSequencedFlights.Select(f => f.Callsign).ToList();
    }

    public string AirportIdentifier { get; }

    [RelayCommand]
    async Task Resume(IList selectedCallsigns)
    {
        try
        {
            var callsigns = Callsigns.ToList();
            foreach (var selectedCallsign in selectedCallsigns)
            {
                var selectedCallsignString = (string) selectedCallsign;
                await _mediator.Send(
                    new ResumeSequencingRequest(AirportIdentifier, selectedCallsignString),
                    CancellationToken.None);
                callsigns.Remove(selectedCallsignString);
            }

            Callsigns = callsigns;
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    async Task Remove(IList selectedCallsigns)
    {
        try
        {
            var callsigns = Callsigns.ToList();
            foreach (var selectedCallsign in selectedCallsigns)
            {
                var selectedCallsignString = (string) selectedCallsign;
                await _mediator.Send(
                    new RemoveRequest(AirportIdentifier, selectedCallsignString),
                    CancellationToken.None);

                callsigns.Remove(selectedCallsignString);
            }

            Callsigns = callsigns;
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void Close()
    {
        _windowHandle.Close();
    }
}
