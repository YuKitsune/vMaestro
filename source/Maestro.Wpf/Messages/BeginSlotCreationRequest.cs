using MediatR;

namespace Maestro.Wpf.Messages;

public enum SlotCreationReferencePoint
{
    Before,
    After
}

public record BeginSlotCreationRequest(
    string AirportIdentifier,
    string[] RunwayIdentifiers,
    DateTimeOffset ReferenceLandingTime,
    SlotCreationReferencePoint ReferencePoint) : IRequest;
