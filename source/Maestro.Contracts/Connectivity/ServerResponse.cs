namespace Maestro.Contracts.Connectivity;

public class ServerResponse
{
    public bool Success { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;

    public static ServerResponse CreateSuccess() => new() { Success = true };
    public static ServerResponse CreateFailure(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}
