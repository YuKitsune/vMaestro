---
sidebar_position: 3
---

# Flight Management

This page covers how to insert, modify, move, and remove flights from the sequence.

## Inserting Flights

### Departures

Flights from departure airports must be manually inserted into the sequence.

1. Click the `DEPS` button in the Sequence Display Zone
2. Select the flight from the Pending list
3. Set the expected take-off time
4. Click `OK`

![Insert a Flight window with a pending departure](../../../static/img/insert_departure_window.png)

The `ETA` is calculated from the take-off time plus the flight plan `EET`. The flight becomes Stable immediately. The `*` symbol may appear to indicate the flight is not yet coupled to a radar track.

![Uncoupled departure on ladder](../../../static/img/insert_departure_ladder_uncoupled.png)

:::tip
If vMaestro calculates delay before departure, this can be absorbed on the ground.
:::

Once departed and coupled, the `ETA_FF` and delay values update automatically.

### Overshoot Flights

To resequence a flight that has conducted a missed approach:

1. Right-click on another flight (or the timeline in a runway view)
2. Select `Insert Flight > Before` or `Insert Flight > After`
3. Select the flight from the list
4. Click `OK`

![Insert a Flight window with an overshoot](../../../static/img/insert_overshoot_window.png)

The flight becomes Frozen immediately.

:::info
Flights cannot be inserted between two Frozen flights when the gap is less than twice the acceptance rate.
:::

### Dummy Flights

Dummy flights are placeholders for flights not tracked by vatSys (airwork, practice approaches, etc.).

1. Right-click on another flight (or the timeline in a runway view)
2. Select `Insert Flight > Before` or `Insert Flight > After`
3. Enter the flight details (or leave blank for auto-generated callsign)
4. Click `OK`

The dummy flight becomes Frozen immediately.

### Relative Insertion

- **Before** - The inserted flight takes the position of the target flight, delaying the target. Not available for Frozen flights.
- **After** - The inserted flight is placed behind the target flight without affecting it.

## Modifying Flights

### Change Runway

1. Right-click the flight
2. Select `Change Runway`
3. Select the new runway

![Change Runway context menu](../../../static/img/change_runway.png)

When changed:
- The flight is reinserted based on its `ETA`
- Unstable flights become Stable
- The sequence is recalculated

### Change Approach Type

1. Right-click the flight
2. Select `Change Approach Type`
3. Select the approach type

![Change Approach Type context menu](../../../static/img/change_approach.png)

Only the `ETA` is recalculated. The sequence position remains unchanged.

### Change ETA_FF

If the vatSys estimate is inaccurate, it can be manually overridden.

1. Right-click the flight
2. Select `Change ETA_FF`
3. Set the new time
4. Click `OK`

![Change ETA_FF window](../../../static/img/change_eta_ff.png)

When changed:
- The `ETA` is recalculated
- The flight is reinserted based on the new `ETA`
- Unstable flights become Stable
- The sequence is recalculated
- Future vatSys updates to `ETA_FF` are ignored

Use [Recompute](#recompute) to cancel the manual override.

### View Flight Information

1. Right-click the flight
2. Click `Information`

![Information Window](../../../static/img/information_window.png)

Up to 4 Information windows can be displayed simultaneously.

### Manual Delay (Increase Priority)

To ensure a flight receives no more than a specified delay:

1. Right-click the flight
2. Select `Manual Delay`
3. Select the maximum delay

![Manual Delay dropdown](../../../static/img/manual_delay.png)

When set:
- The flight is positioned to not exceed the specified delay
- The sequence is recalculated
- New flights will not cause delay to exceed the limit

Use [Recompute](#recompute) to cancel the manual delay.

:::info
A delay of `00` still allows delay up to the runway's acceptance rate.
:::

## Moving Flights

Flights can be moved within the sequence from a runway view.

### Click to Move

1. Left-click a flight label to select it (a frame appears)
2. Left-click the destination position on the timeline

![Flight with selection frame](../../../static/img/flight_selected.png)

The flight moves immediately. To deselect without moving, left-click the flight again or right-click anywhere.

When moved:
- The flight is repositioned in the sequence
- The `STA` is recalculated based on the new position
- If clicked on a different timeline, the runway is changed
- Unstable flights become Stable
- The sequence is recalculated

### Click to Swap

If you click another flight while one is selected, the two flights swap their `STA` and runway assignments.

### Drag to Move

1. Left-click and hold a flight label
2. Drag to the new position
3. Release the mouse button
4. Click `Confirm` in the confirmation window

:::note
Flights are moved by position in the sequence, not by setting a specific landing time. The `STA` is automatically calculated based on the new position and acceptance rates.
:::

:::info
Flights cannot be moved between two Frozen flights when the gap is less than twice the acceptance rate.
:::

### Recompute

Recomputing recalculates a flight's parameters as if it were new.

1. Right-click the flight
2. Select `Recompute`

This will:
- Remove the flight from the sequence
- Cancel any manual delay and manual `ETA_FF`
- Recalculate the feeder fix, `ETA_FF`, and `ETA`
- Reinsert the flight based on the new estimates
- Recalculate the sequence

## Removing Flights

### Desequence

Desequencing temporarily removes a flight from the sequence (for holding, technical issues, etc.). The flight can be quickly resequenced later.

1. Right-click the flight
2. Select `Desequence`

The flight moves to the Desequenced list and the sequence is recalculated.

### Make Pending

For departures that have not yet taken off:

1. Right-click the flight
2. Select `Make Pending`

The flight returns to the Pending list and can be reinserted later.

### Remove

Removing permanently deletes a flight from the sequence (for diversions, etc.).

1. Right-click the flight
2. Select `Remove`
3. Click `Confirm`

The flight moves to the Pending list and the sequence is recalculated.

:::tip
Flights can also be removed from the Desequenced list by clicking `DESQ`, selecting the flight, and clicking `REMOVE`.
:::

## Resequencing Desequenced Flights

1. Click the `DESQ` button
2. Select the flight
3. Click `RESEQUENCE`

![Desequenced Window](../../../static/img/desequenced_window.png)

The flight is placed in the sequence based on its last `ETA_FF`, becomes Stable immediately, and the sequence is recalculated.
