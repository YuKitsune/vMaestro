using Maestro.Core.Integration;
using Maestro.Core.Model;
using Maestro.Core.Tests.Fixtures;
using NSubstitute;

[assembly: AssemblyFixture(typeof(PerformanceLookupFixture))]

namespace Maestro.Core.Tests.Fixtures;

public class PerformanceLookupFixture
{
    public IPerformanceLookup Instance
    {
        get
        {
            var lookup = Substitute.For<IPerformanceLookup>();

            lookup.GetPerformanceDataFor(Arg.Is(AircraftTypes.LightProp))
                .Returns(new AircraftPerformanceData(
                    AircraftTypes.LightProp,
                    AircraftCategory.NonJet,
                    WakeCategory.Light));

            lookup.GetPerformanceDataFor(Arg.Is(AircraftTypes.MediumProp))
                .Returns(new AircraftPerformanceData(
                    AircraftTypes.MediumProp,
                    AircraftCategory.NonJet,
                    WakeCategory.Medium));

            lookup.GetPerformanceDataFor(Arg.Is(AircraftTypes.MediumJet))
                .Returns(new AircraftPerformanceData(
                    AircraftTypes.MediumJet,
                    AircraftCategory.Jet,
                    WakeCategory.Medium));

            lookup.GetPerformanceDataFor(Arg.Is(AircraftTypes.HeavyJet))
                .Returns(new AircraftPerformanceData(
                    AircraftTypes.HeavyJet,
                    AircraftCategory.Jet,
                    WakeCategory.Heavy));

            lookup.GetPerformanceDataFor(Arg.Is(AircraftTypes.SuperHeavyJet))
                .Returns(new AircraftPerformanceData(
                    AircraftTypes.SuperHeavyJet,
                    AircraftCategory.Jet,
                    WakeCategory.SuperHeavy));

            return lookup;
        }
    }
}
