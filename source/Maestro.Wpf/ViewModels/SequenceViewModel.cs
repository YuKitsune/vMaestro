using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Wpf.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class SequenceViewModel : ObservableObject
{
    readonly IMediator _mediator;

    // TODO: Use a ViewModel
    [ObservableProperty]
    ViewConfiguration[] _views = [];

    [ObservableProperty]
    ViewConfiguration _selectedView;

    [ObservableProperty]
    RunwayModeViewModel[] _runwayModes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TerminalConfiguration))]
    RunwayModeViewModel _currentRunwayMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TerminalConfiguration))]
    [NotifyPropertyChangedFor(nameof(RunwayChangeIsPlanned))]
    RunwayModeViewModel? _nextRunwayMode;

    [ObservableProperty]
    List<FlightViewModel> _flights = [];

    [ObservableProperty]
    List<string> _desequencedFlights = [];

    [ObservableProperty]
    List<string> _landedFlights = [];

    [ObservableProperty]
    List<string> _pendingFlights = [];

    [ObservableProperty]
    SlotMessage[] _slots = [];

    public string AirportIdentifier { get; }

    public string TerminalConfiguration =>
        NextRunwayMode is not null
            ? $"{CurrentRunwayMode.Identifier} → {NextRunwayMode.Identifier}"
            : CurrentRunwayMode.Identifier;

    public bool RunwayChangeIsPlanned => NextRunwayMode is not null;

    public SequenceViewModel(
        string airportIdentifier,
        ViewConfiguration[] views,
        RunwayModeDto[] runwayModes,
        SequenceMessage sequence,
        IMediator mediator)
    {
        _mediator = mediator;

        AirportIdentifier = airportIdentifier;
        Views = views;
        SelectedView = Views.First();

        RunwayModes = runwayModes.Select(r => new RunwayModeViewModel(r)).ToArray();
        CurrentRunwayMode = new RunwayModeViewModel(sequence.CurrentRunwayMode);
        NextRunwayMode = sequence.NextRunwayMode is null ? null : new RunwayModeViewModel(sequence.NextRunwayMode);

        Flights = sequence.Flights.Select(f => new FlightViewModel(f)).ToList();
        Slots = sequence.Slots;
    }

    [RelayCommand]
    void OpenTerminalConfiguration()
    {
        _mediator.Send(new OpenTerminalConfigurationRequest(AirportIdentifier));
    }

    [RelayCommand]
    void SelectView(ViewConfiguration viewConfiguration)
    {
        SelectedView = viewConfiguration;
    }

    [RelayCommand]
    void OpenDesequencedWindow() => _mediator.Send(new OpenDesequencedWindowRequest(AirportIdentifier, DesequencedFlights.ToArray()));

    public void UpdateFrom(SequenceMessage sequenceMessage)
    {
        CurrentRunwayMode = new RunwayModeViewModel(sequenceMessage.CurrentRunwayMode);
        NextRunwayMode = sequenceMessage.NextRunwayMode is not null
            ? new RunwayModeViewModel(sequenceMessage.NextRunwayMode)
            : null;

        Flights = sequenceMessage.Flights.Select(flight => new FlightViewModel(flight)).ToList();
        DesequencedFlights = sequenceMessage.DesequencedFlights.ToList();
        LandedFlights = sequenceMessage.LandedFlights.ToList();
        PendingFlights = sequenceMessage.PendingFlights.ToList();
        Slots = sequenceMessage.Slots;
    }
}
