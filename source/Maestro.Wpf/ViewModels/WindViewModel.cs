using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Contracts.Sessions;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class WindViewModel : ObservableObject
{
    readonly IMediator _mediator;
    readonly IWindowHandle _windowHandle;
    readonly IErrorReporter _errorReporter;

    readonly string _airportIdentifier;

    [ObservableProperty]
    int _surfaceWindDirection;

    [ObservableProperty]
    int _surfaceWindSpeed;

    [ObservableProperty]
    int _upperWindDirection;

    [ObservableProperty]
    int _upperWindSpeed;

    [ObservableProperty]
    int _upperWindAltitude;

    public WindViewModel(
        string airportIdentifier,
        WindDto surfaceWind,
        WindDto upperWind,
        int upperWindAltitude,
        IMediator mediator,
        IWindowHandle windowHandle,
        IErrorReporter errorReporter)
    {
        _airportIdentifier = airportIdentifier;
        SurfaceWindDirection = surfaceWind.Direction;
        SurfaceWindSpeed = surfaceWind.Speed;
        UpperWindDirection = upperWind.Direction;
        UpperWindSpeed = upperWind.Speed;
        UpperWindAltitude = upperWindAltitude;

        _mediator = mediator;
        _windowHandle = windowHandle;
        _errorReporter = errorReporter;
    }

    [RelayCommand]
    public async Task ChangeWind()
    {
        try
        {
            await _mediator.Send(
                new UpdateWindRequest(
                    _airportIdentifier,
                    new WindDto(SurfaceWindDirection, SurfaceWindSpeed),
                    new WindDto(UpperWindDirection, UpperWindSpeed),
                    ManualWind: true),
                CancellationToken.None);

            _windowHandle.Close();
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    public void CloseWindow()
    {
        _windowHandle.Close();
    }
}
