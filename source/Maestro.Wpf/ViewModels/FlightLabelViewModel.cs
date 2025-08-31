using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Wpf.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class FlightLabelViewModel(
    IMediator mediator,
    SequenceViewModel sequence,
    FlightMessage flightViewModel,
    RunwayModeViewModel runwayModeViewModel)
    : ObservableObject
{
    [ObservableProperty]
    bool _isSelected = false;

    readonly SequenceViewModel _sequence = sequence;

    public FlightMessage FlightViewModel { get; } = flightViewModel;
    public RunwayModeViewModel RunwayModeViewModel { get; } = runwayModeViewModel;

    public bool CanInsertBefore => FlightViewModel.State != State.Frozen;

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

    [RelayCommand(CanExecute = nameof(CanInsertFlightBefore))]
    void InsertFlightBefore()
    {
        mediator.Send(
            new OpenInsertFlightWindowRequest(
                FlightViewModel.DestinationIdentifier,
                new RelativeInsertionOptions(FlightViewModel.Callsign, RelativePosition.Before),
                _sequence.Flights.Where(f => f.State == State.Landed).ToArray(),
                _sequence.Flights.Where(f => f.State == State.Pending).ToArray()));
    }

    bool CanInsertFlightBefore()
    {
        return FlightViewModel.State != State.Frozen;
    }

    [RelayCommand]
    void InsertFlightAfter()
    {
        mediator.Send(
            new OpenInsertFlightWindowRequest(
                FlightViewModel.DestinationIdentifier,
                new RelativeInsertionOptions(FlightViewModel.Callsign, RelativePosition.Before),
                _sequence.Flights.Where(f => f.State == State.Landed).ToArray(),
                _sequence.Flights.Where(f => f.State == State.Pending).ToArray()));
    }

    [RelayCommand]
    void InsertSlotBefore()
    {
        mediator.Send(
            new BeginSlotCreationRequest(
                FlightViewModel.DestinationIdentifier,
                [FlightViewModel.AssignedRunway],
                FlightViewModel.LandingTime,
                SlotCreationReferencePoint.Before));
    }

    [RelayCommand]
    void InsertSlotAfter()
    {
        mediator.Send(
            new BeginSlotCreationRequest(
                FlightViewModel.DestinationIdentifier,
                [FlightViewModel.AssignedRunway],
                FlightViewModel.LandingTime,
                SlotCreationReferencePoint.After));
    }

    [RelayCommand(CanExecute = nameof(CanChangeEta))]
    void ChangeEta()
    {
        mediator.Send(
            new OpenChangeFeederFixEstimateWindowRequest(
                FlightViewModel.DestinationIdentifier,
                FlightViewModel.Callsign,
                FlightViewModel.FeederFixIdentifier!,
                FlightViewModel.FeederFixEstimate!.Value));
    }

    bool CanChangeEta()
    {
        return !string.IsNullOrEmpty(FlightViewModel.FeederFixIdentifier) && FlightViewModel.FeederFixEstimate != null;
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
