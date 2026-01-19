using Maestro.Core.Connectivity.Contracts;
using MediatR;

namespace Maestro.Core.Messages;

public record DeleteSlotRequest(string AirportIdentifier, Guid SlotId) : IRequest, IRelayableRequest;
