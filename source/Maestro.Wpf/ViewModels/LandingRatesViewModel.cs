using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Maestro.Contracts.Runway;
using Maestro.Contracts.Sessions;
using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class LandingRatesViewModel : ObservableObject
{
    readonly string _airportIdentifier;
    readonly IClock _clock;
    readonly IMediator _mediator;
    readonly IWindowHandle _windowHandle;
    readonly IErrorReporter _errorReporter;
    readonly AirportConfiguration _airportConfiguration;
    readonly WindDto _surfaceWind;

    [ObservableProperty]
    RunwayModeViewModel _selectedRunwayMode;

    [ObservableProperty]
    RunwayConfigurationItemViewModel[] _runwayConfigurationItems = [];

    [ObservableProperty]
    DateTimeOffset _changeTime;

    [ObservableProperty]
    bool _hasPendingChange;

    public double MinimumLandingRateSeconds => 30;
    public double MaximumLandingRateSeconds => 60 * 5; // 5 Minutes

    public LandingRatesViewModel(
        string airportIdentifier,
        RunwayModeViewModel[] availableRunwayModes,
        RunwayModeViewModel currentRunwayMode,
        RunwayModeViewModel? nextTerminalConfiguration,
        DateTimeOffset changeTime,
        AirportConfiguration airportConfiguration,
        WindDto surfaceWind,
        IMediator mediator,
        IWindowHandle windowHandle,
        IClock clock,
        IErrorReporter errorReporter)
    {
        _airportIdentifier = airportIdentifier;
        _mediator = mediator;
        _windowHandle = windowHandle;
        _clock = clock;
        _errorReporter = errorReporter;
        _airportConfiguration = airportConfiguration;
        _surfaceWind = surfaceWind;

        HasPendingChange = nextTerminalConfiguration != null;
        SelectedRunwayMode = nextTerminalConfiguration ?? currentRunwayMode;

        ChangeTime = changeTime;

        RunwayConfigurationItems = CreateRunwayConfigurationItems(SelectedRunwayMode);

        WeakReferenceMessenger.Default.Register<SessionUpdatedNotification>(this, (_, notification) =>
        {
            if (notification.AirportIdentifier != _airportIdentifier)
                return;

            var pendingRatesChange = notification.Session.Sequence.PendingConfigurationChange as LandingRatesChangeDto;

            HasPendingChange = pendingRatesChange is not null;
            ChangeTime = pendingRatesChange is null
                ? _clock.UtcNow()
                : pendingRatesChange.ChangeTime;
        });
    }

    RunwayConfigurationItemViewModel[] CreateRunwayConfigurationItems(RunwayModeViewModel runwayMode)
    {
        return runwayMode.Runways
            .Select(r => new RunwayConfigurationItemViewModel(
                r.Identifier,
                r.ApproachType,
                r.LandingRateSeconds,
                r.FeederFixes,
                _airportConfiguration,
                _surfaceWind))
            .ToArray();
    }

    [RelayCommand]
    void ChangeLandingRates()
    {
        try
        {
            var newLandingRates = RunwayConfigurationItems
                .ToDictionary(r => r.Identifier, r => TimeSpan.FromSeconds(r.LandingRateSeconds));

            _mediator.Send(
                new ChangeLandingRatesRequest(_airportIdentifier, newLandingRates, ChangeTime),
                CancellationToken.None);

            CloseWindow();
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError( ex);
        }
    }

    [RelayCommand]
    void CancelPendingChanges()
    {
        try
        {
            _mediator.Send(new CancelConfigurationChangeRequest(_airportIdentifier), CancellationToken.None);
            CloseWindow();
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void CloseWindow()
    {
        _windowHandle.Close();
    }
}
