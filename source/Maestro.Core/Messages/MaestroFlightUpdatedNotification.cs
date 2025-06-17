using MediatR;

namespace Maestro.Core.Messages;

public record MaestroFlightUpdatedNotification(FlightMessage Flight) : INotification;