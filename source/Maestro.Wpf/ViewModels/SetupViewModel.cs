using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class SetupViewModel : ObservableObject
{
    readonly IMediator _mediator;
    readonly IWindowHandle _windowHandle;
    readonly IErrorReporter _errorReporter;

    [ObservableProperty]
    AirportConfiguration[] _airportConfigurations = [];

    [ObservableProperty]
    string _selectedAirport = "";

    [ObservableProperty]
    RunwayModeViewModel[] _availableRunwayModes = [];

    [ObservableProperty]
    string _selectedRunwayModeIdentifier = "";

    [ObservableProperty]
    RunwayModeViewModel _selectedRunwayMode = null!;

    public SetupViewModel(
        AirportConfiguration[] airportConfigurations,
        IMediator mediator,
        IWindowHandle windowHandle,
        IErrorReporter errorReporter)
    {
        AirportConfigurations = airportConfigurations;

        var initialAirportConfiguration = AirportConfigurations.First();

        SelectedAirport = initialAirportConfiguration.Identifier;
        AvailableRunwayModes = initialAirportConfiguration.RunwayModes.Select(rm => new RunwayModeViewModel(rm.ToMessage())).ToArray();
        SelectedRunwayModeIdentifier = initialAirportConfiguration.RunwayModes.First().Identifier;

        _mediator = mediator;
        _windowHandle = windowHandle;
        _errorReporter = errorReporter;
    }

    // TODO: Make configurable
    public double MinimumLandingRateSeconds => 30;
    public double MaximumLandingRateSeconds => 60 * 5; // 5 Minutes

    partial void OnSelectedAirportChanged(string value)
    {
        var airport = AirportConfigurations.First(a => a.Identifier == value);

        // TODO: Mix of domain types, DTOs, and ViewModels here - needs cleanup
        AvailableRunwayModes = airport.RunwayModes.Select(r => new RunwayModeViewModel(r.ToMessage())).ToArray();
    }

    partial void OnSelectedRunwayModeIdentifierChanged(string value)
    {
        // When selection changes in UI, update SelectedRunwayMode to the copy of the matching template
        var template = AirportConfigurations.First(a => a.Identifier == SelectedAirport).RunwayModes.First(r => r.Identifier == value).ToMessage();
        SelectedRunwayMode = new RunwayModeViewModel(template);
    }

    [RelayCommand]
    void Start()
    {
        try
        {
            var runwayModeDto = new RunwayModeDto(
                SelectedRunwayMode.Identifier,
                SelectedRunwayMode.Runways
                    .Select(r => new RunwayConfigurationDto(r.Identifier, r.LandingRateSeconds))
                    .ToArray());

            _mediator.Send(new StartSequencingRequest(SelectedAirport, runwayModeDto));
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
