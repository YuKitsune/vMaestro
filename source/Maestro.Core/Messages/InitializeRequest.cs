using System.Data;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using MediatR;

namespace Maestro.Core.Messages;

public record InitializeRequest : IRequest<InitializeResponse>;

public record InitializeResponse(InitializationItem[] Sequences);

// TODO: Use DTO for Views
public record InitializationItem(
    string AirportIdentifier,
    ViewConfiguration[] Views,
    RunwayModeDto[] RunwayModes,
    SequenceMessage Sequence);
    