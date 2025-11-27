using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Messages;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class CoordinationViewModel : ObservableObject
{
    const string AllPeers = "ALL";

    readonly IWindowHandle _windowHandle;
    readonly IMediator _mediator;
    readonly IErrorReporter _errorReporter;
    readonly string _airportIdentifier;
    readonly string? _flightCallsign;

    [ObservableProperty]
    ObservableCollection<string> _messages = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    string? _selectedMessage;

    [ObservableProperty]
    ObservableCollection<string> _destinations = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    string? _selectedDestination;

    public bool IsFlightSpecific => _flightCallsign is not null;
    public bool ShowSendButton => IsFlightSpecific;
    public bool ShowDestinationSelector => !IsFlightSpecific;

    public CoordinationViewModel(
        string airportIdentifier,
        string? flightCallsign,
        IWindowHandle windowHandle,
        IMediator mediator,
        IErrorReporter errorReporter,
        string[] messageTemplates,
        string[] peers)
    {
        _airportIdentifier = airportIdentifier;
        _flightCallsign = flightCallsign;
        _windowHandle = windowHandle;
        _mediator = mediator;
        _errorReporter = errorReporter;
        Messages = new ObservableCollection<string>(messageTemplates);
        Destinations = [AllPeers, ..peers.Distinct()]; // BUG: We somehow end up with duplicate peers.

        if (IsFlightSpecific)
            SelectedDestination = AllPeers;
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    async Task Send()
    {
        if (SelectedMessage is null)
            return;

        try
        {
            CoordinationDestination destination = IsFlightSpecific || SelectedDestination == AllPeers
                ? new CoordinationDestination.Broadcast()
                : new CoordinationDestination.Controller(SelectedDestination!);

            await _mediator.Send(
                new SendCoordinationMessageRequest(
                    _airportIdentifier,
                    DateTimeOffset.UtcNow, // TODO: Use a clock
                    SelectedMessage,
                    destination),
                CancellationToken.None);

            _windowHandle.Close();
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    async Task DestinationSelected() => await Send();

    bool CanSend()
    {
        if (SelectedMessage is null)
            return false;

        if (!IsFlightSpecific && SelectedDestination is null)
            return false;

        return true;
    }

    [RelayCommand]
    void Cancel()
    {
        _windowHandle.Close();
    }
}
