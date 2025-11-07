using System.Text;
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
    string[] availableRunways)
    : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvailableRunways))]
    [NotifyPropertyChangedFor(nameof(Indicators))]
    [NotifyCanExecuteChangedFor(nameof(ShowInformationWindowCommand))]
    FlightMessage _flightViewModel = flightViewModel;

    [ObservableProperty]
    bool _isSelected = false;

    public string Indicators => BuildIndicators();

    public string[] AvailableRunways => availableRunways.Where(r => r != FlightViewModel.AssignedRunwayIdentifier).ToArray();

    public bool CanInsertBefore => FlightViewModel.State != State.Frozen;

    string BuildIndicators()
    {
        var sb = new StringBuilder();

        if (FlightViewModel.MaximumDelay is not null)
        {
            if (FlightViewModel.MaximumDelay == TimeSpan.Zero)
            {
                sb.Append("#");
            }
            else
            {
                sb.Append("%");
            }
        }
        else
        {
            sb.Append(" ");
        }

        if (FlightViewModel.FlowControls == FlowControls.ProfileSpeed)
        {
            sb.Append("+");
        }
        else
        {
            sb.Append(" ");
        }

        if (FlightViewModel.Position is null)
        {
            sb.Append("*");
        }
        else
        {
            sb.Append(" ");
        }

        return sb.ToString();
    }

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
        return !FlightViewModel.IsManuallyInserted;
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
        return !FlightViewModel.IsManuallyInserted;
    }

    [RelayCommand]
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
                    maestroViewModel.PendingFlights.ToArray()));
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
            mediator.Send(
                new OpenInsertFlightWindowRequest(
                    FlightViewModel.DestinationIdentifier,
                    new RelativeInsertionOptions(FlightViewModel.Callsign, RelativePosition.After),
                    maestroViewModel.Flights.Where(f => f.State == State.Landed).ToArray(),
                    maestroViewModel.PendingFlights.ToArray()));
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
            mediator.Send(
                new OpenSlotWindowRequest(
                    FlightViewModel.DestinationIdentifier,
                    SlotId: null, // slotId is null for new slots
                    StartTime: FlightViewModel.LandingTime.Subtract(TimeSpan.FromMinutes(5)),
                    EndTime: FlightViewModel.LandingTime,
                    [FlightViewModel.AssignedRunwayIdentifier]));
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
            mediator.Send(
                new OpenSlotWindowRequest(
                    FlightViewModel.DestinationIdentifier,
                    SlotId: null, // slotId is null for new slots
                    StartTime: FlightViewModel.LandingTime.Add(TimeSpan.FromMinutes(1)),
                    EndTime: FlightViewModel.LandingTime.Add(TimeSpan.FromMinutes(6)),
                    [FlightViewModel.AssignedRunwayIdentifier]));
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
        return !string.IsNullOrEmpty(FlightViewModel.FeederFixIdentifier) && FlightViewModel.FeederFixEstimate != null;
    }

    [RelayCommand]
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

    [RelayCommand]
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

    [RelayCommand]
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

    [RelayCommand]
    void ManualDelay(object parameter)
    {
        try
        {
            // Handle both string and int parameters from XAML
            var maximumDelayMinutes = parameter switch
            {
                int intValue => intValue,
                string strValue when int.TryParse(strValue, out var parsed) => parsed,
                _ => throw new ArgumentException($"Invalid parameter type: {parameter?.GetType().Name ?? "null"}")
            };

            mediator.Send(
                new ManualDelayRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign, maximumDelayMinutes),
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
            mediator.Send(new OpenCoordinationWindowRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign));
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }
}
