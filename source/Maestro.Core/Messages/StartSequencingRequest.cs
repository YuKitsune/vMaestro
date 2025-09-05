using Maestro.Core.Handlers;
using MediatR;

namespace Maestro.Core.Messages;

public record StartSequencingRequest(string AirportIdentifier, RunwayModeDto RunwayMode) : IRequest;
