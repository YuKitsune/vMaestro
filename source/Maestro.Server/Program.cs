using Maestro.Server;
using Maestro.Server.Handlers;
using MediatR;
using Newtonsoft.Json.Serialization;
using Serilog;

var loggerConfig = new LoggerConfiguration()
    .WriteTo.Console();

var seqServerUrl = Environment.GetEnvironmentVariable("SEQ_SERVER_URL");
if (!string.IsNullOrEmpty(seqServerUrl))
{
    loggerConfig.WriteTo.Seq(seqServerUrl);
}

Log.Logger = loggerConfig.CreateLogger();

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
    builder.Services.AddSignalR()
        .AddHubOptions<MaestroHub>(x =>
        {
            x.MaximumReceiveMessageSize = 32_000_000; // TODO: Just send smaller messages!!!
            x.EnableDetailedErrors = true;
        })
        .AddNewtonsoftJsonProtocol(x => x.PayloadSerializerSettings.ContractResolver = new DefaultContractResolver());

    builder.Services.AddSerilog();
    builder.Services.AddSingleton(Log.Logger);

    builder.Services.AddMediatR(c =>
    {
        c.RegisterServicesFromAssemblies(typeof(AssemblyMarker).Assembly);
    });

    builder.Services.AddTransient(typeof(IRequestHandler<>), typeof(RelayToMasterRequestHandler<>));

    builder.Services.AddSingleton<SequenceCache>();
    builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();
    builder.Services.AddTransient<IHubProxy, HubProxy>();

    var app = builder.Build();

    app.UseStaticFiles();

    app.MapHub<MaestroHub>("/hub");
    app.MapGet("/health", () => Results.Ok());
    app.MapFallbackToFile("index.html");
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
