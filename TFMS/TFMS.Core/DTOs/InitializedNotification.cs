using MediatR;

namespace TFMS.Core.DTOs;

public record InitializedNotification(FlightDataRecord[] FlightDataRecords) : INotification;
