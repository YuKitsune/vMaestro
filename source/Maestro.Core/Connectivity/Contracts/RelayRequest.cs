using Maestro.Core.Connectivity.Contracts;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Core.Handlers;

public class RelayRequest : IRequest<RelayResponse>
{
    public required RequestEnvelope Envelope { get; init; }
    public required string ActionKey { get; init; }
}

public class RelayResponse
{
    public bool Success { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;

    public static RelayResponse CreateSuccess() => new() { Success = true };
    public static RelayResponse CreateFailure(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}
