using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Configuration;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public record ApproachTypeLookup(string FeederFix, string Runway, string ApproachType);

public partial class FlightLabelViewModel(
    IMediator mediator,
    IErrorReporter errorReporter,
    MaestroViewModel maestroViewModel,
    FlightMessage flightViewModel,
    string[] availableRunways,
    ApproachTypeLookup[] availableApproachTypes)
    : ObservableObject
{
    const string None = "NONE";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvailableRunways))]
    [NotifyPropertyChangedFor(nameof(AvailableApproachTypes))]
    [NotifyPropertyChangedFor(nameof(CanChangeApproachType))]
    [NotifyPropertyChangedFor(nameof(Indicators))]
    [NotifyCanExecuteChangedFor(nameof(ShowInformationWindowCommand))]
    FlightMessage _flightViewModel = flightViewModel;

    [ObservableProperty]
    bool _isSelected = false;

    public string Indicators => BuildIndicators();

    public string[] AvailableRunways => availableRunways.Where(r => r != FlightViewModel.AssignedRunwayIdentifier).ToArray();

    public string[] AvailableApproachTypes => availableApproachTypes.Where(a =>
            a.FeederFix == FlightViewModel.FeederFixIdentifier &&
            a.Runway == FlightViewModel.AssignedRunwayIdentifier &&
            a.ApproachType != FlightViewModel.ApproachType)
        .Select(a => string.IsNullOrEmpty(a.ApproachType) ? None : a.ApproachType)
        .ToArray();

    public bool CanChangeApproachType => AvailableApproachTypes.Any();

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

    [RelayCommand]
    void ChangeApproachType(string approachType)
    {
        try
        {
            if (approachType == None)
                approachType = string.Empty;

            mediator.Send(
                new ChangeApproachTypeRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign, approachType),
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
                    StartTime: FlightViewModel.LandingTime.Subtract(TimeSpan.FromMinutes(6)),
                    EndTime: FlightViewModel.LandingTime.Subtract(TimeSpan.FromMinutes(1)),
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

    [ObservableProperty]
    ObservableCollection<LabelItemViewModel> _labelItems = [];

    public void UpdateLabelItems(LabelLayoutConfigurationV2 labelLayout, FlightMessage flight, int ladderIndex)
    {
        LabelItems.Clear();

        // Determine if this ladder uses right-to-left rendering (odd-indexed ladders)
        bool rightToLeft = ladderIndex % 2 == 1;

        foreach (var itemConfig in labelLayout.Items)
        {
            var content = GetItemContent(itemConfig, flight);
            var color = GetItemColor(itemConfig, flight);

            LabelItems.Add(new LabelItemViewModel
            {
                Content = content,
                Foreground = color,
                Width = itemConfig.Width,
                Padding = itemConfig.Padding,
                RightToLeft = rightToLeft
            });
        }
    }

    string GetItemContent(LabelItemConfigurationV2 itemConfig, FlightMessage flight)
    {
        return itemConfig switch
        {
            CallsignItemConfigurationV2 => flight.Callsign,
            AircraftTypeItemConfigurationV2 => flight.AircraftType,
            AircraftWakeCategoryItemConfigurationV2 => flight.WakeCategory.ToString(),
            RunwayItemConfigurationV2 => flight.AssignedRunwayIdentifier ?? "",
            ApproachTypeItemConfigurationV2 => flight.ApproachType ?? "",
            LandingTimeItemConfigurationV2 => flight.LandingTime.ToString("mm"),
            FeederFixTimeItemConfigurationV2 => flight.FeederFixEstimate?.ToString("mm") ?? "",
            RequiredDelayItemConfigurationV2 => flight.RemainingDelay.TotalMinutes.ToString("00"),
            RemainingDelayItemConfigurationV2 => flight.RemainingDelay.TotalMinutes.ToString("00"),
            ManualDelayItemConfigurationV2 manual => FormatManualDelay(flight, manual.ZeroDelaySymbol, manual.ManualDelaySymbol),
            ProfileSpeedItemConfigurationV2 profile => flight.FlowControls == FlowControls.ProfileSpeed ? profile.Symbol : "",
            CouplingStatusItemConfigurationV2 coupling => flight.Position is not null ? "" : coupling.UncoupledSymbol,
            _ => ""
        };
    }

    string FormatManualDelay(FlightMessage flight, string zeroDelaySymbol, string manualDelaySymbol)
    {
        if (flight.MaximumDelay == null)
            return "";

        return flight.MaximumDelay == TimeSpan.Zero
            ? zeroDelaySymbol
            : manualDelaySymbol;
    }

    Color GetItemColor(LabelItemConfigurationV2 itemConfig, FlightMessage flight)
    {
        // TODO: Implement color selection based on itemConfig.ColourSources
        // For now, return white as placeholder
        return Colors.White;
    }
}

public class LabelItemViewModel
{
    public required string Content { get; set; }
    public required Color Foreground { get; set; }
    public required int Width { get; set; }
    public required int Padding { get; set; }
    public required bool RightToLeft { get; set; }

    public string PaddedText
    {
        get
        {
            // Truncate or pad content to exact width
            var text = Content.Length > Width
                ? Content.Substring(0, Width)
                : Content.PadRight(Width);

            // Apply padding characters
            var padding = new string(' ', Padding);

            return RightToLeft
                ? padding + text
                : text + padding;
        }
    }
}
