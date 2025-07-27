using MediatR;

namespace Maestro.Core.Messages;

public class SequenceChangedNotification(SlotBasedSequenceDto sequence) : INotification
{
    public SlotBasedSequenceDto Sequence { get; } = sequence;
}
