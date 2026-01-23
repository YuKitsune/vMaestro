using MediatR;
using System.Text.Json.Serialization;
using Maestro.Core.Messages;

namespace Maestro.Core.Connectivity.Contracts;

public class RequestEnvelope
{
    public required string OriginatingCallsign { get; init; }
    public required string OriginatingConnectionId { get; init; }
    public required Role OriginatingRole { get; init; }
    public required IRelayableRequest Request { get; init; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "RequestType")]
[JsonDerivedType(typeof(ChangeRunwayRequest), "ChangeRunway")]
[JsonDerivedType(typeof(ChangeRunwayModeRequest), "ChangeRunwayMode")]
[JsonDerivedType(typeof(ChangeFeederFixEstimateRequest), "ChangeFeederFixEstimate")]
[JsonDerivedType(typeof(InsertFlightRequest), "InsertFlight")]
[JsonDerivedType(typeof(MoveFlightRequest), "MoveFlight")]
[JsonDerivedType(typeof(SwapFlightsRequest), "SwapFlights")]
[JsonDerivedType(typeof(RemoveRequest), "Remove")]
[JsonDerivedType(typeof(DesequenceRequest), "Desequence")]
[JsonDerivedType(typeof(MakePendingRequest), "MakePending")]
[JsonDerivedType(typeof(MakeStableRequest), "MakeStable")]
[JsonDerivedType(typeof(RecomputeRequest), "Recompute")]
[JsonDerivedType(typeof(ResumeSequencingRequest), "ResumeSequencing")]
[JsonDerivedType(typeof(ManualDelayRequest), "ManualDelay")]
[JsonDerivedType(typeof(CreateSlotRequest), "CreateSlot")]
[JsonDerivedType(typeof(ModifySlotRequest), "ModifySlot")]
[JsonDerivedType(typeof(DeleteSlotRequest), "DeleteSlot")]
[JsonDerivedType(typeof(ChangeApproachTypeRequest), "ChangeApproachType")]
public interface IRelayableRequest
{
    string AirportIdentifier { get; }
}
