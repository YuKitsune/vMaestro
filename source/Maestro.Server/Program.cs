using Maestro.Server;
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
