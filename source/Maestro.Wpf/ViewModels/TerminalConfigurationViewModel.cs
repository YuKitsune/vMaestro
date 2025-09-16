using Maestro.Core.Infrastructure;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public class TerminalConfigurationViewModel : ViewModel
{
    readonly string _airportIdentifier;
    readonly IClock _clock;
    readonly IMediator _mediator;
    readonly IWindowHandle _windowHandle;
    readonly IErrorReporter _errorReporter;

    public string OriginalRunwayModeIdentifier
    {
        get => Get<string>(nameof(OriginalRunwayModeIdentifier));
        set => Set(nameof(OriginalRunwayModeIdentifier), value);
    }

    public string SelectedRunwayModeIdentifier
    {
        get => Get<string>(nameof(SelectedRunwayModeIdentifier));
        set => Set(
            nameof(SelectedRunwayModeIdentifier),
            value,
            onPropertyChanged: OnSelectedRunwayModeIdentifierChanged);
    }

    public RunwayModeViewModel SelectedRunwayMode
    {
        get => Get<RunwayModeViewModel>(nameof(SelectedRunwayMode));
        set => Set(nameof(SelectedRunwayMode), value);
    }

    public bool ChangeImmediately
    {
        get => Get<bool>(nameof(ChangeImmediately));
        set => Set(nameof(ChangeImmediately), value);
    }

    public DateTimeOffset LastLandingTime
    {
        get => Get<DateTimeOffset>(nameof(LastLandingTime));
        set => Set(nameof(LastLandingTime), value);
    }

    public DateTimeOffset FirstLandingTime
    {
        get => Get<DateTimeOffset>(nameof(FirstLandingTime));
        set => Set(nameof(FirstLandingTime), value);
    }

    public RelayCommand ChangeRunwayModeCommand => new(ChangeRunwayMode);
    public RelayCommand CloseWindowCommand => new(CloseWindow);

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
        IClock clock,
        IErrorReporter errorReporter)
    {
        _airportIdentifier = airportIdentifier;
        _mediator = mediator;
        _windowHandle = windowHandle;
        _clock = clock;
        _errorReporter = errorReporter;

        OriginalRunwayModeIdentifier = currentRunwayMode.Identifier;
        AvailableRunwayModes = availableRunwayModes;
        SelectedRunwayMode = new RunwayModeViewModel(nextTerminalConfiguration ?? currentRunwayMode);
        SelectedRunwayModeIdentifier = SelectedRunwayMode.Identifier;
        LastLandingTime = lastLandingTimeForOldMode;
        FirstLandingTime = firstLandingTimeForNewMode;
    }

    void OnSelectedRunwayModeIdentifierChanged(string _, string newValue)
    {
        // When selection changes in UI, update SelectedRunwayMode to the copy of the matching template
        var template = AvailableRunwayModes.FirstOrDefault(r => r.Identifier == newValue);
        if (template != null)
        {
            SelectedRunwayMode = new RunwayModeViewModel(template);
        }
    }

    void ChangeRunwayMode()
    {
        try
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
        catch (Exception ex)
        {
            _errorReporter.ReportError( ex);
        }
    }

    void CloseWindow()
    {
        _windowHandle.Close();
    }
}
