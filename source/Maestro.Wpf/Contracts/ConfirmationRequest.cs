using MediatR;

namespace Maestro.Wpf.Contracts;

public record ConfirmationRequest(string Title, string Message) : IRequest<ConfirmationResponse>;
public record ConfirmationResponse(bool Confirmed);
