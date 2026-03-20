using System.Text.Json.Serialization;
using Maestro.Contracts.Flights;
using Maestro.Contracts.Runway;
using Maestro.Contracts.Slots;

namespace Maestro.Contracts.Connectivity;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "RequestType")]
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
[JsonDerivedType(typeof(ChangeRunwayRequest), "ChangeRunway")]
[JsonDerivedType(typeof(ChangeFeederFixEstimateRequest), "ChangeFeederFixEstimate")]
[JsonDerivedType(typeof(ChangeApproachTypeRequest), "ChangeApproachType")]
[JsonDerivedType(typeof(CreateSlotRequest), "CreateSlot")]
[JsonDerivedType(typeof(ModifySlotRequest), "ModifySlot")]
[JsonDerivedType(typeof(DeleteSlotRequest), "DeleteSlot")]
[JsonDerivedType(typeof(ChangeRunwayModeRequest), "ChangeRunwayMode")]
public interface IRelayableRequest
{
    string AirportIdentifier { get; }
}
