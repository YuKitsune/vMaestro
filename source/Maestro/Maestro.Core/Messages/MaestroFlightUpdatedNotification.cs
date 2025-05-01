using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Messages;

public record MaestroFlightUpdatedNotification(Flight Flight) : INotification;