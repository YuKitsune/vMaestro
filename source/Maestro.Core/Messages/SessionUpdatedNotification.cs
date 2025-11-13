using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Messages;

public record SessionUpdatedNotification(
    string AirportIdentifier,
    SessionMessage Session)
    : INotification;
