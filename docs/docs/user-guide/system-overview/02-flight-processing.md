---
sidebar_position: 2
---

# How Flights are Processed

This page explains how vMaestro tracks and processes flights from creation through to landing.

## Flight Creation

When a flight plan is created in vatSys, the flight becomes visible to vMaestro. At this stage, the flight is not yet active and is not being processed.

## Flight Activation

When a flight plan is activated in vatSys, the flight becomes active in vMaestro. Enroute flights within 2 hours of their feeder fix are tracked automatically and added to the sequence.

## Pending List

Flights from departure airports appear in the pending list when their flight plan is activated. These flights must be manually inserted into the sequence.

Pending flights can be inserted prior to departure, allowing any required delay to be absorbed on the ground rather than in the air.

Flights not tracking via a feeder fix also appear in the pending list and must be manually inserted.

## The Processing Cycle

vMaestro processes all active flights every 30 seconds.
Each cycle performs the following steps for each flight.

![Flight Processing Cycle Flowchart](../../../static/img/process-flowchart.png)

### 1. Estimate Calculation

The `ETA_FF` is sourced from vatSys route estimates. The landing estimate (`ETA`) is then calculated by adding the time-to-go (`TTG`) from the allocated trajectory.

For flights not tracking via a feeder fix, an average `TTG` will be calculated, and the `ETA_FF` is derived by subtracting the average `TTG` from the last estimate in the flight plan route.

![Diagram of ETA_FF calculation](../../../static/img/eta_ff.png)

:::info
Once a flight passes the feeder fix, the `ATO` (Actual Time Over) is used instead of `ETA_FF`. Since the `ATO` is fixed, the `ETA` and remaining delay will not change after passing the feeder fix.
:::

### 2. Sequencing

Flights are ordered by their `ETA` following a first-come, first-served approach.

### 3. Scheduling

During scheduling, vMaestro assigns each flight a runway and calculates its scheduled landing time (`STA`). The runway's acceptance rate enforces minimum separation, and conflicts are resolved by delaying flights as necessary.

Runways are assigned based on the active runway mode. If the runway mode specifies preferred feeder fixes, flights via those fixes are assigned to the corresponding runway. Otherwise, vMaestro calculates the `STA` for each available runway and assigns the one resulting in the earliest landing time. Flights not tracking via a feeder fix are assigned to the first runway in the runway mode.

The `STA` is assigned based on the flight's sequence position and is never earlier than the `ETA` unless manually adjusted. The `STA_FF` is then derived by subtracting the trajectory time.

Flights allocated a maximum delay are prioritised. If their calculated delay exceeds the maximum, they are moved forward by swapping with preceding flights until their total delay is within the allocated maximum.

Flights will not be scheduled to land during a Slot, or during a Runway Mode transition period.

:::info
Multiple flights tracking via the same feeder fix may share the same `STA_FF` if they have different trajectory times (e.g., different aircraft categories). Labels may overlap on feeder fix views but will separate on runway views.
:::

### 4. Delay Calculation

The required delay is the difference between the scheduled landing time and the initial estimate. The remaining delay is also calculated, which decreases as the flight absorbs delay through speed reduction or vectoring.

### 5. State Transition

The flight's state is updated based on time to `ETA_FF` and `STA`. As flights progress through states, processing becomes increasingly restricted. See [Flight States](#flight-states) below.

## Flight States

vMaestro uses states to control how flights are processed. Flights progress through these states as they approach landing.

![Diagram of Flight States](../../../static/img/states.png)

### Unstable

All new flights start in this state. On each update, the full processing cycle runs: estimates are recalculated, the flight is repositioned in the sequence, and scheduling assigns a runway and `STA`. The runway and approach type may change if an earlier `STA` is available on an alternative runway.

### Stable

Flights become Stable as they approach the `ETA_FF`. From this point, only estimates (`ETA_FF`, `ETA`) and remaining delay are recalculated on each update. Sequencing and scheduling no longer run automatically.

Stable flights keep their position unless displaced by controller action on a preceding flight, or a new flight entering with an earlier `ETA`.

:::warning
There is no alert when required delays change. Controllers should regularly review delay figures to recognise changes.
:::

### SuperStable

Flights become SuperStable at their original `ETA_FF`. Processing is the same as Stable, but the flight is fixed in position. All new flights are positioned after it. Displacement only occurs through controller action on this flight or a preceding flight.

### Frozen

Flights become Frozen as they approach the `STA`. Processing is the same as Stable, but the flight cannot be displaced at all, even by controller actions.

### Landed

Flights become Landed at the `STA`. Processing stops entirely. The last 5 landed flights remain visible in case of an overshoot, after which they are removed. Flights are also removed when deleted from vatSys.
