using Maestro.Core.Dtos;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public record GetSequenceRequest(string AirportIdentifier) : IRequest<GetSequenceResponse>;

public record GetSequenceResponse(SequenceDto Sequence);

public class GetSequenceRequestHandler : IRequestHandler<GetSequenceRequest, GetSequenceResponse>
{
    readonly SequenceProvider _sequenceProvider;

    public GetSequenceRequestHandler(SequenceProvider sequenceProvider)
    {
        _sequenceProvider = sequenceProvider;
    }

    public Task<GetSequenceResponse> Handle(GetSequenceRequest request, CancellationToken cancellationToken)
    {
        var sequence = _sequenceProvider.TryGetSequence(request.AirportIdentifier);
        if (sequence is null)
            throw new MaestroException($"No sequence defined for {request.AirportIdentifier}");
        
        return Task.FromResult(new GetSequenceResponse(sequence.ToDto()));
    }
}