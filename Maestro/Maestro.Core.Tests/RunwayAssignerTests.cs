using Maestro.Core.Configuration;
using Maestro.Core.Dtos.Configuration;
using Maestro.Core.Model;
using NSubstitute;
using Shouldly;

namespace Maestro.Core.Tests;

public class RunwayAssignerTests
{
    readonly RunwayAssigner _runwayAssigner;
    readonly RunwayAssignmentRule[] _assignmentRules;

    public RunwayAssignerTests()
    {
        var lookup = Substitute.For<IPerformanceLookup>();
        lookup.GetPerformanceDataFor(Arg.Is("C172")).Returns(new AircraftPerformanceData { IsJet = false, WakeCategory = WakeCategory.Light });
        lookup.GetPerformanceDataFor(Arg.Is("DH8D")).Returns(new AircraftPerformanceData { IsJet = false, WakeCategory = WakeCategory.Medium });
        lookup.GetPerformanceDataFor(Arg.Is("B738")).Returns(new AircraftPerformanceData { IsJet = true, WakeCategory = WakeCategory.Medium });
        lookup.GetPerformanceDataFor(Arg.Is("B744")).Returns(new AircraftPerformanceData { IsJet = true, WakeCategory = WakeCategory.Heavy });
        lookup.GetPerformanceDataFor(Arg.Is("A388")).Returns(new AircraftPerformanceData { IsJet = true, WakeCategory = WakeCategory.SuperHeavy });
        
        _runwayAssigner = new RunwayAssigner(lookup);

        _assignmentRules =
        [
            new RunwayAssignmentRule(
                "34R",
                "34R",
                jets: true,
                nonJets: true,
                heavy: false,
                medium: true,
                light: true,
                feederFixes: ["BOREE", "YAKKA", "MARLN"],
                priority: 1),
            new RunwayAssignmentRule(
                "34R",
                "34R",
                jets: true,
                nonJets: true,
                heavy: true,
                medium: true,
                light: true,
                feederFixes: ["RIVET", "WELSH"],
                priority: 2),
            new RunwayAssignmentRule(
                "34L",
                "34L",
                jets: true,
                nonJets: true,
                heavy: true,
                medium: true,
                light: false,
                feederFixes: ["RIVET", "WELSH"],
                priority: 1),
            new RunwayAssignmentRule(
                "34L",
                "34L",
                jets: true,
                nonJets: true,
                heavy: true,
                medium: true,
                light: false,
                feederFixes: ["BOREE", "YAKKA", "MARLN"],
                priority: 2)
        ];
    }
    
    [Fact]
    public void ReturnsEmpty_WhenNoMatchingRules()
    {
        var result = _runwayAssigner.FindBestRunways("A320", "RIVET", _assignmentRules);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ReturnsSingleMatch()
    {
        var result = _runwayAssigner.FindBestRunways("C172", "BOREE", _assignmentRules);
        result.ShouldBe(["34R"]);
    }

    [Fact]
    public void ReturnsMatchesInOrderOrPriority_WhenMultipleMatches()
    {
        var result1 = _runwayAssigner.FindBestRunways("B738", "BOREE", _assignmentRules);
        result1.ShouldBe(["34R", "34L"]);
        
        var result2 = _runwayAssigner.FindBestRunways("B738", "RIVET", _assignmentRules);
        result2.ShouldBe(["34L", "34R"]);
    }
}