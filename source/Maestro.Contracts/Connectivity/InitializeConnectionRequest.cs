using MediatR;

namespace Maestro.Contracts.Connectivity;

public record InitializeConnectionRequest : IRequest<InitializeConnectionResponse>;
