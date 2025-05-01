using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public record GetSequenceRequest(string AirportIdentifier) : IRequest<GetSequenceResponse>;

public record GetSequenceResponse(Sequence Sequence);

public class GetSequenceRequestHandler(ISequenceProvider sequenceProvider)
    : IRequestHandler<GetSequenceRequest, GetSequenceResponse>
{
    public Task<GetSequenceResponse> Handle(GetSequenceRequest request, CancellationToken cancellationToken)
    {
        var sequence = sequenceProvider.TryGetSequence(request.AirportIdentifier);
        if (sequence is null)
            throw new MaestroException($"No sequence defined for {request.AirportIdentifier}");
        
        return Task.FromResult(new GetSequenceResponse(sequence));
    }
}