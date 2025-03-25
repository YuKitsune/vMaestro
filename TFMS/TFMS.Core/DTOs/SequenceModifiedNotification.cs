using MediatR;
using TFMS.Core.Model;

namespace TFMS.Core.DTOs;

public record SequenceModifiedNotification(Sequence Sequence) : INotification;
