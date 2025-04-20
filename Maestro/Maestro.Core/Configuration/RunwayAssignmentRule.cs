using Maestro.Core.Model;

namespace Maestro.Core.Configuration;

public class RunwayAssignmentRule(
    int priority,
    string[] feederFixes,
    WakeCategory[] wakeCategories,
    string[] runways)
{
    public int Priority { get; } = priority;
    public string[] FeederFixes { get; } = feederFixes;
    public WakeCategory[] WakeCategories { get; } = wakeCategories;
    public string[] Runways { get; } = runways;
}