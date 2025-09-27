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
                .Returns(new AircraftPerformanceData
                {
                    TypeCode = AircraftTypes.LightProp,
                    AircraftCategory = AircraftCategory.NonJet,
                    WakeCategory = WakeCategory.Light
                });

            lookup.GetPerformanceDataFor(Arg.Is(AircraftTypes.MediumProp))
                .Returns(new AircraftPerformanceData
                {
                    TypeCode = AircraftTypes.MediumProp,
                    AircraftCategory = AircraftCategory.NonJet,
                    WakeCategory = WakeCategory.Medium
                });

            lookup.GetPerformanceDataFor(Arg.Is(AircraftTypes.MediumJet))
                .Returns(new AircraftPerformanceData
                {
                    TypeCode = AircraftTypes.MediumJet,
                    AircraftCategory = AircraftCategory.Jet,
                    WakeCategory = WakeCategory.Medium
                });

            lookup.GetPerformanceDataFor(Arg.Is(AircraftTypes.HeavyJet))
                .Returns(new AircraftPerformanceData
                {
                    TypeCode = AircraftTypes.HeavyJet,
                    AircraftCategory = AircraftCategory.Jet,
                    WakeCategory = WakeCategory.Heavy
                });

            lookup.GetPerformanceDataFor(Arg.Is(AircraftTypes.SuperHeavyJet))
                .Returns(new AircraftPerformanceData
                {
                    TypeCode = AircraftTypes.SuperHeavyJet,
                    AircraftCategory = AircraftCategory.Jet,
                    WakeCategory = WakeCategory.SuperHeavy
                });

            return lookup;
        }
    }
}
