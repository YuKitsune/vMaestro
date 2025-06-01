using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class DesequencedViewModel : ObservableObject
{
    readonly IMediator _mediator;
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand), nameof(RemoveCommand))]
    string _selectedCallsign = string.Empty;

    [ObservableProperty]
    List<string> _callsigns = [];
    
    public DesequencedViewModel(IMediator mediator, string airportIdentifier, string[] callsigns)
    {
        AirportIdentifier = airportIdentifier;
        Callsigns = callsigns.ToList();
        _mediator = mediator;
    }

    public string AirportIdentifier { get; }
    
    bool CanExecute() => !string.IsNullOrWhiteSpace(SelectedCallsign);

    [RelayCommand(CanExecute = nameof(CanExecute))]
    async Task Resume()
    {
        await _mediator.Send(new ResumeSequencingRequest(AirportIdentifier, SelectedCallsign));

        var callsigns = Callsigns.ToList();
        callsigns.Remove(SelectedCallsign);
        Callsigns = callsigns;
    }
    
    [RelayCommand(CanExecute = nameof(CanExecute))]
    async Task Remove()
    {
        await _mediator.Send(new RemoveRequest(AirportIdentifier, SelectedCallsign));
        
        var callsigns = Callsigns.ToList();
        callsigns.Remove(SelectedCallsign);
        Callsigns = callsigns;
    }
}