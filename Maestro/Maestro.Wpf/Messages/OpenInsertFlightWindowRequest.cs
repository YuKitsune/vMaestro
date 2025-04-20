using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.Messages;

public record OpenInsertFlightWindowResponse;
public record OpenInsertFlightWindowRequest(string AirportIdentifier, string Callsign, InsertionPoint InsertionPoint) : IRequest<OpenInsertFlightWindowResponse>;