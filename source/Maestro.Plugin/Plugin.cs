using System.ComponentModel.Composition;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Maestro.Contracts.Flights;
using Maestro.Contracts.Shared;
using Maestro.Core;
using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Integration;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using Maestro.Core.Sessions.Contracts;
using Maestro.Plugin.Configuration;
using Maestro.Plugin.Handlers;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf;
using Maestro.Wpf.Contracts;
using Maestro.Wpf.Integrations;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using vatsys;
using vatsys.Plugin;

namespace Maestro.Plugin;

[Export(typeof(IPlugin))]
public class Plugin : IPlugin
{
#if DEBUG
    public const string Name = "Maestro - Debug";
#else
    public const string Name = "Maestro";
#endif

    string IPlugin.Name => Name;

    readonly AircraftLandingCircuitBreaker _aircraftLandingCircuitBreaker = new();

    readonly IMediator? _mediator;
    readonly ISessionManager? _sessionManager;
    readonly ILogger? _logger;
    readonly WorkQueue? _workQueue;
    readonly BackgroundTask? _windCheckTask;

    public Plugin()
    {
        try
        {
            DpiAwareness.EnsureDpiAwareness();

            var configuration = ConfigureConfiguration();
            ConfigureServices(configuration);
            ConfigureTheme();
            AddToolbarItems(configuration);

            _mediator = Ioc.Default.GetRequiredService<IMediator>();
            _sessionManager = Ioc.Default.GetRequiredService<ISessionManager>();
            _logger = Ioc.Default.GetRequiredService<ILogger>();
            _workQueue = Ioc.Default.GetRequiredService<WorkQueue>();
            _windCheckTask = new BackgroundTask(WindCheckWorker);

            Network.Connected += NetworkOnConnected;
            Network.Disconnected += NetworkOnDisconnected;

            MMI.SelectedTrackChanged += SelectedTrackChanged;

            var executingAssembly = Assembly.GetExecutingAssembly();
            var version = executingAssembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? string.Empty;
#if RELEASE
            version = version.Split('+').First();
#endif
            _logger.Information("{PluginName} {Version} initialized.", Name, version);

#if RELEASE
            // Check for updates in background (fire-and-forget)
            var pluginConfiguration = Ioc.Default.GetRequiredService<PluginConfiguration>();
            if (pluginConfiguration.CheckForUpdates)
            {
                _ = GitHubReleaseChecker.CheckForUpdatesAsync(version, _logger);
            }
#endif
        }
        catch (Exception ex)
        {
            Errors.Add(ex, Name);
            _logger?.Error(ex, "An error occurred during initialization.");
        }
    }

    void SelectedTrackChanged(object sender, EventArgs e)
    {
        var selectedTrack = MMI.SelectedTrack;
        var callsign = selectedTrack?.GetFDR()?.Callsign;
        WeakReferenceMessenger.Default.Send(new VatsysTrackSelectedNotification(callsign));
    }

    void ConfigureTheme()
    {
        Theme.BackgroundColor = new SolidColorBrush(Colours.GetColour(Colours.Identities.WindowBackground).ToWindowsColor());
        Theme.GenericTextColor = new SolidColorBrush(Colours.GetColour(Colours.Identities.GenericText).ToWindowsColor());
        Theme.InteractiveTextColor = new SolidColorBrush(Colours.GetColour(Colours.Identities.InteractiveText).ToWindowsColor());
        Theme.NonInteractiveTextColor = new SolidColorBrush(Colours.GetColour(Colours.Identities.NonInteractiveText).ToWindowsColor());
        Theme.SelectedButtonColor = new SolidColorBrush(Colours.GetColour(Colours.Identities.WindowButtonSelected).ToWindowsColor());
        Theme.FontFamily = new FontFamily(MMI.eurofont_xsml.FontFamily.Name);
        Theme.FontSize = MMI.eurofont_xsml.Size;
        Theme.FontWeight = MMI.eurofont_xsml.Bold ? FontWeights.Bold : FontWeights.Regular;
    }

