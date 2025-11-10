using MediatR;

namespace Maestro.Core.Messages;

public record RestoreSequenceRequest(string AirportIdentifier, SequenceMessage Sequence) : IRequest;
