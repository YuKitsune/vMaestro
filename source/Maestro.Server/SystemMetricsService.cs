using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;

namespace Maestro.Server;

public class SystemMetricsService : BackgroundService
{
    readonly IHubContext<DashboardHub> _dashboardHub;
    readonly IConnectionManager _connectionManager;
    readonly ILogger<SystemMetricsService> _logger;
    readonly Process _currentProcess;

    public SystemMetricsService(
        IHubContext<DashboardHub> dashboardHub,
        IConnectionManager connectionManager,
        ILogger<SystemMetricsService> logger)
    {
        _dashboardHub = dashboardHub;
        _connectionManager = connectionManager;
        _logger = logger;
        _currentProcess = Process.GetCurrentProcess();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SystemMetricsService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Get CPU usage
                var startTime = DateTime.UtcNow;
                var startCpuUsage = _currentProcess.TotalProcessorTime;

                await Task.Delay(5000, stoppingToken);

                var endTime = DateTime.UtcNow;
                var endCpuUsage = _currentProcess.TotalProcessorTime;

                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                var cpuUsagePercentage = cpuUsageTotal * 100;

                // Get memory usage
                _currentProcess.Refresh();
                var memoryUsageMB = _currentProcess.WorkingSet64 / 1024.0 / 1024.0;

                // Send metrics to dashboard
                await _dashboardHub.Clients.All.SendAsync(
                    "UpdateMetrics",
                    Math.Round(cpuUsagePercentage, 2),
                    Math.Round(memoryUsageMB, 2),
                    stoppingToken);

                // Send connection list
                var allConnections = GetAllConnections();
                await _dashboardHub.Clients.All.SendAsync(
                    "UpdateConnections",
                    allConnections,
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SystemMetricsService");
            }
        }

        _logger.LogInformation("SystemMetricsService stopped");
    }

    object[] GetAllConnections()
    {
        // Access internal connection list via reflection
        var type = _connectionManager.GetType();
        var field = type.GetField("_connections", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field?.GetValue(_connectionManager) is not List<Connection> connections)
            return [];

        return connections.Select(c => new
        {
            c.Id,
            c.Partition,
            c.AirportIdentifier,
            c.Callsign,
            Role = c.Role.ToString(),
            c.IsMaster
        }).ToArray();
    }
}
