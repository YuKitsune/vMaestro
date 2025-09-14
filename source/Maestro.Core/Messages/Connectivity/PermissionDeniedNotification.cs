using MediatR;

namespace Maestro.Core.Messages.Connectivity;

public record PermissionDeniedNotification(string Action, string Message) : INotification;