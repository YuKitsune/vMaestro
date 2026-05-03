using System.Reflection;
using Maestro.Contracts.Sessions;
using Maestro.Server;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.OpenApi;
using Serilog;
using AssemblyMarker = Maestro.Server.AssemblyMarker;

var dataPath = Environment.GetEnvironmentVariable("DATA_PATH") ?? "/app/data";
var logsPath = Path.Combine(dataPath, "logs");
Directory.CreateDirectory(logsPath);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(logsPath, "maestro-.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

// Log version information at startup
var assembly = Assembly.GetExecutingAssembly();
var version = AssemblyVersionHelper.GetVersion(assembly);
Log.Information("Starting Maestro.Server version {Version}", version);

// TODO: Error handling
// - Don't throw exceptions, return results
// - Write exceptions to logs
// - Log notifications and requests are they're being sent and received

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSignalR()
        .AddHubOptions<MaestroHub>(x =>
        {
            x.EnableDetailedErrors = true;
            x.MaximumReceiveMessageSize = 64_000; // 64KB, default is 32KB
        })
        .AddMessagePackProtocol(options =>
        {
            options.SerializerOptions = MessagePackSerializerOptions.Standard
                .WithResolver(ContractlessStandardResolver.Instance)
                .WithCompression(MessagePackCompression.Lz4Block);
        });

    builder.Services.AddSerilog();
    builder.Services.AddSingleton(Log.Logger);

    builder.Services.AddMediatR(c =>
    {
        c.RegisterServicesFromAssemblies(typeof(AssemblyMarker).Assembly);
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Maestro API",
            Version = "v1",
            Description = "API for accessing Maestro session data"
        });

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }

        c.UseAllOfToExtendReferenceSchemas();
    });

    builder.Services.AddSingleton<SessionCache>();
    builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();
    builder.Services.AddTransient<IHubProxy, HubProxy>();

    builder.Services.AddRazorPages();

    var app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapHub<MaestroHub>(
        "/hub",
        opts =>
        {
            opts.AllowStatefulReconnects = true;
        });

    app.MapGet("/health", () => Results.Ok())
        .WithName("GetHealth")
        .WithDescription("Health check endpoint")
        .WithTags("Health")
        .Produces(200);

    // Session API
    var api = app.MapGroup("/api");

    api.MapGet("/sessions", (SessionCache cache) =>
    {
        var keys = cache.GetAll()
            .Select(s => s.Key)
            .ToArray();

        return Results.Ok(keys);
    })
    .WithName("GetSessions")
    .WithDescription("Returns all active session keys")
    .WithTags("Sessions")
    .Produces<SessionKey[]>();

    api.MapGet("/sessions/{environment}/{airportIdentifier}",
        (string environment, string airportIdentifier, SessionCache cache) =>
    {
        var session = cache.Get(environment, airportIdentifier);
        return session is null
            ? Results.NotFound()
            : Results.Ok(session);
    })
    .WithName("GetSession")
    .WithDescription("Returns the full session data for a specific airport")
    .WithTags("Sessions")
    .Produces<SessionDto>()
    .Produces(404);

    app.MapRazorPages();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
