# System Overview

<!-- TODO: ChatGPT, please fill this in. -->

## Terminology and Abbreviations

| Term | Meaning |
| ---- | ------- |
| Feeder Fix (FF) | A point on the TMA boundary. |
| `ETA`[^2] | Estimated time of arrival **at the runway**. |
| `STA` | **Scheduled** time of arrival at the runway (landing time) calculated by Maestro. |
| `ETA_FF`[^1] | Estimated time of arrival at the **feeder fix**. |
| `STA_FF` | **Scheduled** time of arrival at the feeder fix calculated by Maestro. |

[^1]: The `ETA_FF` is derived from the route estimate provided by vatSys (this is also called a "system estimate")
[^2]: The `ETA` is derived by adding a pre-configured ETI to the `ETA_FF`

## Flight Thread

A Maestro flight is created when:

- a vatSys flight is within 2 hours flight time of the Feeder Fix
- on entry into the Pending List when an FDR is activated for a flight from a Departure Airport

Once a flight is tracked by Maestro, vatSys will provide Maestro with updated position information and estimates every 30 seconds.

At each update, the estimates are re-calculated.
The flights position in the sequence, `STA_FF`, and `STA` may change depending on its [State](#flight-states).

When the flight has reached its STA, it will no longer be processed by Maestro, but it will remain available for a short period of time in case of an overshoot.

<!-- ## Wind

Maestro will consider wind when calculating the TMA trajectory times.

There are two wind inputs:

- The surface wind, updated automatically.
- A 6,000 ft wind provided by the GRIB.

Wind can be manually entered, but the next automatic update will override it. -->

## Estimate Calculation

<!-- TODO: ETA_FF is sourced from vatSys route estimates ("System Estimates") -->
<!-- TODO: ETA is derived from ETA_FF + STAR ETI -->
<!-- TODO: When passed the FF, the ETA will no longer update. Delay figured will not change inside the TMA. -->

## `STA` and `STA_FF` calculation

<!-- TODO: STA will never be earlier than ETA -->
<!-- TODO: STA will be STAn - 1 + Acceptance Rate if there is preceeding traffic -->
<!-- TODO: STA_FF is derived from STA - STAR ETI -->
<!-- TODO: Note that flights may overlap on enroute views when their feeder fix times are close, but they have different STAR ETIs -->

## Airports

<!-- TODO: Managed airport -->
<!-- TODO: Departure airport -->
<!-- TODO: Close airport -->

## Flight States

Maestro uses various "States" for flights that affect how they are processed.

![Diagram of Flight States](../static/img/states.png)

### Unstable

After each update from vatSys, Unstable flights are re-positioned in the sequence based on their estimates, and their `STA_FF` and `STA` times are re-calculated.

All new flights will remain Unstable for at least 5 minutes before they progress into one of the following states.

### Stable

Flights become Stable 25 minutes prior to the `ETA_FF`.

Stable flights will keep their position in the sequence unless a flight appears, disappears, or moves before it.

Stable flights can be displaced by:

- a preceeding flight being moved by controller action
- a new flight entering the sequence with an earlier `ETA_FF`

The required delay figures will change when the flight moves.

:::info
There is no message or other indication to alert controllers of changes to required delays.
Controllers may need to regularly review the Maestro delay figure to recognise changes.
:::

### Super Stable

Flights will become Super Stable at the original `ETA_FF`.

Super Stable flights are "fixed" in position.
All new flights are positioned after Super Stable flights.

Super Stable flights can be moved manually by controller interaction.

### Frozen

Flights become Frozen within 15 minutes of the `STA`.

No changes can be made to Frozen flights.

### Landed

Flights will become Landed at the `STA`.

No changes can be made to Landed flights.

The last 5 landed flights remain in the system in case of an overshoot.

## Pending List

The Pending List contains flights that cannot be automatically inserted into the sequence, and must be inserted manually.

Flights from Departure Airports, or flights not tracking via a Feeder Fix are automatically inserted into the Pending List when their FDR is activated.

Flights from a Departure Airport can be inserted prior to departure to allow the pilot to absorb any required delay on the ground if possible.

<!-- Flights from airports within the TMA must be manually inserted into the sequence. -->

## Delaying Action

Maestro will display the delay required for each flight on the ladder.
The recommended delaying action is represented by various colours:

| Colour | Suggested Actions |
| ------ | ----------------- |
| Green | Speed increase. The flight needs to make up the time shown (i.e. they have slowed down too much).|
| Dark Blue | No delay required. |
| Cyan | Speed reduction. The flight needs to loose the time shown (i.e. they are too early and need to be delayed). |
<!-- | White | TMA pressure and initial enroute delay absorbed. Delay includes the use of extended TMA delay. | -->
| Yellow | Holding recommended. |

<!-- TODO: Add an image of different delay figures -->
