using System.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class DesequencedViewModel : ObservableObject
{
    readonly IMediator _mediator;

    [ObservableProperty]
    List<string> _callsigns = [];

    public DesequencedViewModel(IMediator mediator, string airportIdentifier, string[] callsigns)
    {
        AirportIdentifier = airportIdentifier;
        Callsigns = callsigns.ToList();
        _mediator = mediator;
    }

    public string AirportIdentifier { get; }

    [RelayCommand]
    async Task Resume(IList selectedCallsigns)
    {
        var callsigns = Callsigns.ToList();
        foreach (var selectedCallsign in selectedCallsigns)
        {
            var selectedCallsignString = (string) selectedCallsign;
            await _mediator.Send(new ResumeSequencingRequest(AirportIdentifier, selectedCallsignString));
            callsigns.Remove(selectedCallsignString);
        }

        Callsigns = callsigns;
    }

    [RelayCommand]
    async Task Remove(IList selectedCallsigns)
    {
        var callsigns = Callsigns.ToList();
        foreach (var selectedCallsign in selectedCallsigns)
        {
            var selectedCallsignString = (string) selectedCallsign;
            await _mediator.Send(new RemoveRequest(AirportIdentifier, selectedCallsignString));
            callsigns.Remove(selectedCallsignString);
        }

        Callsigns = callsigns;
    }
}
