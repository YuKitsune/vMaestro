namespace Maestro.Core.Configuration;

public class ServerConfiguration
{
    public required Uri Uri { get; init; }
    public required string[] Partitions { get; init; } = ["Default"];
    public required int TimeoutSeconds { get; init; } = 30;
}
