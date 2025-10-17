using MediatR;

namespace Maestro.Core.Messages;

public record GetApproachTypesRequest(string AirportIdentifier) : IRequest<GetApproachTypesResponse>;
public record GetApproachTypesResponse(RunwayApproachTypes[] Runways);
public record RunwayApproachTypes(string RunwayIdentifier, string[] ApproachTypes);
