using MediatR;

namespace TFMS.Core.Dtos.Messages;

public record SequenceModifiedNotification(SequenceDTO Sequence) : INotification;
