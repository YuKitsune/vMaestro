using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public class InitializeRequestHandler(
    IAirportConfigurationProvider airportConfigurationProvider,
    ISequenceProvider sequenceProvider)
    : IRequestHandler<InitializeRequest, InitializeResponse>
{
    public Task<InitializeResponse> Handle(InitializeRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(
            new InitializeResponse(
                airportConfigurationProvider.GetAirportConfigurations()
                    .Select(airportConfiguration =>
                        new InitializationItem(
                            airportConfiguration.Identifier,
                            airportConfiguration.Views,
                            airportConfiguration.RunwayModes.Select(r => r.ToMessage()).ToArray(),
                            sequenceProvider.GetReadOnlySequence(airportConfiguration.Identifier)))
                    .ToArray()));
    }
}