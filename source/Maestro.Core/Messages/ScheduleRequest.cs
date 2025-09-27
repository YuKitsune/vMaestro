using MediatR;

namespace Maestro.Core.Messages;

public record ScheduleRequest(string AirportIdentifier) : IRequest;
