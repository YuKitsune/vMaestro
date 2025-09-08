using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class FlightLabelViewModel(
    IMessageDispatcher messageDispatcher,
    IErrorReporter errorReporter,
    MaestroViewModel maestroViewModel,
    FlightMessage flightViewModel)
    : ObservableObject
{
    [ObservableProperty]
    FlightMessage _flightViewModel = flightViewModel;

    [ObservableProperty]
    bool _isSelected = false;

    public bool CanInsertBefore => FlightViewModel.State != State.Frozen;

    [RelayCommand(CanExecute = nameof(CanShowInformation))]
    void ShowInformationWindow()
    {
        try
        {
            messageDispatcher.Send(new OpenInformationWindowRequest(FlightViewModel), CancellationToken.None);
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
            messageDispatcher.Send(
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
        return !FlightViewModel.IsDummy;
    }

    [RelayCommand]
    void ChangeRunway(string runwayIdentifier)
    {
        try
        {
            messageDispatcher.Send(
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
            messageDispatcher.Send(
                new OpenInsertFlightWindowRequest(
                    FlightViewModel.DestinationIdentifier,
                    new RelativeInsertionOptions(FlightViewModel.Callsign, RelativePosition.Before),
                    maestroViewModel.Flights.Where(f => f.State == State.Landed).ToArray(),
                    maestroViewModel.Flights.Where(f => f.State == State.Pending).ToArray()),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    bool CanInsertFlightBefore()
    {
        return FlightViewModel.State != State.Frozen;
    }

    [RelayCommand]
    void InsertFlightAfter()
    {
        try
        {
            messageDispatcher.Send(
                new OpenInsertFlightWindowRequest(
                    FlightViewModel.DestinationIdentifier,
                    new RelativeInsertionOptions(FlightViewModel.Callsign, RelativePosition.Before),
                    maestroViewModel.Flights.Where(f => f.State == State.Landed).ToArray(),
                    maestroViewModel.Flights.Where(f => f.State == State.Pending).ToArray()),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void InsertSlotBefore()
    {
        try
        {
            messageDispatcher.Send(
                new BeginSlotCreationRequest(
                    FlightViewModel.DestinationIdentifier,
                    [FlightViewModel.AssignedRunway],
                    FlightViewModel.LandingTime,
                    SlotCreationReferencePoint.Before),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void InsertSlotAfter()
    {
        try
        {
            messageDispatcher.Send(
                new BeginSlotCreationRequest(
                    FlightViewModel.DestinationIdentifier,
                    [FlightViewModel.AssignedRunway],
                    FlightViewModel.LandingTime,
                    SlotCreationReferencePoint.After),
                CancellationToken.None);
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
            messageDispatcher.Send(
                new OpenChangeFeederFixEstimateWindowRequest(
                    FlightViewModel.DestinationIdentifier,
                    FlightViewModel.Callsign,
                    FlightViewModel.FeederFixIdentifier!,
                    FlightViewModel.FeederFixEstimate!.Value),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    bool CanChangeEta()
    {
        return !string.IsNullOrEmpty(FlightViewModel.FeederFixIdentifier) && FlightViewModel.FeederFixEstimate != null;
    }

    [RelayCommand]
    void Remove()
    {
        try
        {
            messageDispatcher.Send(
                new RemoveRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void Desequence()
    {
        try
        {
            messageDispatcher.Send(
                new DesequenceRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void MakePending()
    {
        try
        {
            messageDispatcher.Send(
                new MakePendingRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void ZeroDelay()
    {
        try
        {
            messageDispatcher.Send(
                new ZeroDelayRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void Coordination()
    {
        try
        {
            messageDispatcher.Send(
                new OpenCoordinationWindowRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }
}
