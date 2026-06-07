using Maestro.Contracts.Connectivity;
using MediatR;
using MessagePack;

namespace Maestro.Contracts.Runway;

[MessagePackObject]
public record ChangeLandingRatesRequest(
    [property: Key(0)] string AirportIdentifier,
    [property: Key(1)] IReadOnlyDictionary<string, TimeSpan> NewLandingRates,
    [property: Key(2)] DateTimeOffset ChangeTime)
    : IRequest, IRelayableRequest;