    void ConfigureServices(PluginConfiguration pluginConfiguration)
    {
        var logger = ConfigureLogger(pluginConfiguration.Logging);

        Ioc.Default.ConfigureServices(
            new ServiceCollection()
                .AddConfiguration(pluginConfiguration)
                .AddSingleton(pluginConfiguration.Server)
                .AddViewModels()
                .AddMaestro()
                .AddMediatR(c =>
                {
                    c.NotificationPublisher = new AsyncNotificationPublisher(logger);

                    c.RegisterServicesFromAssemblies(
                        typeof(Core.AssemblyMarker).Assembly,
                        typeof(AssemblyMarker).Assembly,
                        typeof(Wpf.AssemblyMarker).Assembly
                    );
                })
                .AddSingleton<IFixLookup, VatsysFixLookup>()
                .AddSingleton<IPerformanceLookup>(new YamlPerformanceLookup(pluginConfiguration.AircraftPerformance))
                .AddSingleton(new GuiInvoker(MMI.InvokeOnGUI))
                .AddSingleton(logger)
                .AddSingleton<IErrorReporter>(x => new ErrorReporter(Name, logger, x.GetRequiredService<IClock>()))
                .AddSingleton<WindowManager>()
                .AddSingleton<WorkQueue>()
                .BuildServiceProvider());
    }

    PluginConfiguration ConfigureConfiguration()
    {
        var configFilePath = ConfigurationLocator.LocateConfigurationFile();

        var configurationYaml = File.ReadAllText(configFilePath);
        var configuration = YamlConfigurationLoader.LoadFromYaml(configurationYaml);

        return configuration;
    }

    ILogger ConfigureLogger(ILoggingConfiguration loggingConfiguration)
    {
        var logFileName = Path.Combine(Helpers.GetFilesFolder(), "maestro_log.txt");

        // TODO: Include the airport in the log output
        var logger = new LoggerConfiguration()
            .WriteTo.File(
                path: logFileName,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: loggingConfiguration.MaxFileAgeDays,
                outputTemplate: "{Timestamp:u} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .MinimumLevel.Is(loggingConfiguration.LogLevel)
            .CreateLogger();

        Log.Logger = logger;

        return logger;
    }

    void AddToolbarItems(PluginConfiguration pluginConfiguration)
    {
        const string menuItemCategory = "TFMS";

        foreach (var airportConfiguration in pluginConfiguration.Airports)
        {
            var menuItem = new CustomToolStripMenuItem(
                CustomToolStripMenuItemWindowType.Main,
                menuItemCategory,
                new ToolStripMenuItem(airportConfiguration.Identifier));

            menuItem.Item.Click += (_, _) => OpenOrFocusWindowFor(airportConfiguration.Identifier);

            MMI.AddCustomMenuItem(menuItem);
        }
    }

    void OpenOrFocusWindowFor(string airportIdentifier)
    {
        var windowManager = Ioc.Default.GetRequiredService<WindowManager>();
        if (windowManager.TryGetWindow(WindowKeys.Maestro(airportIdentifier), out var windowHandle))
        {
            windowHandle!.Focus();

            var guiInvoker = Ioc.Default.GetRequiredService<GuiInvoker>();
            guiInvoker.InvokeOnUiThread(_ =>
            {
            });
        }
        else
        {
            TryEnqueue(async () =>
            {
                if (_mediator is null)
                    return;

                await _mediator.Send(new CreateMaestroSessionRequest(airportIdentifier), CancellationToken.None);
            });
        }
    }

    void NetworkOnConnected(object sender, EventArgs e)
    {
        try
        {
            TryEnqueue(async () =>
            {
                if (_mediator is null)
                    return;

                await _mediator.Publish(new NetworkConnectedNotification(Network.Callsign), CancellationToken.None);
            });

            TryEnqueue(StartWindCheck);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "An error occurred while handling Network.Connected.");
            Errors.Add(ex, Name);
        }
    }

