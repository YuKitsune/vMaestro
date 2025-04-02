using MediatR;

namespace Maestro.Core.Dtos.Messages;

public record SequenceModifiedNotification(SequenceDTO Sequence) : INotification;
