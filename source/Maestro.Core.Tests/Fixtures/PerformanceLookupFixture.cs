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

            lookup.GetPerformanceDataFor(Arg.Is("C172"))
                .Returns(new AircraftPerformanceData
                {
                    Type = "C172",
                    AircraftCategory = AircraftCategory.NonJet,
                    WakeCategory = WakeCategory.Light
                });

            lookup.GetPerformanceDataFor(Arg.Is("DH8D"))
                .Returns(new AircraftPerformanceData
                {
                    Type = "DH8D",
                    AircraftCategory = AircraftCategory.NonJet,
                    WakeCategory = WakeCategory.Medium
                });

            lookup.GetPerformanceDataFor(Arg.Is("B738"))
                .Returns(new AircraftPerformanceData
                {
                    Type = "B738",
                    AircraftCategory = AircraftCategory.Jet,
                    WakeCategory = WakeCategory.Medium
                });

            lookup.GetPerformanceDataFor(Arg.Is("B744"))
                .Returns(new AircraftPerformanceData
                {
                    Type = "B744",
                    AircraftCategory = AircraftCategory.Jet,
                    WakeCategory = WakeCategory.Heavy
                });

            lookup.GetPerformanceDataFor(Arg.Is("A388"))
                .Returns(new AircraftPerformanceData
                {
                    Type = "A388",
                    AircraftCategory = AircraftCategory.Jet,
                    WakeCategory = WakeCategory.SuperHeavy
                });

            return lookup;
        }
    }
}
