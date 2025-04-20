using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.Messages;

public record OpenInsertSlotWindowResponse;
public record OpenInsertSlotWindowRequest(string AirportIdentifier, string Callsign, InsertionPoint InsertionPoint) : IRequest<OpenInsertSlotWindowResponse>;