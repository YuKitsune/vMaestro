using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Contracts.Runway;
using Maestro.Contracts.Sessions;
using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class TerminalConfigurationViewModel : ObservableObject
{
    readonly string _airportIdentifier;
    readonly IClock _clock;
    readonly IMediator _mediator;
    readonly IWindowHandle _windowHandle;
    readonly IErrorReporter _errorReporter;
    readonly AirportConfiguration _airportConfiguration;
    readonly WindDto _surfaceWind;

    [ObservableProperty]
    string _originalRunwayModeIdentifier;

    [ObservableProperty]
    string _selectedRunwayModeIdentifier;

    [ObservableProperty]
    RunwayModeViewModel _selectedRunwayMode;

    [ObservableProperty]
    RunwayConfigurationItemViewModel[] _runwayConfigurationItems = [];

    [ObservableProperty]
    bool _changeImmediately;

    [ObservableProperty]
    DateTimeOffset _lastLandingTime;

    [ObservableProperty]
    DateTimeOffset _firstLandingTime;

    public RunwayModeViewModel[] AvailableRunwayModes { get; }

    public double MinimumLandingRateSeconds => 30;
    public double MaximumLandingRateSeconds => 60 * 5; // 5 Minutes

    public TerminalConfigurationViewModel(
        string airportIdentifier,
        RunwayModeViewModel[] availableRunwayModes,
        RunwayModeViewModel currentRunwayMode,
        RunwayModeViewModel? nextTerminalConfiguration,
        DateTimeOffset lastLandingTimeForOldMode,
        DateTimeOffset firstLandingTimeForNewMode,
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

        OriginalRunwayModeIdentifier = currentRunwayMode.Identifier;

        AvailableRunwayModes = availableRunwayModes;
        SelectedRunwayMode = nextTerminalConfiguration ?? currentRunwayMode;
        SelectedRunwayModeIdentifier = SelectedRunwayMode.Identifier;

        LastLandingTime = lastLandingTimeForOldMode;
        FirstLandingTime = firstLandingTimeForNewMode;

        RunwayConfigurationItems = CreateRunwayConfigurationItems(SelectedRunwayMode);
    }

    partial void OnSelectedRunwayModeIdentifierChanged(string value)
    {
        // When selection changes in UI, update SelectedRunwayMode to the copy of the matching template
        var template = AvailableRunwayModes.FirstOrDefault(r => r.Identifier == value);
        if (template != null)
        {
            SelectedRunwayMode = new RunwayModeViewModel(template);
            RunwayConfigurationItems = CreateRunwayConfigurationItems(SelectedRunwayMode);
        }
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
    void ChangeRunwayMode()
    {
        try
        {
            var runwayModeDto = new RunwayModeDto(
                SelectedRunwayMode.Identifier,
                RunwayConfigurationItems.Select(r =>
                    new RunwayDto(r.Identifier, r.ApproachType, r.LandingRateSeconds, r.FeederFixes)).ToArray(),
                SelectedRunwayMode.DependencyRateSeconds,
                SelectedRunwayMode.OffModeSeparationSeconds);

            _mediator.Send(
                new ChangeRunwayModeRequest(
                    _airportIdentifier,
                    runwayModeDto,
                    LastLandingTime,
                    FirstLandingTime),
                CancellationToken.None);

            CloseWindow();
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError( ex);
        }
    }

    [RelayCommand]
    void CloseWindow()
    {
        _windowHandle.Close();
    }
}
