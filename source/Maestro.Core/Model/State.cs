namespace Maestro.Core.Model;

public enum State
{
    /// <summary>
    ///     The flight has been activated in the FDP at a nearby departure airport and is pending departure.
    /// </summary>
    Pending,

    /// <summary>
    ///     The flight is new to the sequence and has not yet been sequenced.
    /// </summary>
    New,

    /// <summary>
    ///     The flight is being sequenced and it's landing time has not yet been locked in.
    ///     The flights position in the sequence is recomputed and updated as updates are received from Eurocat.
    /// </summary>
    Unstable,

    /// <summary>
    ///     The flight will keep it's position in the sequence unless another flight appears or disappears before it.
    /// </summary>
    Stable,

    /// <summary>
    ///     The flights position in the sequence is fixed.
    ///     All new flights should be positioned after this flight unless manually overridden.
    /// </summary>
    SuperStable,

    Overshoot,

    /// <summary>
    ///     No changes can be made to flights in this state.
    /// </summary>
    Frozen,

    /// <summary>
    ///     No changes can be made to flights in this state.
    /// </summary>
    Landed,

    /// <summary>
    ///     The flight has been temporarily removed from the sequence.
    /// </summary>
    Desequenced,

    /// <summary>
    ///     The flight has been permanently removed from the sequence and will not be sequenced again.
    /// </summary>
    Removed
}
