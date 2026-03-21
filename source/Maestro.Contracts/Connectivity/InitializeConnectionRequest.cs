using MediatR;
using MessagePack;

namespace Maestro.Contracts.Connectivity;

[MessagePackObject]
public record InitializeConnectionRequest : IRequest<InitializeConnectionResponse>;
