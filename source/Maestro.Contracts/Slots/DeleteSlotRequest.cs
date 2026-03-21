using Maestro.Contracts.Connectivity;
using MediatR;

namespace Maestro.Contracts.Slots;

public record DeleteSlotRequest(string AirportIdentifier, Guid SlotId) : IRequest, IRelayableRequest;
