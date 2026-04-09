using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Maestro.Avalonia.Integrations;
using Maestro.Contracts.Coordination;

namespace Maestro.Avalonia.ViewModels;

public partial class InformationViewModel : ObservableObject, IRecipient<CoordinationMessageReceivedNotification>
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

        WeakReferenceMessenger.Default.Register(this);
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

    public void Receive(CoordinationMessageReceivedNotification message)
    {
        if (message.AirportIdentifier != _airportIdentifier)
            return;

        AppendMessage(message);
    }
}
