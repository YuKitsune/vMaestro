using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Messages;

public record SequenceModifiedNotification(Sequence Sequence) : INotification;
