using MediatR;

namespace Maestro.Core.Messages;

public record ResumeSequencingResponse;
public record ResumeSequencingRequest(string AirportIdentifier, string Callsign) : IRequest<ResumeSequencingResponse>;