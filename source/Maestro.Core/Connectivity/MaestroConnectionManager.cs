using Maestro.Core.Configuration;
using MediatR;
using Serilog;

namespace Maestro.Core.Connectivity;

public class MaestroConnectionManager : IMaestroConnectionManager, IAsyncDisposable
{
    readonly ServerConfiguration _serverConfiguration;
    readonly IMediator _mediator;
    readonly ILogger _logger;
    readonly SemaphoreSlim _semaphore = new(1, 1);
    readonly Dictionary<string, MaestroConnection> _connections = new();

    public MaestroConnectionManager(
        ServerConfiguration serverConfiguration,
        IMediator mediator,
        ILogger logger)
    {
        _serverConfiguration = serverConfiguration;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<MaestroConnection> CreateConnection(
        string airportIdentifier,
        string partition,
        CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_connections.ContainsKey(airportIdentifier))
            {
                throw new MaestroException(
                    $"Connection already exists for {airportIdentifier}.");
            }

            var connection = new MaestroConnection(
                _serverConfiguration,
                airportIdentifier,
                partition,
                _mediator,
                _logger.ForContext<MaestroConnection>());

            _connections[airportIdentifier] = connection;

            _logger.Information("Connection created for {AirportIdentifier}", airportIdentifier);

            return connection;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public bool TryGetConnection(string airportIdentifier, out MaestroConnection? connection)
    {
        return _connections.TryGetValue(airportIdentifier, out connection);
    }

    public async Task RemoveConnection(string airportIdentifier, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (!_connections.TryGetValue(airportIdentifier, out var connection))
            {
                _logger.Warning("No connection found for {AirportIdentifier} to remove", airportIdentifier);
                return;
            }

            if (connection.IsConnected)
            {
                await connection.Stop(cancellationToken);
            }

            await connection.DisposeAsync();
            _connections.Remove(airportIdentifier);

            _logger.Information("Connection removed for {AirportIdentifier}", airportIdentifier);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            foreach (var kvp in _connections)
            {
                var airportIdentifier = kvp.Key;
                var connection = kvp.Value;

                try
                {
                    if (connection.IsConnected)
                    {
                        await connection.Stop(CancellationToken.None);
                    }

                    await connection.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error disposing connection for {AirportIdentifier}", airportIdentifier);
                }
            }

            _connections.Clear();
        }
        finally
        {
            _semaphore.Release();
            _semaphore.Dispose();
        }
    }
}
