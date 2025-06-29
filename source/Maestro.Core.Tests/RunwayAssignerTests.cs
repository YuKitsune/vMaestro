using Maestro.Core.Configuration;
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
        lookup.GetPerformanceDataFor(Arg.Is("C172")).Returns(new AircraftPerformanceData { Type = "C172", AircraftCategory = AircraftCategory.NonJet, WakeCategory = WakeCategory.Light });
        lookup.GetPerformanceDataFor(Arg.Is("DH8D")).Returns(new AircraftPerformanceData { Type = "DH8D", AircraftCategory = AircraftCategory.NonJet, WakeCategory = WakeCategory.Medium });
        lookup.GetPerformanceDataFor(Arg.Is("B738")).Returns(new AircraftPerformanceData { Type = "B738", AircraftCategory = AircraftCategory.Jet, WakeCategory = WakeCategory.Medium });
        lookup.GetPerformanceDataFor(Arg.Is("B744")).Returns(new AircraftPerformanceData { Type = "B744", AircraftCategory = AircraftCategory.Jet, WakeCategory = WakeCategory.Heavy });
        lookup.GetPerformanceDataFor(Arg.Is("A388")).Returns(new AircraftPerformanceData { Type = "A388", AircraftCategory = AircraftCategory.Jet, WakeCategory = WakeCategory.SuperHeavy });
        
        _runwayAssigner = new RunwayAssigner(lookup);

        _assignmentRules =
        [
            new RunwayAssignmentRule(
                0,
                ["RIVET", "AKMIR", "BOREE", "MEPIL", "MARLN"],
                [WakeCategory.Heavy, WakeCategory.SuperHeavy],
                ["34L", "16R", "07", "25"]),
            
            new RunwayAssignmentRule(
                1,
                ["RIVET", "AKMIR"],
                [WakeCategory.Light, WakeCategory.Medium],
                ["34L", "16R", "07", "25"]),
            
            new RunwayAssignmentRule(
                2,
                ["RIVET", "AKMIR"],
                [WakeCategory.Light, WakeCategory.Medium],
                ["34R", "16L"]),
            
            new RunwayAssignmentRule(
                1,
                ["BOREE", "MEPIL", "MARLN"],
                [WakeCategory.Light, WakeCategory.Medium],
                ["34R", "16L", "07", "25"]),
            
            new RunwayAssignmentRule(
                2,
                ["BOREE", "MEPIL", "MARLN"],
                [WakeCategory.Light, WakeCategory.Medium],
                ["34L", "16R"]),
        ];
    }
    
    [Fact]
    public void ReturnsEmpty_WhenNoMatchingRules()
    {
        var result = _runwayAssigner.FindBestRunways("A320", "RIVET", _assignmentRules);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void WhenOneRuleMatches_ReturnsRunwaysInOrderOfPriority()
    {
        var result = _runwayAssigner.FindBestRunways("B744", "BOREE", _assignmentRules);
        result.ShouldBe(["34L", "16R", "07", "25"]);
    }

    [Fact]
    public void WhenMultipleRulesMatches_ReturnsRunwaysInOrderOfPriority()
    {
        var result1 = _runwayAssigner.FindBestRunways("B738", "BOREE", _assignmentRules);
        result1.ShouldBe(["34R", "16L", "07", "25", "34L", "16R"]);
        
        var result2 = _runwayAssigner.FindBestRunways("B738", "RIVET", _assignmentRules);
        result2.ShouldBe(["34L", "16R", "07", "25", "34R", "16L"]);
    }
}