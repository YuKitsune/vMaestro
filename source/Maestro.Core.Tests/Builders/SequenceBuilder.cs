using Maestro.Core.Model;

namespace Maestro.Core.Tests.Fixtures;

public class SequenceBuilder(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    readonly List<Flight?> _flights = [];

    public SequenceBuilder WithFlight(Flight? flight)
    {
        _flights.Add(flight);
        return this;
    }

    public SlotBasedSequence Build()
    {
        var airportConfiguration = airportConfigurationFixture.Instance;
        var sequence = new SlotBasedSequence(
            airportConfiguration,
            airportConfiguration.RunwayModes.First(),
            clockFixture.Instance.UtcNow());

        for (var i = 0; i < _flights.Count; i++)
        {
            var flight = _flights[i];
            if (flight is null)
                continue;

            var slot = sequence.Slots[i];
            slot.AllocateTo(flight);
        }

        return sequence;
    }
}
