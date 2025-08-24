using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.Messages;

public record OpenInsertSlotWindowRequest(string AirportIdentifier, string Callsign) : IRequest;
