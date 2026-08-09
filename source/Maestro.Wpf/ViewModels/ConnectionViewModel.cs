using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Maestro.Core.Configuration;
using Maestro.Core.Connectivity.Contracts;
using Maestro.Wpf.Contracts;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class ConnectionViewModel : ObservableObject
{
    readonly IMediator _mediator;
    readonly IWindowHandle _windowHandle;
    readonly IErrorReporter _errorReporter;

    readonly string _airportIdentifier;
    readonly Uri _defaultServerUrl;

    [ObservableProperty]
    string _serverUrl;

    [ObservableProperty]
    string[] _servers;

    [ObservableProperty]
    string _selectedServer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeServer))]
    [NotifyPropertyChangedFor(nameof(ConnectButtonText))]
    bool _isConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeServer))]
    [NotifyPropertyChangedFor(nameof(ConnectButtonText))]
    bool _isReady;

    public bool CanChangeServer => !IsConnected && !IsReady;

    public string ConnectButtonText => IsConnected || IsReady ? "Disconnect" : "Connect";

    public ConnectionViewModel(
        string airportIdentifier,
        ServerConfiguration serverConfiguration,
        Uri currentServerUrl,
        string environment,
        bool isConnected,
        bool isReady,
        IMediator mediator,
        IWindowHandle windowHandle,
        IErrorReporter errorReporter)
    {
        _airportIdentifier = airportIdentifier;
        _defaultServerUrl = serverConfiguration.Uri;
        ServerUrl = currentServerUrl.ToString();
        Servers = serverConfiguration.Environments;
        SelectedServer = !string.IsNullOrEmpty(environment) ? environment : serverConfiguration.Environments.First();
        IsConnected = isConnected;
        IsReady = isReady;

        _mediator = mediator;
        _windowHandle = windowHandle;
        _errorReporter = errorReporter;

        WeakReferenceMessenger.Default.Register<ConnectionStatusChangedNotification>(this, (r, m) =>
        {
            if (m.AirportIdentifier != _airportIdentifier)
                return;

            IsReady = m.IsReady;
            IsConnected = m.IsConnected;
        });
    }

    [RelayCommand]
    void ConnectOrDisconnect()
    {
        try
        {
            if (IsConnected || IsReady)
            {
                _mediator.Send(new DestroyConnectionRequest(_airportIdentifier));
            }
            else
            {
                var serverUrl = string.IsNullOrWhiteSpace(ServerUrl)
                    ? _defaultServerUrl
                    : new Uri(ServerUrl);

                _mediator.Send(new CreateConnectionRequest(_airportIdentifier, serverUrl, SelectedServer));
            }
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
