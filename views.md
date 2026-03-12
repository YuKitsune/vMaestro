# View configurations

The new airport configuration V2 schema defines new view configuration types.

## Ladders

For the selected view, N number of ladders should be displayed, stacked horizontally from left to right.

When one ladder is to be displayed, the labels should be drawn on the left, then the tick marks for that ladder, then the timeline (with left and right borders) containing the 5-minute intervals.
The reference time box should also be displayed at the bottom.

When a ladder is drawn, the tick marks for that ladder are to be drawn on the left side if this is an odd-numbered ladder, or the right side if it is an even numbered ladder.

There may be one timeline between two ladders.
When one or two ladders are drawn, the timeline should be drawn to the right of the first ladder, with the 5-minute markers, and the left and right borders.
Then three ladders are drawn, an additional timeline should be drawn to the right of the third ladder.

The order can generally be boiled down to:
Ladder N
Ticks N
Timeline
Ticks N+1
Ladder N+1
Ladder N+2
Ticks N+2
Timeline
Ticks N+3
Ladder N+3

etc.

Each ladder must have a timeline on either it's left or right side.
Ticks should not be drawn next to the timeline if there is no ladder on that side of the timeline.

At the bottom of each timeline is a time reference box.

On either side of the time reference box, the feeder fixes and/or runways the ladder is filtered to should be displayed.
If no feeder fixes are in the filter, then "All Feeders" should be displayed".
If no runways re in the filter, then "All Runways" should be displayed.

## Labels

Labal items are to be displayed for each flight.
The filters specified in the ladder configuration define which ladder a flight may appear on.
A flight may appear on multiple ladders if it matches the filters for multiple ladders.

Label items are to be rendered programmatically using LabelItemConfigurationV2.
Label items can be rendered from right-to-left when placed on an odd-numbered ladder, or from left-to-right when placed on an even-numbered ladder.

Each label item configuration will result in a string being produced.
The length of the string is fixed to `LabelItemConfigurationV2.Width`.
Padding characters may be applied to the string length, no content may occupy the padding characters.
When rendered from right-to-left, the padding should be applied to the beginning of the string.

Each string will also be coloured as defined by `LabelItemConfigurationV2.ColourSources`. Leave the implementation of colour selection to me.
Once each string and colour for each string has been determined, the text must be rendered in WPF.

The width of the ladder is defined by the Width + Padding of all label items defined in the label layout configuration for that ladder.
Use the `_` character Width + Padding times to determine the pixel width.
