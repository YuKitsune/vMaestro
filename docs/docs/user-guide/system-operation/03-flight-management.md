---
sidebar_position: 3
---

# Flight Management

This page covers how to manage flights in the sequence, including inserting, modifying, moving, and removing flights.

## Inserting Flights

### Departures

Flights from departure airports must be manually inserted into the sequence.

1. Click the `DEPS` button in the Sequence Display Zone
2. Select the flight from the Pending list
3. Set the expected take-off time
4. Click `OK`

![Insert a Flight window with a pending departure](../../../static/img/insert_departure_window.png)

The landing estimate is calculated from the take-off time plus a predefined flight time. The coupling status indicator may appear until the flight departs and couples to a radar track.

![Uncoupled departure on ladder](../../../static/img/insert_departure_ladder_uncoupled.png)

:::tip
If Maestro calculates delay before departure, this can be absorbed on the ground.
:::

### Overshoot Flights

To resequence a flight that has conducted a missed approach:

1. Right-click on another flight (or the ladder in a runway view)
2. Select `Insert Flight`, then `Before` or `After`
3. Select the overshoot flight from the list
4. Click `OK`

![Insert a Flight window with an overshoot](../../../static/img/insert_overshoot_window.png)

**Before** inserts the flight ahead of the target, delaying the target flight. **After** inserts behind the target without affecting it.

:::info
Flights cannot be inserted between two Frozen flights when the gap is less than twice the acceptance rate.
:::

### Dummy Flights

Dummy flights are placeholders for flights not tracked by vatSys (airwork, practice approaches, etc.).

1. Right-click on another flight (or the ladder in a runway view)
2. Select `Insert Flight`, then `Before` or `After`
3. Enter the flight details (or leave blank for an auto-generated callsign)
4. Click `OK`

## Moving Flights

Flights can be moved within the sequence from a runway view.

1. Left-click a flight label to select it (a frame appears)
2. Left-click the destination position on the ladder

Alternatively, drag the flight label up or down the ladder.

![Flight with selection frame](../../../static/img/flight_selected.png)

To deselect without moving, left-click the flight again or right-click anywhere.

To swap two flights, select one flight then click another. The two flights exchange positions.

:::info
Flights cannot be moved between two Frozen flights when the gap is less than twice the acceptance rate.
:::

## Modifying Flights

### Change Runway

1. Right-click the flight
2. Select `Change Runway`
3. Select the new runway

![Change Runway context menu](../../../static/img/change_runway.png)

The flight is reinserted into the sequence based on its estimate for the new runway.

### Change Approach Type

1. Right-click the flight
2. Select `Change Approach Type`
3. Select the approach type

![Change Approach Type context menu](../../../static/img/change_approach.png)

The landing estimate is recalculated. The sequence position remains unchanged.

### Change ETA_FF

If the vatSys estimate is inaccurate, it can be manually overridden.

1. Right-click the flight
2. Select `Change ETA_FF`
3. Set the new time
4. Click `OK`

![Change ETA_FF window](../../../static/img/change_eta_ff.png)

The flight is reinserted based on the new estimate. Future updates from vatSys are ignored until the override is cleared.

### Manual Delay

To limit the maximum delay a flight can receive:

1. Right-click the flight
2. Select `Manual Delay`
3. Select the maximum delay

![Manual Delay dropdown](../../../static/img/manual_delay.png)

The flight is repositioned to not exceed the specified delay. New flights entering the sequence will not push this flight beyond its delay limit.

:::info
A delay of `00` still allows delay up to the runway's acceptance rate.
:::

### Recompute

Recomputing resets a flight to its original state, clearing any manual overrides.

1. Right-click the flight
2. Select `Recompute`

This clears any manual delay or ETA_FF override, recalculates the estimates, and reinserts the flight as if it were new.

## Removing Flights

### Desequence

Desequencing temporarily removes a flight from the sequence for holding, technical issues, or other reasons. The flight can be quickly resequenced later.

1. Right-click the flight
2. Select `Desequence`

The flight moves to the Desequenced list.

To resequence:

1. Click the `DESQ` button
2. Select the flight
3. Click `RESEQUENCE`

![Desequenced Window](../../../static/img/desequenced_window.png)

### Make Pending

For departures that have not yet taken off:

1. Right-click the flight
2. Select `Make Pending`

The flight returns to the Pending list and can be reinserted later.

### Remove

Removing deletes a flight from the sequence (for diversions, cancellations, etc.).

1. Right-click the flight
2. Select `Remove`
3. Click `Confirm`

The flight moves to the Pending list and can be reinserted if needed.

## Viewing Flight Information

To view detailed information about a flight:

1. Right-click the flight
2. Click `Information`

![Information Window](../../../static/img/information_window.png)

Up to 4 Information windows can be displayed simultaneously.
