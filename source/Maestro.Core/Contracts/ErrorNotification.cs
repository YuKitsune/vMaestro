using MediatR;

namespace Maestro.Core.Contracts;

public record ErrorNotification(Exception Exception) : INotification;
