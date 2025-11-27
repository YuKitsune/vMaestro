using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Wpf.Integrations;

namespace Maestro.Wpf.ViewModels;

public partial class InformationViewModel : ObservableObject
{
    readonly string _airportIdentifier;
    readonly IWindowHandle _windowHandle;

    [ObservableProperty]
    string _messageText = string.Empty;

    public InformationViewModel(string airportIdentifier, IWindowHandle windowHandle, CoordinationMessageReceivedNotification initialNotification)
    {
        _airportIdentifier = airportIdentifier;
        _windowHandle = windowHandle;
        AppendMessage(initialNotification);

        WeakReferenceMessenger.Default.Register<CoordinationMessageReceivedNotification>(this, (s, m) =>
        {
            if (m.AirportIdentifier != _airportIdentifier)
                return;

            AppendMessage(m);
        });
    }

    void AppendMessage(CoordinationMessageReceivedNotification notification)
    {
        var newMessageText = $"{notification.Sender} ({notification.Time:HH:mm:ss}): {notification.Message}";
        if (string.IsNullOrEmpty(MessageText))
        {
            MessageText = newMessageText;
        }
        else
        {
            MessageText += Environment.NewLine + newMessageText;
        }
    }

    [RelayCommand]
    void Acknowledge()
    {
        MessageText = string.Empty;
        _windowHandle.Close();
    }
}
