using Maestro.Core.Configuration;
using MediatR;

namespace Maestro.Core.Messages.Connectivity;

public record JoinSequenceRequest(string Partition, string AirportIdentifier, string Position, Role Role) : IRequest<JoinSequenceResponse>;
public record JoinSequenceResponse(string ConnectionId, bool OwnsSequence, SequenceMessage? Sequence, IReadOnlyDictionary<string, Role[]> Permissions);
