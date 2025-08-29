using Maestro.Core.Configuration;
using Maestro.Core.Model;

namespace Maestro.Core.Tests.Mocks;

public class MockEstimateProvider(DateTimeOffset? feederFixEstimate, DateTimeOffset? landingEstimate) : IEstimateProvider
{
    DateTimeOffset? FeederFixEstimate { get; set; } = feederFixEstimate;
    DateTimeOffset? LandingEstimate { get; set; } = landingEstimate;

    public DateTimeOffset? GetFeederFixEstimate(
        AirportConfiguration airportConfiguration,
        string feederFixIdentifier,
        DateTimeOffset systemEstimate,
        FlightPosition? flightPosition)
    {
        return FeederFixEstimate;
    }

    public DateTimeOffset? GetLandingEstimate(Flight flight, DateTimeOffset? systemEstimate)
    {
        return LandingEstimate;
    }
}
