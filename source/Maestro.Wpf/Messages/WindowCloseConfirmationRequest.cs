using MediatR;

namespace Maestro.Wpf.Messages;

public record WindowCloseConfirmationRequest(string AirportIdentifier) : IRequest<WindowCloseConfirmationResponse>;
public record WindowCloseConfirmationResponse(bool AllowClose);