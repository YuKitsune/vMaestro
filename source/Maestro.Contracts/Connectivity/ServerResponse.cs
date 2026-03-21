using MessagePack;

namespace Maestro.Contracts.Connectivity;

[MessagePackObject]
public class ServerResponse
{
    [Key(0)]
    public bool Success { get; init; }

    [Key(1)]
    public string ErrorMessage { get; init; } = string.Empty;

    public static ServerResponse CreateSuccess() => new() { Success = true };
    public static ServerResponse CreateFailure(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}
