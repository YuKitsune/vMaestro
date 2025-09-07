using MediatR;

namespace Maestro.Wpf.Messages;

public record ConfirmationRequest(string Title, string Message) : IRequest<ConfirmationResponse>;
public record ConfirmationResponse(bool Confirmed);
