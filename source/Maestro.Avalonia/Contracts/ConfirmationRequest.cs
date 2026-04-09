using MediatR;

namespace Maestro.Avalonia.Contracts;

public record ConfirmationRequest(string Title, string Message) : IRequest<ConfirmationResponse>;
public record ConfirmationResponse(bool Confirmed);
