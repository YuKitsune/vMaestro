using System.Text.Json.Serialization;
using MessagePack;

namespace Maestro.Contracts.Flights;

/// <summary>
/// Represents how a flight's landing runway was assigned.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AutomaticRunwayAssignmentDto), "Automatic")]
[JsonDerivedType(typeof(ManualRunwayAssignmentDto), "Manual")]
[Union(0, typeof(AutomaticRunwayAssignmentDto))]
[Union(1, typeof(ManualRunwayAssignmentDto))]
public interface IRunwayAssignmentDto
{
    string RunwayIdentifier { get; }
}

/// <summary>
/// The runway was assigned by Maestro and may be reassigned by the scheduling algorithm.
/// </summary>
[MessagePackObject]
public record AutomaticRunwayAssignmentDto(
    [property: Key(0)] string RunwayIdentifier)
    : IRunwayAssignmentDto;

/// <summary>
/// The runway was assigned by the controller and must not be changed by the scheduling algorithm.
/// </summary>
[MessagePackObject]
public record ManualRunwayAssignmentDto(
    [property: Key(0)] string RunwayIdentifier)
    : IRunwayAssignmentDto;
