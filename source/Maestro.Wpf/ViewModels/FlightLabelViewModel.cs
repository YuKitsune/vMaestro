using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Messages;
using Maestro.Wpf.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class FlightLabelViewModel(
    IMediator mediator,
    FlightViewModel flightViewModel,
    RunwayModeViewModel runwayModeViewModel)
    : ObservableObject
{
    public FlightViewModel FlightViewModel { get; } = flightViewModel;
    public RunwayModeViewModel RunwayModeViewModel { get; } = runwayModeViewModel;

    public FlightLabelViewModel() : this(
        null!,
        new FlightViewModel(),
        new RunwayModeViewModel("34", [new RunwayViewModel("34L", 180)]))
    {
    }

    [RelayCommand]
    void ShowInformationWindow()
    {
        mediator.Send(new OpenInformationWindowRequest(FlightViewModel));
    }

    [RelayCommand]
    void Recompute()
    {
        mediator.Send(new RecomputeRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign));
    }

    [RelayCommand]
    void ChangeRunway(string runwayIdentifier)
    {
        mediator.Send(new ChangeRunwayRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign, runwayIdentifier));
    }

    [RelayCommand]
    void InsertFlightBefore()
    {
        mediator.Send(new OpenInsertFlightWindowRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign, InsertionPoint.Before));
    }

    [RelayCommand]
    void InsertFlightAfter()
    {
        mediator.Send(new OpenInsertFlightWindowRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign, InsertionPoint.After));
    }

    [RelayCommand]
    void InsertSlotBefore()
    {
        mediator.Send(new OpenInsertSlotWindowRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign, InsertionPoint.Before));
    }

    [RelayCommand]
    void InsertSlotAfter()
    {
        mediator.Send(new OpenInsertSlotWindowRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign, InsertionPoint.After));
    }

    [RelayCommand]
    void ChangeEta()
    {
        mediator.Send(new OpenEstimateWindowRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign));
    }

    [RelayCommand]
    void Remove()
    {
        mediator.Send(new RemoveRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign));
    }

    [RelayCommand]
    void Desequence()
    {
        mediator.Send(new DesequenceRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign));
    }

    [RelayCommand]
    void MakePending()
    {
        mediator.Send(new MakePendingRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign));
    }

    [RelayCommand]
    void ZeroDelay()
    {
        mediator.Send(new ZeroDelayRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign));
    }

    [RelayCommand]
    void Coordination()
    {
        mediator.Send(new OpenCoordinationWindowRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign));
    }
}