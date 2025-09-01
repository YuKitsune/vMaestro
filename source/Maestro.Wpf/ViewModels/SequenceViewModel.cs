using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class SequenceViewModel : ObservableObject
{
    readonly IMediator _mediator;
    readonly IErrorReporter _errorReporter;

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
    [NotifyPropertyChangedFor(nameof(HasDesequencedFlight))]
    List<FlightMessage> _flights = [];

    [ObservableProperty]
    SlotMessage[] _slots = [];

    public bool HasDesequencedFlight => Flights.Any(f => f.State == State.Desequenced);

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
        IMediator mediator,
        IErrorReporter errorReporter)
    {
        _mediator = mediator;
        _errorReporter = errorReporter;

        AirportIdentifier = airportIdentifier;
        Views = views;
        SelectedView = Views.First();

        RunwayModes = runwayModes.Select(r => new RunwayModeViewModel(r)).ToArray();
        CurrentRunwayMode = new RunwayModeViewModel(sequence.CurrentRunwayMode);
        NextRunwayMode = sequence.NextRunwayMode is null ? null : new RunwayModeViewModel(sequence.NextRunwayMode);

        Flights = sequence.Flights.ToList();
        Slots = sequence.Slots;
    }

    [RelayCommand]
    void OpenTerminalConfiguration()
    {
        try
        {
            _mediator.Send(new OpenTerminalConfigurationRequest(AirportIdentifier));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void SelectView(ViewConfiguration viewConfiguration)
    {
        SelectedView = viewConfiguration;
    }

    [RelayCommand]
    void OpenPendingDeparturesWindow()
    {
        try
        {
            _mediator.Send(new OpenPendingDeparturesWindowRequest(AirportIdentifier,
                Flights.Where(f => f.State == State.Pending).ToArray()));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void OpenDesequencedWindow()
    {
        try
        {
            _mediator.Send(
                new OpenDesequencedWindowRequest(
                    AirportIdentifier,
                    Flights.Where(f => f.State == State.Desequenced)
                        .Select(f => f.Callsign)
                        .ToArray()));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    public void UpdateFrom(SequenceMessage sequenceMessage)
    {
        CurrentRunwayMode = new RunwayModeViewModel(sequenceMessage.CurrentRunwayMode);
        NextRunwayMode = sequenceMessage.NextRunwayMode is not null
            ? new RunwayModeViewModel(sequenceMessage.NextRunwayMode)
            : null;

        Flights = sequenceMessage.Flights.ToList();
        Slots = sequenceMessage.Slots;
    }
}
