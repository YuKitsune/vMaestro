---
sidebar_position: 2
---

# Flight Lifecycle

This page explains how flights are tracked by vMaestro, how estimates are calculated, and how flights transition through different states.

## Flight Tracking

Flights are tracked by vMaestro when:

- Within 2 hours of flight time to the feeder fix, or
- An FDR is activated for a flight from a departure airport or close airport.

Once tracked, vatSys provides vMaestro with updated position information and estimates every 30 seconds.

At each update, the estimates are recalculated. The flight's position in the sequence, `STA_FF`, and `STA` may be recalculated depending on its current [state](#flight-states).

When a flight reaches its `STA`, it is considered landed and will no longer be processed. Landed flights remain visible briefly in case of an overshoot.

## Estimate Calculation

### Feeder Fix Estimate (ETA_FF)

The `ETA_FF` is sourced from vatSys route estimates (system estimates).

### Landing Estimate (ETA)

vMaestro calculates the landing estimate by adding a predefined trajectory time (`ETI`) to the `ETA_FF`:

```
ETA = ETA_FF + ETI
```

The `ETI` varies based on:
- Feeder fix
- Runway
- Aircraft type
- Approach type

![Diagram of ETA_FF calculation](../../../static/img/eta_ff.png)

:::info
Once a flight passes the feeder fix, the `ATO` (Actual Time Over) is used instead of `ETA_FF`. Since the `ATO` is fixed, the `ETA` and remaining delay values will not change after passing the feeder fix.
:::

### Scheduled Times (STA and STA_FF)

The `STA` (scheduled landing time) is initially based on the `ETA`. vMaestro then checks if the preceding flight would conflict based on the runway's acceptance rate. If the gap is insufficient, the flight is delayed to achieve the required separation.

The `STA` is never earlier than the `ETA` unless manually adjusted by a controller.

Once the `STA` is calculated, the `ETI` is subtracted to determine the `STA_FF`:

```
STA_FF = STA - ETI
```

:::info
Multiple flights tracking via the same feeder fix may share the same `STA_FF` if they have different trajectory times (e.g., different aircraft categories). In this case, labels overlap on feeder fix views but separate on runway views.
:::

## Flight States

vMaestro uses states to control how flights are processed. Flights progress through these states as they approach landing.

![Diagram of Flight States](../../../static/img/states.png)

### Unstable

**When**: All new flights start in this state and remain unstable for at least 5 minutes.

**Behaviour**: After each update from vatSys, unstable flights are repositioned in the sequence based on their `ETA`, and their `STA_FF` and `STA` times are recalculated.

### Stable

**When**: Flights become stable 25 minutes prior to the `ETA_FF`.

**Behaviour**: Stable flights keep their position in the sequence unless displaced by:
- A preceding flight being moved by controller action
- A new flight entering the sequence with an earlier `ETA_FF`

The delay figures will update when the flight's estimates change, but the sequence position remains fixed.

:::warning
There is no alert when required delays change. Controllers should regularly review delay figures to recognise changes.
:::

### Super Stable

**When**: Flights become super stable at the original `ETA_FF`.

**Behaviour**: Super stable flights are fixed in position. All new flights are positioned after them.

Super stable flights can only be moved by explicit controller action.

### Frozen

**When**: Flights become frozen within 15 minutes of the `STA`.

**Behaviour**: No changes can be made to frozen flights. They remain locked in their scheduled position and time.

### Landed

**When**: Flights become landed at the `STA`.

**Behaviour**: No changes can be made. The last 5 landed flights remain visible in case of an overshoot, after which they are automatically removed.

## Pending List

The pending list contains flights that cannot be automatically inserted into the sequence:

- Flights from departure airports
- Flights not tracking via a feeder fix

These flights appear in the pending list when their FDR is activated and must be manually inserted.

Flights from departure airports can be inserted prior to departure, allowing any required delay to be absorbed on the ground.
