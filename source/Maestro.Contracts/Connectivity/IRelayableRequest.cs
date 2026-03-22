using System.Text.Json.Serialization;
using Maestro.Contracts.Flights;
using Maestro.Contracts.Runway;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Slots;
using MessagePack;

namespace Maestro.Contracts.Connectivity;

// JSON attributes
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
[JsonDerivedType(typeof(ModifyWindRequest), "ModifyWind")]

// Message Pack attributes
[Union(0, typeof(InsertFlightRequest))]
[Union(1, typeof(MoveFlightRequest))]
[Union(2, typeof(SwapFlightsRequest))]
[Union(3, typeof(RemoveRequest))]
[Union(4, typeof(DesequenceRequest))]
[Union(5, typeof(MakePendingRequest))]
[Union(6, typeof(MakeStableRequest))]
[Union(7, typeof(RecomputeRequest))]
[Union(8, typeof(ResumeSequencingRequest))]
[Union(9, typeof(ManualDelayRequest))]
[Union(10, typeof(ChangeRunwayRequest))]
[Union(11, typeof(ChangeFeederFixEstimateRequest))]
[Union(12, typeof(ChangeApproachTypeRequest))]
[Union(13, typeof(CreateSlotRequest))]
[Union(14, typeof(ModifySlotRequest))]
[Union(15, typeof(DeleteSlotRequest))]
[Union(16, typeof(ChangeRunwayModeRequest))]
[Union(17, typeof(ModifyWindRequest))]
public interface IRelayableRequest
{
    string AirportIdentifier { get; }
}
