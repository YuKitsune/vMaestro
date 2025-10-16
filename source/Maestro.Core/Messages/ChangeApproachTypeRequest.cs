using MediatR;

namespace Maestro.Core.Messages;

public record ChangeApproachTypeRequest(string AirportIdentifier, string Callsign, string ApproachType) : IRequest;
