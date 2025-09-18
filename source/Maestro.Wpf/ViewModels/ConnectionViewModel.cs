using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Configuration;
using Maestro.Core.Messages;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class ConnectionViewModel : ObservableObject
{
    readonly IMediator _mediator;
    readonly IWindowHandle _windowHandle;
    readonly IErrorReporter _errorReporter;

    readonly string _airportIdentifier;

    [ObservableProperty]
    string[] _servers;

    [ObservableProperty]
    string _selectedServer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeServer))]
    [NotifyPropertyChangedFor(nameof(ConnectButtonText))]
    bool _isConnected;

    public bool CanChangeServer => !IsConnected;

    public string ConnectButtonText => IsConnected ? "Disconnect" : "Connect";

    public ConnectionViewModel(
        string airportIdentifier,
        ServerConfiguration serverConfiguration,
        string partition,
        bool isConnected,
        IMediator mediator,
        IWindowHandle windowHandle,
        IErrorReporter errorReporter)
    {
        _airportIdentifier = airportIdentifier;
        Servers = serverConfiguration.Partitions;
        SelectedServer = !string.IsNullOrEmpty(partition) ? partition : serverConfiguration.Partitions.First();
        IsConnected = isConnected;

        _mediator = mediator;
        _windowHandle = windowHandle;
        _errorReporter = errorReporter;
    }

    [RelayCommand]
    void ConnectOrDisconnect()
    {
        try
        {
            if (IsConnected)
            {
                _mediator.Send(new DisconnectSessionRequest(_airportIdentifier));
            }
            else
            {
                _mediator.Send(new ConnectSessionRequest(_airportIdentifier, SelectedServer));
            }

            _windowHandle.Close();
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
