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
    List<RunwayModeViewModel> _availableRunwayModes = [];
    
    [ObservableProperty]
    RunwayModeViewModel _selectedRunwayMode;

    [ObservableProperty]
    string _changeTimeText;

    [ObservableProperty]
    bool _reassignRunways;

    [ObservableProperty]
    List<string> _errors = [];

    // TODO: Make configurable
    public double MinimumLandingRateSeconds => 30;
    public double MaximumLandingRateSeconds => 60 * 5; // 5 Minutes

    public TerminalConfigurationViewModel()
    {
        SelectedRunwayMode = new RunwayModeViewModel("34PROPS", [
            new RunwayViewModel("34L", 180),
            new RunwayViewModel("34R", 180)
        ]);

        AvailableRunwayModes =
        [
            SelectedRunwayMode
        ];

        ChangeTimeText = "1234";
        Errors = ["Example Error"];
    }

    public TerminalConfigurationViewModel(
        string airportIdentifier,
        RunwayModeDto[] availableRunwayModes,
        RunwayModeDto currentRunwayMode,
        RunwayModeDto? nextTerminalConfiguration,
        DateTimeOffset terminalConfigurationChangeTime,
        IMediator mediator,
        IWindowHandle windowHandle,
        IClock clock)
    {
        _airportIdentifier = airportIdentifier;
        _mediator = mediator;
        _windowHandle = windowHandle;
        _clock = clock;

        AvailableRunwayModes = availableRunwayModes.Select(r => new RunwayModeViewModel(r)).ToList();
        SelectedRunwayMode = new RunwayModeViewModel(nextTerminalConfiguration ?? currentRunwayMode);
        ChangeTimeText = terminalConfigurationChangeTime == default
            ? _clock.UtcNow().ToString("HHmm")
            : terminalConfigurationChangeTime.ToString("HHmm");
    }

    partial void OnChangeTimeTextChanged(string value)
    {
        Errors = [];
        if (GetHoursAndMinutes(value, out _, out _))
            return;

        Errors = [$"{ChangeTimeText} is invalid. Please use the format HHmm"];
    }
    

    [RelayCommand(CanExecute = nameof(CanChangeRunwayMode))]
    public void ChangeRunwayMode()
    {
        Errors = [];
        if (!TryGetChangeTime(out var changeTime))
            return;

        var runwayModeDto = new RunwayModeDto(
            SelectedRunwayMode.Identifier,
            SelectedRunwayMode.Runways
                .Select(r => new RunwayConfigurationDto(r.Identifier, r.LandingRateSeconds))
                .ToArray());
        
        _mediator.Send(new ChangeRunwayModeRequest(
            _airportIdentifier,
            runwayModeDto,
            changeTime,
            ReassignRunways));
        
        CloseWindow();
    }

    public bool CanChangeRunwayMode()
    {
        return GetHoursAndMinutes(ChangeTimeText, out _, out _);
    }

    [RelayCommand]
    public void CloseWindow()
    {
        _windowHandle.Close();
    }
    
    bool TryGetChangeTime(out DateTimeOffset result)
    {
        result = default;

        if (!GetHoursAndMinutes(ChangeTimeText, out var hours, out var minutes))
            return false;

        var currentTime = _clock.UtcNow();
        var todayTarget = new DateTimeOffset(
            currentTime.Year, currentTime.Month, currentTime.Day,
            hours, minutes, 0, currentTime.Offset
        );

        result = todayTarget > currentTime ? todayTarget : todayTarget.AddDays(1);
        return true;
    }

    static bool GetHoursAndMinutes(string value, out int hours, out int minutes)
    {
        hours = -1;
        minutes = -1;

        if (string.IsNullOrWhiteSpace(value) || value.Length != 4)
            return false;

        if (!int.TryParse(value.Substring(0, 2), out hours) || 
            !int.TryParse(value.Substring(2, 2), out minutes))
            return false;

        if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59)
            return false;
        
        return true;
    }
}