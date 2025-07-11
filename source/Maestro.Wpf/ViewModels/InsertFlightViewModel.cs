using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Messages;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class InsertFlightViewModel : ObservableObject
{
    readonly IWindowHandle _windowHandle;
    readonly IMediator _mediator;

    [ObservableProperty]
    string _selectedCallsign = string.Empty;

    [ObservableProperty]
    List<string> _overshoot = [];

    [ObservableProperty]
    List<string> _pending = [];

    public string AirportIdentifier { get; set; }
    public InsertionPoint InsertionPoint { get;  }
    public string RelativeCallsign { get; }

    public InsertFlightViewModel(
        string airportIdentifier,
        InsertionPoint insertionPoint,
        string relativeCallsign,
        string[] overshootCallsigns,
        string[] pendingCallsigns,
        IWindowHandle windowHandle,
        IMediator mediator)
    {
        AirportIdentifier = airportIdentifier;
        InsertionPoint = insertionPoint;
        RelativeCallsign = relativeCallsign;

        Overshoot = overshootCallsigns.ToList();
        Pending = pendingCallsigns.ToList();

        _windowHandle = windowHandle;
        _mediator = mediator;
    }

    [RelayCommand]
    async Task Insert()
    {
        IRequest request;
        if (Overshoot.Contains(SelectedCallsign))
        {
            request = new InsertOvershootFlightRequest(
                AirportIdentifier,
                SelectedCallsign,
                InsertionPoint,
                RelativeCallsign);
        }
        else
        {
            request = new InsertPendingFlightRequest(
                AirportIdentifier,
                SelectedCallsign,
                InsertionPoint,
                RelativeCallsign);
        }

        await _mediator.Send(request);
        CloseWindow();
    }

    [RelayCommand]
    void CloseWindow()
    {
        _windowHandle.Close();
    }
}
