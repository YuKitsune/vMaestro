namespace Maestro.Core.Configuration;

public class LadderConfiguration
{
    /// <summary>
    ///     The feeder fixes to filter this ladder to.
    /// </summary>
    public string[] FeederFixes { get; init; } = [];

    /// <summary>
    ///     The runways to filter this ladder to.
    /// </summary>
    public string[] Runways { get; init; } = [];
}
