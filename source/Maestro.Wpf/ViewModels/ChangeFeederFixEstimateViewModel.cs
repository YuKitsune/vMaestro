using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class ChangeFeederFixEstimateViewModel : ObservableObject
{
    readonly IWindowHandle _windowHandle;
    readonly IMessageDispatcher _messageDispatcher;
    readonly IErrorReporter _errorReporter;

    readonly string _airportIdentifier;

    [ObservableProperty]
    string _callsign;

    [ObservableProperty]
    string _feederFix;

    [ObservableProperty]
    DateTimeOffset _originalFeederFixEstimate;

    [ObservableProperty]
    DateTimeOffset _newFeederFixEstimate;

    public ChangeFeederFixEstimateViewModel(
        string airportIdentifier,
        string callsign,
        string feederFix,
        DateTimeOffset originalFeederFixEstimate,
        IWindowHandle windowHandle,
        IMessageDispatcher messageDispatcher,
        IErrorReporter errorReporter)
    {
        _airportIdentifier = airportIdentifier;
        _callsign = callsign;
        _feederFix = feederFix;
        _originalFeederFixEstimate = originalFeederFixEstimate;
        _newFeederFixEstimate = originalFeederFixEstimate;

        _windowHandle = windowHandle;
        _messageDispatcher = messageDispatcher;
        _errorReporter = errorReporter;
    }

    [RelayCommand]
    public async Task ChangeFeederFixEstimate()
    {
        try
        {
            await _messageDispatcher.Send(
                new ChangeFeederFixEstimateRequest(_airportIdentifier, Callsign, NewFeederFixEstimate),
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
