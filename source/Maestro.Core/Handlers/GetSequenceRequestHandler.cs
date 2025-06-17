using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public record GetSequenceRequest(string AirportIdentifier) : IRequest<GetSequenceResponse>;

public record GetSequenceResponse(SequenceMessage Sequence);

public class GetSequenceRequestHandler(ISequenceProvider sequenceProvider)
    : IRequestHandler<GetSequenceRequest, GetSequenceResponse>
{
    public Task<GetSequenceResponse> Handle(GetSequenceRequest request, CancellationToken cancellationToken)
    {
        var sequence = sequenceProvider.GetReadOnlySequence(request.AirportIdentifier);
        return Task.FromResult(new GetSequenceResponse(sequence));
    }
}