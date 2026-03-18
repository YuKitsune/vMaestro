using System.Reflection;
using System.Text.Json.Serialization;
using Maestro.Core;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using Maestro.Server;
using Microsoft.AspNetCore.SignalR;
using Microsoft.OpenApi.Models;
using Serilog;
using AssemblyMarker = Maestro.Server.AssemblyMarker;

var loggerConfig = new LoggerConfiguration()
    .WriteTo.Console();

var seqServerUrl = Environment.GetEnvironmentVariable("SEQ_SERVER_URL");
if (!string.IsNullOrEmpty(seqServerUrl))
{
    loggerConfig.WriteTo.Seq(seqServerUrl);
}

Log.Logger = loggerConfig.CreateLogger();

// Log version information at startup
var assembly = Assembly.GetExecutingAssembly();
var version = AssemblyVersionHelper.GetVersion(assembly);
Log.Information("Starting Maestro.Server version {Version}", version);

// TODO: Basic web UI with supervisor functions
//  - View connected users
//  - Re-assign sequence owner
//  - Administrative messages
//  - Kick user

// TODO: Error handling
// - Don't throw exceptions, return results
// - Write exceptions to logs
// - Log notifications and requests are they're being sent and received

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSignalR(options =>
        {
            options.AddFilter<SignalRLoggingFilter>();
        })
        .AddHubOptions<MaestroHub>(x =>
        {
            x.MaximumReceiveMessageSize = 32_000_000; // TODO: Just send smaller messages!!!
            x.EnableDetailedErrors = true;
        })
        .AddJsonProtocol();

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
        c.SchemaFilter<EnumSchemaFilter>();
    });
    builder.Services.AddSingleton<SessionCache>();
    builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();
    builder.Services.AddTransient<IHubProxy, HubProxy>();
    builder.Services.AddHostedService<SystemMetricsService>();

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    builder.Services.AddRazorPages();

    var app = builder.Build();

    app.UseStaticFiles();

    app.UseSwagger(c =>
    {
        c.RouteTemplate = "api/swagger/{documentName}/swagger.json";
    });
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/api/swagger/v1/swagger.json", "Maestro API v1");
        c.RoutePrefix = "api/swagger";
    });

    app.MapHub<MaestroHub>("/hub");
    app.MapHub<DashboardHub>("/dashboard-hub");

    // SPA fallback for Docusaurus documentation
    app.MapFallback("/docs/{**path}", async context =>
    {
        var path = context.Request.Path.Value ?? "";

        // Don't handle API requests
        if (path.StartsWith("/api/") || path.StartsWith("/hub"))
        {
            context.Response.StatusCode = 404;
            return;
        }

        var docsIndexPath = Path.Combine(app.Environment.WebRootPath, "docs", "index.html");
        if (File.Exists(docsIndexPath))
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(docsIndexPath);
        }
        else
        {
            context.Response.StatusCode = 404;
        }
    });

    // Session API
    var api = app.MapGroup("/api");

    api.MapGet("/health", () => Results.Ok())
        .WithName("GetHealth")
        .WithDescription("Health check endpoint")
        .WithTags("Health")
        .Produces(200);

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

    api.MapGet("/sessions/{partition}/{airportIdentifier}",
        (string partition, string airportIdentifier, SessionCache cache) =>
    {
        var session = cache.Get(partition, airportIdentifier);
        return session is null
            ? Results.NotFound()
            : Results.Ok(session);
    })
    .WithName("GetSession")
    .WithDescription("Returns the full session data for a specific airport")
    .WithTags("Sessions")
    .Produces<SessionMessage>()
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
