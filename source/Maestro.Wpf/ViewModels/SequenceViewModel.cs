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
    [NotifyPropertyChangedFor(nameof(Desequenced))]
    List<FlightViewModel> _flights = [];

    public string AirportIdentifier { get; }

    public string TerminalConfiguration =>
        NextRunwayMode is not null
            ? $"{CurrentRunwayMode.Identifier} → {NextRunwayMode.Identifier}"
            : CurrentRunwayMode.Identifier;

    public bool RunwayChangeIsPlanned => NextRunwayMode is not null;

    public string[] Desequenced => Flights.Where(f => f.State == State.Desequenced).Select(airport => airport.Callsign).ToArray();

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
    void OpenDesequencedWindow() => _mediator.Send(new OpenDesequencedWindowRequest(AirportIdentifier, Desequenced));

    public void UpdateFlight(FlightMessage flight)
    {
        var flights = Flights.ToList();
        var index = flights.FindIndex(f => f.Callsign == flight.Callsign);
        var viewModel = new FlightViewModel(flight);
        if (index != -1)
        {
            flights[index] = viewModel;
        }
        else
        {
            flights.Add(viewModel);
        }

        Flights = flights;
    }

    public void RemoveFlight(string callsign)
    {
        var flights = Flights.ToList();
        var index = flights.FindIndex(f => f.Callsign == callsign);
        if (index != -1)
        {
            flights.RemoveAt(index);
        }

        Flights = flights;
    }
}
