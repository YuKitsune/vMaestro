using MediatR;

namespace Maestro.Wpf.Messages;

public record ConfirmationRequest(string Message) : IRequest<ConfirmationResponse>;
public record ConfirmationResponse(bool Confirmed);
