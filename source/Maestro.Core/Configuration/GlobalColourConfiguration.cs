using Maestro.Contracts.Shared;

namespace Maestro.Core.Configuration;

/// <summary>
///     Shared colour configuration
/// </summary>
public class GlobalColourConfiguration
{
    /// <summary>
    ///     The colors to apply to specific states.
    /// </summary>
    public Dictionary<State, Color> States { get; init; } = new();

    /// <summary>
    ///     The colors to apply to specific control actions.
    /// </summary>
    public Dictionary<ControlAction, Color> ControlActions { get; init; } = new();

    /// <summary>
    ///     The color to apply to flights scheduled to land in a deferred runway mode.
    /// </summary>
    public string DeferredRunwayMode { get; init; } = string.Empty;
}
