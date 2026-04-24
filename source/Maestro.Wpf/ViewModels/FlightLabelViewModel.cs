using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Maestro.Contracts.Flights;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Model;
using Maestro.Wpf.Contracts;
using Maestro.Wpf.Integrations;
using MediatR;
using Color = Maestro.Core.Configuration.Color;

namespace Maestro.Wpf.ViewModels;

public partial class FlightLabelViewModel : ObservableObject
{
    const string None = "NONE";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvailableRunways))]
    [NotifyPropertyChangedFor(nameof(AvailableApproachTypes))]
    [NotifyPropertyChangedFor(nameof(CanChangeApproachType))]
    [NotifyPropertyChangedFor(nameof(Indicators))]
    [NotifyCanExecuteChangedFor(nameof(ShowInformationWindowCommand))]
    FlightDto _flightViewModel;

    [ObservableProperty]
    bool _isSelected = false;

    [ObservableProperty]
    bool _isVatsysTrackSelected = false;

    public string Indicators => BuildIndicators();

    public string[] AvailableRunways => _availableRunways.Where(r => r != FlightViewModel.AssignedRunwayIdentifier).ToArray();

    public string[] AvailableApproachTypes => _availableApproachTypes.Where(a => a != FlightViewModel.ApproachType).ToArray();

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

        if (FlightViewModel.HighSpeed)
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
            _mediator.Send(new OpenInformationWindowRequest(FlightViewModel));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
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
            _mediator.Send(
                new RecomputeRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
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
            _mediator.Send(
                new ChangeRunwayRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign, runwayIdentifier),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void ChangeApproachType(string approachType)
    {
        try
        {
            if (approachType == None)
                approachType = string.Empty;

            _mediator.Send(
                new ChangeApproachTypeRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign, approachType),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanInsertFlightBefore))]
    void InsertFlightBefore()
    {
        try
        {
            _mediator.Send(
                new OpenInsertFlightWindowRequest(
                    FlightViewModel.DestinationIdentifier,
                    new RelativeInsertionOptions(FlightViewModel.Callsign, RelativePosition.Before),
                    _maestroViewModel.Flights.Where(f => f.State == State.Landed).ToArray(),
                    _maestroViewModel.PendingFlights.ToArray()));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
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
            _mediator.Send(
                new OpenInsertFlightWindowRequest(
                    FlightViewModel.DestinationIdentifier,
                    new RelativeInsertionOptions(FlightViewModel.Callsign, RelativePosition.After),
                    _maestroViewModel.Flights.Where(f => f.State == State.Landed).ToArray(),
                    _maestroViewModel.PendingFlights.ToArray()));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void InsertSlotBefore()
    {
        try
        {
            var runways = FlightViewModel.AssignedRunwayIdentifier != null
                ? [FlightViewModel.AssignedRunwayIdentifier]
                : Array.Empty<string>();

            _mediator.Send(
                new OpenSlotWindowRequest(
                    FlightViewModel.DestinationIdentifier,
                    SlotId: null, // slotId is null for new slots
                    StartTime: FlightViewModel.LandingTime.Subtract(TimeSpan.FromMinutes(6)),
                    EndTime: FlightViewModel.LandingTime.Subtract(TimeSpan.FromMinutes(1)),
                    runways));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void InsertSlotAfter()
    {
        try
        {
            var runways = FlightViewModel.AssignedRunwayIdentifier != null
                ? [FlightViewModel.AssignedRunwayIdentifier]
                : Array.Empty<string>();

            _mediator.Send(
                new OpenSlotWindowRequest(
                    FlightViewModel.DestinationIdentifier,
                    SlotId: null, // slotId is null for new slots
                    StartTime: FlightViewModel.LandingTime.Add(TimeSpan.FromMinutes(1)),
                    EndTime: FlightViewModel.LandingTime.Add(TimeSpan.FromMinutes(6)),
                    runways));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanChangeEta))]
    void ChangeEta()
    {
        try
        {
            _mediator.Send(
                new OpenChangeFeederFixEstimateWindowRequest(
                    FlightViewModel.DestinationIdentifier,
                    FlightViewModel.Callsign,
                    FlightViewModel.FeederFixIdentifier!,
                    FlightViewModel.FeederFixEstimate));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
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
            _mediator.Send(
                new RemoveRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void Desequence()
    {
        try
        {
            _mediator.Send(
                new DesequenceRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void MakePending()
    {
        try
        {
            _mediator.Send(
                new MakePendingRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
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

            _mediator.Send(
                new ManualDelayRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign, maximumDelayMinutes),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void Coordination()
    {
        try
        {
            _mediator.Send(new OpenCoordinationWindowRequest(FlightViewModel.DestinationIdentifier, FlightViewModel.Callsign));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [ObservableProperty]
    ObservableCollection<LabelItemViewModel> _labelItems = [];

    readonly IMediator _mediator;
    readonly IErrorReporter _errorReporter;
    readonly MaestroViewModel _maestroViewModel;
    readonly GlobalColourConfiguration _globalColorConfiguration;
    readonly string[] _availableRunways;
    readonly string[] _availableApproachTypes;

    public FlightLabelViewModel(IMediator mediator,
        IErrorReporter errorReporter,
        MaestroViewModel maestroViewModel,
        GlobalColourConfiguration globalColorConfiguration,
        FlightDto flightViewModel,
        string[] availableRunways,
        string[] availableApproachTypes,
        string? vatsysSelectedTrackCallsign = null)
    {
        _mediator = mediator;
        _errorReporter = errorReporter;
        _maestroViewModel = maestroViewModel;
        _globalColorConfiguration = globalColorConfiguration;
        _availableRunways = availableRunways;
        _availableApproachTypes = availableApproachTypes;
        _flightViewModel = flightViewModel;
        _isVatsysTrackSelected = vatsysSelectedTrackCallsign == flightViewModel.Callsign;

        WeakReferenceMessenger.Default.Register<VatsysTrackSelectedNotification>(this, (_, m) =>
            IsVatsysTrackSelected = m.Callsign == FlightViewModel.Callsign);
    }

    public void UpdateLabelItems(LabelLayoutConfiguration labelLayout, FlightDto flight, int ladderIndex)
    {
        LabelItems.Clear();

        // Determine if this ladder uses right-to-left rendering (even-indexed ladders)
        var rightToLeft = ladderIndex % 2 == 0;
        if (rightToLeft)
        {
            for (var i = labelLayout.Items.Length - 1; i >= 0; i--)
            {
                ToLabelItemViewModel(labelLayout.Items[i]);
            }
        }
        else
        {
            for (var i = 0; i < labelLayout.Items.Length; i++)
            {
                ToLabelItemViewModel(labelLayout.Items[i]);
            }
        }

        void ToLabelItemViewModel(LabelItemConfiguration item)
        {
            var content = GetItemContent(item, flight);
            var color = GetItemColor(item, flight);

            LabelItems.Add(new LabelItemViewModel
            {
                Content = content,
                Foreground = new SolidColorBrush(color),
                Width = item.Width,
                Padding = item.Padding,
                RightToLeft = rightToLeft
            });
        }
    }

    string GetItemContent(LabelItemConfiguration itemConfig, FlightDto flight)
    {
        return itemConfig switch
        {
            CallsignItemConfiguration => flight.Callsign,
            AircraftTypeItemConfiguration => flight.AircraftType,
            AircraftWakeCategoryItemConfiguration => flight.WakeCategory switch
            {
                WakeCategory.Light => "L",
                WakeCategory.Medium => "M",
                WakeCategory.Heavy => "H",
                WakeCategory.SuperHeavy => "J",
                _ => "?"
            },
            RunwayItemConfiguration => flight.AssignedRunwayIdentifier,
            ApproachTypeItemConfiguration => flight.ApproachType,
            LandingTimeItemConfiguration => flight.LandingTime.ToString("mm"),
            FeederFixTimeItemConfiguration => flight.FeederFixTime.ToString("mm"),
            RequiredDelayItemConfiguration required => FormatDelay(flight.RequiredEnrouteDelay, flight.RequiredTerminalDelay, required.Component),
            RemainingDelayItemConfiguration remaining => FormatDelay(flight.RemainingEnrouteDelay, flight.RemainingTerminalDelay, remaining.Component),
            ManualDelayItemConfiguration manual => FormatManualDelay(flight, manual.ZeroDelaySymbol, manual.ManualDelaySymbol),
            HighSpeedItemConfiguration highSpeed => flight.HighSpeed ? highSpeed.Symbol : "",
            CouplingStatusItemConfiguration coupling => flight.Position is not null ? "" : coupling.UncoupledSymbol,
            _ => "?"
        };
    }

    static string FormatDelay(TimeSpan enroute, TimeSpan tma, DelayComponent component)
    {
        var value = component switch
        {
            DelayComponent.Enroute => enroute,
            DelayComponent.Tma => tma,
            _ => enroute + tma
        };

        return ((int)value.TotalMinutes).ToString(CultureInfo.InvariantCulture);
    }

    string FormatManualDelay(FlightDto flight, string zeroDelaySymbol, string manualDelaySymbol)
    {
        if (flight.MaximumDelay == null)
            return "";

        return flight.MaximumDelay == TimeSpan.Zero
            ? zeroDelaySymbol
            : manualDelaySymbol;
    }

    // TODO: Reconsider allowing users to customise what colours each label item.
    //  Hard code what colours which item. Allow users to customise the specific colours.
    System.Windows.Media.Color GetItemColor(LabelItemConfiguration itemConfig, FlightDto flight)
    {
        foreach (var itemConfigColourSource in itemConfig.ColourSources)
        {
            var color = itemConfigColourSource switch
            {
                LabelItemColourSource.Runway when _maestroViewModel.AirportConfiguration.Colours is not null => GetColorByRunway(_maestroViewModel.AirportConfiguration.Colours, flight),
                LabelItemColourSource.ApproachType when _maestroViewModel.AirportConfiguration.Colours is not null => GetColorByApproachType(_maestroViewModel.AirportConfiguration.Colours, flight),
                LabelItemColourSource.FeederFix when _maestroViewModel.AirportConfiguration.Colours is not null => GetColorByFeederFix(_maestroViewModel.AirportConfiguration.Colours, flight),
                LabelItemColourSource.State => GetColorByState(_globalColorConfiguration, flight),
                LabelItemColourSource.RunwayMode => GetColorByRunwayMode(_globalColorConfiguration, flight),
                LabelItemColourSource.RequiredControlAction => GetColorByRequiredControlAction(itemConfig, _globalColorConfiguration, flight),
                LabelItemColourSource.RemainingControlAction => GetColorByRemainingControlAction(itemConfig, _globalColorConfiguration, flight),
                _ => null
            };

            if (color.HasValue)
            {
                return color.Value;
            }
        }

        // Default to interactive text
        return Theme.InteractiveTextColor.Color;
    }

    System.Windows.Media.Color? GetColorByRunway(AirportColourConfiguration airportColourConfiguration, FlightDto flight)
    {
        if (airportColourConfiguration.Runways.TryGetValue(flight.AssignedRunwayIdentifier, out var runwayColour))
        {
            return ToColor(runwayColour);
        }

        return null;
    }

    System.Windows.Media.Color? GetColorByApproachType(AirportColourConfiguration airportColourConfiguration, FlightDto flight)
    {
        if (airportColourConfiguration.ApproachTypes.TryGetValue(flight.ApproachType, out var approachColor))
        {
            return ToColor(approachColor);
        }

        return null;
    }

    System.Windows.Media.Color? GetColorByFeederFix(AirportColourConfiguration airportColourConfiguration, FlightDto flight)
    {
        if (!string.IsNullOrEmpty(flight.FeederFixIdentifier) &&
            airportColourConfiguration.FeederFixes.TryGetValue(flight.FeederFixIdentifier, out var feederFixColor))
        {
            return ToColor(feederFixColor);
        }

        return null;
    }

    System.Windows.Media.Color? GetColorByState(GlobalColourConfiguration globalColourConfiguration, FlightDto flight)
    {
        if (globalColourConfiguration.States.TryGetValue(flight.State, out var stateColor))
        {
            return ToColor(stateColor);
        }

        return null;
    }

    System.Windows.Media.Color? GetColorByRequiredControlAction(LabelItemConfiguration labelItemConfiguration, GlobalColourConfiguration globalColourConfiguration, FlightDto flight)
    {
        if (globalColourConfiguration.ControlActions.TryGetValue(flight.RequiredControlAction, out var actionColor))
            return ToColor(actionColor);

        return null;
    }

    System.Windows.Media.Color? GetColorByRemainingControlAction(LabelItemConfiguration labelItemConfiguration, GlobalColourConfiguration globalColourConfiguration, FlightDto flight)
    {
        if (globalColourConfiguration.ControlActions.TryGetValue(flight.RemainingControlAction, out var actionColor))
            return ToColor(actionColor);

        return null;
    }

    System.Windows.Media.Color? GetColorByRunwayMode(GlobalColourConfiguration globalColourConfiguration, FlightDto flight)
    {
        // TODO: Need to know what the current runway mode is, and whether the flight has been processed in the current one
        //  or the next one.
        return null;
    }

    System.Windows.Media.Color ToColor(Color config) => System.Windows.Media.Color.FromArgb(255, (byte)config.Red, (byte)config.Green, (byte)config.Blue);
}

public class LabelItemViewModel
{
    public required string Content { get; set; }
    public required Brush Foreground { get; set; }
    public required int Width { get; set; }
    public required int Padding { get; set; }
    public required bool RightToLeft { get; set; }

    public string PaddedText
    {
        get
        {
            // Truncate or pad content to exact width
            string text;
            if (Content.Length > Width)
            {
                text = Content.Substring(0, Width);
            }
            else
            {
                // For RTL, pad on the left (spaces before content)
                // For LTR, pad on the right (spaces after content)
                text = RightToLeft
                    ? Content.PadLeft(Width)
                    : Content.PadRight(Width);
            }

            // Apply padding characters
            var padding = new string(' ', Padding);

            // Odd ladders (RightToLeft=false) should have padding on right (text + padding)
            // Even ladders (RightToLeft=true) should have padding on left (padding + text)
            return RightToLeft
                ? padding + text
                : text + padding;
        }
    }
}
