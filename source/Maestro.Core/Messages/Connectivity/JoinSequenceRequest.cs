using MediatR;

namespace Maestro.Core.Messages.Connectivity;

// TODO: Return permission set

public record JoinSequenceRequest(string Partition, string AirportIdentifier, string Position) : IRequest<JoinSequenceResponse>;
public record JoinSequenceResponse(string ConnectionId, bool OwnsSequence, SequenceMessage? Sequence);
