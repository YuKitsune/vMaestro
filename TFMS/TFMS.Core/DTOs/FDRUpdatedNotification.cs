using MediatR;

namespace TFMS.Core.DTOs;

public record FDRUpdatedNotification(FlightDataRecord FlightDataRecord) : INotification;
