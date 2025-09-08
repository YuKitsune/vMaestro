using System.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class DesequencedViewModel : ObservableObject
{
    readonly IMessageDispatcher _messageDispatcher;
    readonly IErrorReporter _errorReporter;

    [ObservableProperty]
    List<string> _callsigns = [];

    public DesequencedViewModel(
        IMessageDispatcher messageDispatcher,
        IErrorReporter errorReporter,
        string airportIdentifier,
        string[] callsigns)
    {
        AirportIdentifier = airportIdentifier;
        _errorReporter = errorReporter;
        Callsigns = callsigns.ToList();
        _messageDispatcher = messageDispatcher;
    }

    public string AirportIdentifier { get; }

    [RelayCommand]
    async Task Resume(IList selectedCallsigns)
    {
        try
        {
            var callsigns = Callsigns.ToList();
            foreach (var selectedCallsign in selectedCallsigns)
            {
                var selectedCallsignString = (string) selectedCallsign;
                await _messageDispatcher.Send(
                    new ResumeSequencingRequest(AirportIdentifier, selectedCallsignString),
                    CancellationToken.None);
                callsigns.Remove(selectedCallsignString);
            }

            Callsigns = callsigns;
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    async Task Remove(IList selectedCallsigns)
    {
        try
        {
            var callsigns = Callsigns.ToList();
            foreach (var selectedCallsign in selectedCallsigns)
            {
                var selectedCallsignString = (string) selectedCallsign;
                await _messageDispatcher.Send(
                    new RemoveRequest(AirportIdentifier, selectedCallsignString),
                    CancellationToken.None);

                callsigns.Remove(selectedCallsignString);
            }

            Callsigns = callsigns;
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }
}
