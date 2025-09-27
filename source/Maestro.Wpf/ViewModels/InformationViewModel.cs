using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Wpf.Integrations;

namespace Maestro.Wpf.ViewModels;

public partial class InformationViewModel : ObservableObject
{
    readonly string _airportIdentifier;
    readonly IWindowHandle _windowHandle;

    [ObservableProperty]
    string _messageText = string.Empty;

    public InformationViewModel(string airportIdentifier, IWindowHandle windowHandle)
    {
        _airportIdentifier = airportIdentifier;
        _windowHandle = windowHandle;

        WeakReferenceMessenger.Default.Register<InformationNotification>(this, (s, m) =>
        {
            if (m.AirportIdentifier != _airportIdentifier)
                return;

            AppendMessage(m);
        });
    }

    void AppendMessage(InformationNotification notification)
    {
        if (string.IsNullOrEmpty(MessageText))
        {
            MessageText = $"{notification.Time:HH:mm:ss}: {notification.Message}";
        }
        else
        {
            MessageText += Environment.NewLine + $"{notification.Time:HH:mm:ss}: {notification.Message}";
        }
    }

    [RelayCommand]
    void Acknowledge()
    {
        MessageText = string.Empty;
        _windowHandle.Close();
    }
}
