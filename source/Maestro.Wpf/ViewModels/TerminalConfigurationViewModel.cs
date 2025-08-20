using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Handlers;
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

    [ObservableProperty]
    string _originalRunwayModeIdentifier;

    [ObservableProperty]
    string _selectedRunwayModeIdentifier;

    [ObservableProperty]
    RunwayModeViewModel _selectedRunwayMode;

    [ObservableProperty]
    bool _changeImmediately;

    [ObservableProperty]
    DateTimeOffset _lastLandingTime;

    [ObservableProperty]
    DateTimeOffset _firstLandingTime;

    public RunwayModeDto[] AvailableRunwayModes { get; }

    // TODO: Make configurable
    public double MinimumLandingRateSeconds => 30;
    public double MaximumLandingRateSeconds => 60 * 5; // 5 Minutes

    public TerminalConfigurationViewModel(
        string airportIdentifier,
        RunwayModeDto[] availableRunwayModes,
        RunwayModeDto currentRunwayMode,
        RunwayModeDto? nextTerminalConfiguration,
        DateTimeOffset lastLandingTimeForOldMode,
        DateTimeOffset firstLandingTimeForNewMode,
        IMediator mediator,
        IWindowHandle windowHandle,
        IClock clock)
    {
        _airportIdentifier = airportIdentifier;
        _mediator = mediator;
        _windowHandle = windowHandle;
        _clock = clock;

        OriginalRunwayModeIdentifier = currentRunwayMode.Identifier;

        AvailableRunwayModes = availableRunwayModes;
        SelectedRunwayMode = new RunwayModeViewModel(nextTerminalConfiguration ?? currentRunwayMode);
        SelectedRunwayModeIdentifier = SelectedRunwayMode.Identifier;

        LastLandingTime = lastLandingTimeForOldMode;
        FirstLandingTime = firstLandingTimeForNewMode;
    }

    partial void OnSelectedRunwayModeIdentifierChanged(string value)
    {
        // When selection changes in UI, update SelectedRunwayMode to the copy of the matching template
        var template = AvailableRunwayModes.FirstOrDefault(r => r.Identifier == value);
        if (template != null)
        {
            SelectedRunwayMode = new RunwayModeViewModel(template);
        }
    }

    [RelayCommand]
    void ChangeRunwayMode()
    {
        var runwayModeDto = new RunwayModeDto(
            SelectedRunwayMode.Identifier,
            SelectedRunwayMode.Runways
                .Select(r => new RunwayConfigurationDto(r.Identifier, r.LandingRateSeconds))
                .ToArray());

        _mediator.Send(new ChangeRunwayModeRequest(
            _airportIdentifier,
            runwayModeDto,
            LastLandingTime,
            FirstLandingTime));

        CloseWindow();
    }

    [RelayCommand]
    void CloseWindow()
    {
        _windowHandle.Close();
    }
}
