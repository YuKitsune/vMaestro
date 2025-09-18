using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Wpf.Integrations;

namespace Maestro.Wpf.ViewModels;

public partial class InformationViewModel : ObservableObject, IAsyncDisposable
{
    readonly string _airportIdentifier;
    readonly INotificationStream<InformationNotification> _notificationStream;
    readonly IErrorReporter _errorReporter;
    readonly IWindowHandle _windowHandle;

    readonly CancellationTokenSource _notificationSubscriptionCancellationTokenSource;
    readonly Task _notificationSubscriptionTask;

    [ObservableProperty]
    string _messageText = string.Empty;


    public InformationViewModel(string airportIdentifier, IErrorReporter errorReporter, IWindowHandle windowHandle, INotificationStream<InformationNotification> notificationStream)
    {
        _airportIdentifier = airportIdentifier;

        _errorReporter = errorReporter;
        _windowHandle = windowHandle;

        // Subscribe to notifications
        _notificationStream = notificationStream;
        _notificationSubscriptionCancellationTokenSource = new CancellationTokenSource();
        _notificationSubscriptionTask = SubscribeToInformationMessages(_notificationSubscriptionCancellationTokenSource.Token);
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

    async Task SubscribeToInformationMessages(CancellationToken cancellationToken)
    {
        await foreach (var notification in _notificationStream.SubscribeAsync(cancellationToken))
        {
            try
            {
                if (notification.AirportIdentifier != _airportIdentifier)
                    continue;

                AppendMessage(notification);
            }
            catch (Exception ex)
            {
                _errorReporter.ReportError(ex);
            }
        }
    }

    [RelayCommand]
    void Acknowledge()
    {
        MessageText = string.Empty;
        _windowHandle.Close();
    }

    public async ValueTask DisposeAsync()
    {
        _notificationSubscriptionCancellationTokenSource.Cancel();
        await _notificationSubscriptionTask;
    }
}
