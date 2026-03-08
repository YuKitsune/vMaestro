namespace Maestro.Core.Configuration;

public class ViewConfiguration
{
    /// <summary>
    ///     The Identifier for this view.
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    ///     The Identifier of the label layout to use in this view.
    /// </summary>
    public required string LabelLayout { get; init; }

    /// <summary>
    ///     The time which flights will be positioned on the ladder by.
    ///     If set to <see cref="LadderReference.LandingTime"/>, flights will be positioned on the lader based on their STA.
    ///     If set to <see cref="LadderReference.FeederFixTime"/>, flights will be positioned on the lader based on their STA_FF.
    /// </summary>
    public required LadderReference Reference { get; init; }

    /// <summary>
    ///     The window of time to be displayed in the sequence display area.
    /// </summary>
    public required int TimeWindowMinutes { get; init; }

    /// <summary>
    ///     The direction the timelines should scroll.
    /// </summary>
    public TimelineDirection Direction { get; init; } = TimelineDirection.Down;

    public required LadderConfiguration[] Ladders { get; init; }
}
