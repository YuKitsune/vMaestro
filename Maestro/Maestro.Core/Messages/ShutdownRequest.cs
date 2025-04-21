using MediatR;

namespace Maestro.Core.Messages;

public record ShutdownResponse;

public class ShutdownRequest : IRequest<ShutdownResponse>;