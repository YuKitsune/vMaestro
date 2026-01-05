using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public record InitializeConnectionRequest : IRequest<InitializeConnectionResponse>;
