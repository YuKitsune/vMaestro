using Maestro.Server;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

// TODO: Basic web UI with supervisor functions
//  - View connected users
//  - Re-assign sequence owner
//  - Administrative messages
//  - Kick user

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSignalR()
        .AddHubOptions<MaestroHub>(x => x.MaximumReceiveMessageSize = 32_000_000) // TODO: Just send smaller messages!!!
        .AddNewtonsoftJsonProtocol();

    builder.Services.AddSerilog();

    var app = builder.Build();
    app.MapHub<MaestroHub>("/hub");
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
