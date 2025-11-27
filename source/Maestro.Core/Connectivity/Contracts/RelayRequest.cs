using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public class RelayRequest : IRequest<ServerResponse>
{
    public required RequestEnvelope Envelope { get; init; }
    public required string ActionKey { get; init; }
}

public class ServerResponse
{
    public bool Success { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;

    public static ServerResponse CreateSuccess() => new() { Success = true };
    public static ServerResponse CreateFailure(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}
