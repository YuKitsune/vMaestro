using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Configuration;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class FlightLabelViewModel(
    IMediator mediator,
    IErrorReporter errorReporter,
    MaestroViewModel maestroViewModel,
    FlightMessage flightViewModel,
    string[] availableRunways,
    PermissionSet permissionSet)
    : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvailableRunways))]
    [NotifyCanExecuteChangedFor(nameof(ShowInformationWindowCommand))]
    FlightMessage _flightViewModel = flightViewModel;

    [ObservableProperty]
    bool _isSelected = false;

    public string[] AvailableRunways => availableRunways.Where(r => r != FlightViewModel.AssignedRunwayIdentifier).ToArray();

    public bool CanInsertBefore => FlightViewModel.State != State.Frozen;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(
        nameof(RecomputeCommand),
        nameof(ChangeRunwayCommand),
        nameof(InsertFlightBeforeCommand),
        nameof(InsertFlightAfterCommand),
        nameof(InsertSlotBeforeCommand),
        nameof(InsertSlotAfterCommand),
        nameof(ChangeEtaCommand),
        nameof(RemoveCommand),
        nameof(DesequenceCommand),
        nameof(MakePendingCommand),
        nameof(ZeroDelayCommand),
        nameof(CoordinationCommand))]
    PermissionSet _permissionSet = permissionSet;

    [RelayCommand(CanExecute = nameof(CanShowInformation))]
    void ShowInformationWindow()
    {
        try
        {
            mediator.Send(new OpenInformationWindowRequest(FlightViewModel));
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    bool CanShowInformation()
    {
        return !FlightViewModel.IsDummy;
    }

    [RelayCommand(CanExecute = nameof(CanRecompute))]
    void Recompute()
    {
        try
        {
            mediator.Send(
                new RecomputeRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    bool CanRecompute()
    {
        return !FlightViewModel.IsDummy && permissionSet.CanPerformAction(ActionKeys.Recompute);
    }

    [RelayCommand(CanExecute = nameof(CanChangeRunway))]
    void ChangeRunway(string runwayIdentifier)
    {
        try
        {
            mediator.Send(
                new ChangeRunwayRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign, runwayIdentifier),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanInsertFlightBefore))]
    void InsertFlightBefore()
    {
        try
        {
            mediator.Send(
                new OpenInsertFlightWindowRequest(
                    FlightViewModel.DestinationIdentifier,
                    new RelativeInsertionOptions(FlightViewModel.Callsign, RelativePosition.Before),
                    maestroViewModel.Flights.Where(f => f.State == State.Landed).ToArray(),
                    maestroViewModel.Flights.Where(f => f.State == State.Pending).ToArray()));
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    bool CanInsertFlightBefore()
    {
        return FlightViewModel.State != State.Frozen && CanInsertFlight();
    }

    [RelayCommand(CanExecute = nameof(CanInsertFlight))]
    void InsertFlightAfter()
    {
        try
        {
            mediator.Send(
                new OpenInsertFlightWindowRequest(
                    FlightViewModel.DestinationIdentifier,
                    new RelativeInsertionOptions(FlightViewModel.Callsign, RelativePosition.Before),
                    maestroViewModel.Flights.Where(f => f.State == State.Landed).ToArray(),
                    maestroViewModel.Flights.Where(f => f.State == State.Pending).ToArray()));
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageSlots))]
    void InsertSlotBefore()
    {
        try
        {
            mediator.Send(
                new BeginSlotCreationRequest(
                    FlightViewModel.DestinationIdentifier,
                    [FlightViewModel.AssignedRunwayIdentifier],
                    FlightViewModel.LandingTime,
                    SlotCreationReferencePoint.Before));
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageSlots))]
    void InsertSlotAfter()
    {
        try
        {
            mediator.Send(
                new BeginSlotCreationRequest(
                    FlightViewModel.DestinationIdentifier,
                    [FlightViewModel.AssignedRunwayIdentifier],
                    FlightViewModel.LandingTime,
                    SlotCreationReferencePoint.After));
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanChangeEta))]
    void ChangeEta()
    {
        try
        {
            mediator.Send(
                new OpenChangeFeederFixEstimateWindowRequest(
                    FlightViewModel.DestinationIdentifier,
                    FlightViewModel.Callsign,
                    FlightViewModel.FeederFixIdentifier!,
                    FlightViewModel.FeederFixEstimate!.Value));
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    bool CanChangeEta()
    {
        return !string.IsNullOrEmpty(FlightViewModel.FeederFixIdentifier) && FlightViewModel.FeederFixEstimate != null && permissionSet.CanPerformAction(ActionKeys.ChangeFeederFixEstimate);
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    void Remove()
    {
        try
        {
            mediator.Send(
                new RemoveRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanDesequence))]
    void Desequence()
    {
        try
        {
            mediator.Send(
                new DesequenceRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanMakePending))]
    void MakePending()
    {
        try
        {
            mediator.Send(
                new MakePendingRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanChangeMaxDelay))]
    void ZeroDelay()
    {
        try
        {
            mediator.Send(
                new ZeroDelayRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanCoordinate))]
    void Coordination()
    {
        try
        {
            mediator.Send(new OpenCoordinationWindowRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign));
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    // Permission-aware CanExecute methods for key commands
    bool CanInsertFlight() => permissionSet.CanPerformAction(ActionKeys.InsertPending) || permissionSet.CanPerformAction(ActionKeys.InsertDummy) || permissionSet.CanPerformAction(ActionKeys.InsertOvershoot);
    bool CanChangeRunway(string runwayIdentifier) => permissionSet.CanPerformAction(ActionKeys.ChangeRunway);
    bool CanRemove() => permissionSet.CanPerformAction(ActionKeys.RemoveFlight);
    bool CanDesequence() => permissionSet.CanPerformAction(ActionKeys.Desequence);
    bool CanMakePending() => permissionSet.CanPerformAction(ActionKeys.MakePending);
    bool CanManageSlots() => permissionSet.CanPerformAction(ActionKeys.ManageSlots);
    bool CanCoordinate() => permissionSet.CanPerformAction(ActionKeys.Coordination);
    bool CanChangeMaxDelay() => permissionSet.CanPerformAction(ActionKeys.ChangeMaxDelay);
}
