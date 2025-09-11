using System.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Messages;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class DesequencedViewModel : ObservableObject
{
    readonly IMediator _mediator;
    readonly IErrorReporter _errorReporter;

    [ObservableProperty]
    List<string> _callsigns = [];

    public DesequencedViewModel(
        IMediator mediator,
        IErrorReporter errorReporter,
        string airportIdentifier,
        string[] callsigns)
    {
        AirportIdentifier = airportIdentifier;
        _errorReporter = errorReporter;
        Callsigns = callsigns.ToList();
        _mediator = mediator;
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
}