    void NetworkOnDisconnected(object sender, EventArgs e)
    {
        try
        {
            TryEnqueue(async () =>
            {
                if (_mediator is null)
                    return;

                await _mediator.Publish(new NetworkDisconnectedNotification(), CancellationToken.None);
            });

            TryEnqueue(StopWindCheck);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "An error occurred while handling Network.Disconnected.");
            Errors.Add(ex, Name);
        }
    }

    Task StartWindCheck()
    {
        if (_windCheckTask is null || _windCheckTask.IsRunning)
            return Task.CompletedTask;

        _logger?.Information("Wind check starting");
        _windCheckTask.Start();

        return Task.CompletedTask;
    }

    async Task StopWindCheck()
    {
        if (_windCheckTask is null || !_windCheckTask.IsRunning)
            return;

        // Fire and forget - don't block the event handler waiting for the task to complete
        _logger?.Information("Wind check stopping");
        await _windCheckTask.Stop(CancellationToken.None);
    }

    async Task WindCheckWorker(CancellationToken cancellationToken)
    {
        var checkInterval = TimeSpan.FromMinutes(30);
        var errorInterval = TimeSpan.FromMinutes(5);
        var windCheckTimeout = TimeSpan.FromMinutes(1);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (Network.IsConnected && _sessionManager is not null && _mediator is not null)
                    {
                        var timeoutCancellationTokenSource = new CancellationTokenSource(windCheckTimeout);
                        foreach (var airportIdentifier in _sessionManager.ActiveSessions)
                        {
                            await _mediator.Send(
                                new RefreshWindRequest(airportIdentifier),
                                timeoutCancellationTokenSource.Token);
                        }
                    }

                    await Task.Delay(checkInterval, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Ignored
                }
                catch (Exception ex)
                {
                    _logger?.Error(ex, "Error updating wind");
                    await Task.Delay(errorInterval, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger?.Information("Wind check stopping");
        }
        catch (Exception ex)
        {
            _logger?.Fatal(ex, "Wind check failed");
        }
    }

    public void OnFDRUpdate(FDP2.FDR updated)
    {
        try
        {
            // Check if the flight has landed
            if (HasLanded(updated))
            {
                TryEnqueue(async () => await TryNotifyLanded(updated, CancellationToken.None));
            }

            TryEnqueue(async () => await PublishFlightUpdatedEvent(updated));
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Failed to handle OnFDRUpdate for {Callsign}.", updated.Callsign);
        }
    }

    public void OnRadarTrackUpdate(RDP.RadarTrack updated)
    {
        try
        {
            // Check if the aircraft has landed
            if (updated.CoupledFDR is null || updated.OnGround)
            {
                var fdr = FDP2.GetFDRs.FirstOrDefault(f => f.Callsign == updated.ActualAircraft.Callsign);
                if (fdr is not null && HasLanded(fdr))
                {
                    TryEnqueue(async () => await TryNotifyLanded(fdr, CancellationToken.None));
                }
            }

            if (updated.CoupledFDR is null)
                return;

            // We publish the FlightUpdatedEvent here because OnFDRUpdate and Network_Connected aren't guaranteed to
            // fire for all flights when initially connecting to the network.
            // The handler for this event will publish the FlightPositionReport if a FlightPosition is available.

            TryEnqueue(async () => await PublishFlightUpdatedEvent(updated.CoupledFDR));
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Failed to handle OnRadarTrackUpdate for {Callsign}.", updated.CoupledFDR?.Callsign);
        }
    }

    bool HasLanded(FDP2.FDR updated)
    {
        var lastWaypointIndex = updated.ParsedRoute.FindLastIndex(s => s.Type == FDP2.FDR.ExtractedRoute.Segment.SegmentTypes.WAYPOINT);
        var didPassLastWaypoint = updated.ParsedRoute.OverflownIndex >= lastWaypointIndex;

        var isOnGround = updated.CoupledTrack is null || updated.CoupledTrack.OnGround;

        return didPassLastWaypoint && isOnGround;
    }

    async Task TryNotifyLanded(FDP2.FDR fdr, CancellationToken cancellationToken)
    {
        if (_mediator is null)
            return;

        if (_sessionManager is null || !_sessionManager.SessionExists(fdr.DesAirport))
            return;

        // Already notified, nothing to do
        if (!_aircraftLandingCircuitBreaker.TrySetBreaker(fdr.Callsign))
            return;

        var lastWaypoint = fdr.ParsedRoute.Last(s => s.Type == FDP2.FDR.ExtractedRoute.Segment.SegmentTypes.WAYPOINT);
        var landingTime = lastWaypoint.ATO;

        if (lastWaypoint.ATO == default || lastWaypoint.ATO == DateTime.MaxValue)
        {
            landingTime = lastWaypoint.ETO;
            _logger?.Warning("{Callsign} actual landing time was {ActualLandingTime}, using ETA of last waypoint {LastWaypointEstimate}", fdr.Callsign, lastWaypoint.ATO, lastWaypoint.ETO);
        }

        await _mediator.Publish(new FlightLandedNotification(fdr.DesAirport, fdr.Callsign, landingTime), cancellationToken);
    }

    async Task PublishFlightUpdatedEvent(FDP2.FDR updated)
    {
        // BUG: FDR updates can be sent before the instance manager has been created, in which case we miss updates
        //  When an instance is created, scan the active FDRs to ensure it's populated.
        if (_mediator is null || _sessionManager is null || !_sessionManager.SessionExists(updated.DesAirport))
            return;

        var isActivated = updated.State > FDP2.FDR.FDRStates.STATE_PREACTIVE;
        if (!isActivated)
            return;

        // Estimates have not been calculated yet
        if (!updated.ESTed)
            return;

        var routeSegments = updated.ParsedRoute
            .ToArray() // Materialize to avoid mutation during enumeration
            .Select((s, i) => (Segment: s, Index: i, Dto: new FixEstimate(s.Intersection.Name, ToDateTimeOffset(s.ETO))))
            .Where(x => x.Index > updated.ParsedRoute.OverflownIndex && x.Segment.Type == FDP2.FDR.ExtractedRoute.Segment.SegmentTypes.WAYPOINT)
            .ToArray();

        // If any remaining estimates are null, ETOs haven't finished computing yet — wait for the next update
        if (routeSegments.Any(x => x.Dto.Estimate == DateTimeOffset.MaxValue))
        {
            _logger?.Verbose("{Callsign} skipped: one or more ETOs not yet computed", updated.Callsign);
            return;
        }

        var estimates = routeSegments.Select(x => x.Dto).ToArray();

        FlightPosition? position = null;
        if (updated.CoupledTrack is not null)
        {
            var track = updated.CoupledTrack;
            var verticalTrack = track.VerticalSpeed >= RDP.VS_CLIMB
                ? VerticalTrack.Climbing
                : track.VerticalSpeed <= RDP.VS_DESCENT
                    ? VerticalTrack.Descending
                    : VerticalTrack.Maintaining;

            position = new FlightPosition(
                new Contracts.Shared.Coordinate(track.LatLong.Latitude, track.LatLong.Longitude),
                track.CorrectedAltitude,
                verticalTrack,
                track.GroundSpeed,
                track.OnGround);
        }

        // PerformanceData can be null
        var aircraftCategory = updated.PerformanceData is null || updated.PerformanceData.IsJet
            ? AircraftCategory.Jet
            : AircraftCategory.NonJet;

        var wake = updated.AircraftWake switch
        {
            "J" => WakeCategory.SuperHeavy,
            "H" => WakeCategory.Heavy,
            "M" => WakeCategory.Medium,
            "L" => WakeCategory.Light,
            _ => WakeCategory.Heavy
        };

        var notification = new FlightUpdatedNotification(
            updated.Callsign,
            updated.AircraftType,
            aircraftCategory,
            wake,
            updated.DepAirport,
            updated.DesAirport,
            ToDateTimeOffset(updated.ETD),
            updated.EET,
            position,
            estimates);

        await _mediator.Publish(notification, CancellationToken.None);
    }

    internal static DateTimeOffset ToDateTimeOffset(DateTime dateTime)
    {
        if (dateTime == DateTime.MaxValue)
            return DateTimeOffset.MaxValue;

        return new DateTimeOffset(
            dateTime.Year, dateTime.Month, dateTime.Day,
            dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond,
            TimeSpan.Zero);
    }

    void TryEnqueue(Func<Task> func)
    {
        _workQueue?.Enqueue(func);
    }
}
