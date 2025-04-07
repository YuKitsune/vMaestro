using Maestro.Core.Model;

namespace Maestro.Core.Dtos.Configuration;

public class SeparationRuleConfiguration(
    string name,
    TimeSpan interval,
    WakeCategory wakeCategoryLeader,
    WakeCategory wakeCategoryFollower,
    string[] aircraftTypeCodesLeader,
    string[] aircraftTypeCodesFollower)
{
    public string Name { get; } = name;
    public TimeSpan Interval { get; } = interval;
    public WakeCategory WakeCategoryLeader { get; } = wakeCategoryLeader;
    public WakeCategory WakeCategoryFollower { get; } = wakeCategoryFollower;
    public string[] AircraftTypeCodesLeader { get; } = aircraftTypeCodesLeader;
    public string[] AircraftTypeCodesFollower { get; } = aircraftTypeCodesFollower;
}