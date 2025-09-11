using MediatR;

namespace Maestro.Core.Messages;

public record ErrorNotification(Exception Exception) : INotification;
