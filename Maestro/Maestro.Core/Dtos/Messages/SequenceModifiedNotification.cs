using MediatR;

namespace Maestro.Core.Dtos.Messages;

public record SequenceModifiedNotification(SequenceDto Sequence) : INotification;
