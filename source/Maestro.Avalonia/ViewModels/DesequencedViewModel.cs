using System.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Avalonia.Integrations;
using Maestro.Contracts.Flights;
using MediatR;

namespace Maestro.Avalonia.ViewModels;

public partial class DesequencedViewModel : ObservableObject
{
    readonly IMediator _mediator;
    readonly IWindowHandle _windowHandle;
    readonly IErrorReporter _errorReporter;

    [ObservableProperty]
    List<string> _callsigns = [];

    string AirportIdentifier { get; }

    public DesequencedViewModel(
        IMediator mediator,
        IWindowHandle windowHandle,
        IErrorReporter errorReporter,
        string airportIdentifier,
        string[] callsigns)
    {
        AirportIdentifier = airportIdentifier;
        _errorReporter = errorReporter;
        Callsigns = callsigns.ToList();
        _mediator = mediator;
        _windowHandle = windowHandle;
    }

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
