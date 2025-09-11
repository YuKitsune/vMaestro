using MediatR;

namespace Maestro.Core.Messages.Connectivity;

public record LeaveSequenceRequest(string AirportIdentifier) : IRequest;
