using Microsoft.AspNetCore.SignalR;

namespace Maestro.Server;

public class SignalRLoggingFilter : IHubFilter
{
    readonly IHubContext<DashboardHub> _dashboardHub;
    readonly IConnectionManager _connectionManager;

    public SignalRLoggingFilter(
        IHubContext<DashboardHub> dashboardHub,
        IConnectionManager connectionManager)
    {
        _dashboardHub = dashboardHub;
        _connectionManager = connectionManager;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        // Don't log dashboard hub messages to avoid infinite loop
        if (invocationContext.Hub is DashboardHub)
            return await next(invocationContext);

        try
        {
            var connectionId = invocationContext.Context.ConnectionId;
            var clientInfo = GetConnectionInfo(connectionId);

            // Log incoming message
            await _dashboardHub.Clients.All.SendAsync(
                "LogMessage",
                "Received",
                invocationContext.HubMethodName,
                new
                {
                    Client = clientInfo,
                    Arguments = SimplifyArguments(invocationContext.HubMethodArguments)
                });
        }
        catch
        {
            // Ignore logging errors
        }

        var result = await next(invocationContext);
        return result;
    }

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        await next(context);

        if (context.Hub is not DashboardHub)
        {
            try
            {
                await _dashboardHub.Clients.All.SendAsync(
                    "LogMessage",
                    "Connected",
                    "OnConnectedAsync",
                    new { ConnectionId = context.Context.ConnectionId });
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }

    public async Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        await next(context, exception);

        if (context.Hub is not DashboardHub)
        {
            try
            {
                await _dashboardHub.Clients.All.SendAsync(
                    "LogMessage",
                    "Disconnected",
                    "OnDisconnectedAsync",
                    new { ConnectionId = context.Context.ConnectionId, Error = exception?.Message });
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }

    static object? SimplifyArguments(IReadOnlyList<object?> arguments)
    {
        if (arguments.Count == 0)
            return null;

        if (arguments.Count == 1)
            return SimplifyArgument(arguments[0]);

        return arguments.Select(SimplifyArgument).ToArray();
    }

    static object? SimplifyArgument(object? arg)
    {
        if (arg == null)
            return null;

        var type = arg.GetType();

        // For simple types, return as-is
        if (type.IsPrimitive || type == typeof(string) || type == typeof(DateTime) || type == typeof(decimal))
            return arg;

        // For complex types, return type name to avoid large serialization
        return $"[{type.Name}]";
    }

    object? GetConnectionInfo(string connectionId)
    {
        if (!_connectionManager.TryGetConnection(connectionId, out var connection))
            return null;

        return new
        {
            connection.Callsign,
            connection.Partition,
            connection.AirportIdentifier,
            Role = connection.Role.ToString(),
            connection.IsMaster
        };
    }
}
