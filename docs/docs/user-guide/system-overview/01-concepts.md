---
sidebar_position: 1
---

# Concepts

## What is MAESTRO?

MAESTRO is a traffic flow management system that provides sequencing information to controllers to optimise the flow of arrivals into an airport. It sequences inbound arrivals, delaying them when required to achieve a desired landing rate.

MAESTRO is a sequencing aid only. It does not provide separation advice. Controllers remain responsible for ensuring separation is maintained.

## Abbreviations

| Term | Meaning |
| ---- | ------- |
| `ETA_FF` | Estimated time at the feeder fix (from vatSys) |
| `ETA` | Estimated landing time (calculated by vMaestro) |
| `STA_FF` | Scheduled time at the feeder fix (calculated by vMaestro) |
| `STA` | Scheduled landing time (calculated by vMaestro) |
| `TTG` | Time-to-Go from feeder fix to runway threshold |

## Managed Airport

The managed airport is the airport vMaestro is sequencing arrivals for. All flights in the sequence are arriving at this airport.

## Departure Airports

Departure airports are airports within close proximity to the managed airport, typically within 30–45 minutes flight time. Flights originating from these airports appear in the [Pending List](./02-flight-processing.md#pending-list) when activated.

## Feeder Fixes

A feeder fix is a point along the TMA boundary where flights are transferred from Enroute to the TMA. Feeder fixes generally correspond to a particular STAR, though they may not be the STAR entry point.

vMaestro uses the feeder fix to:

- Determine which runway to assign based on runway mode preferences
- Calculate the landing estimate using predefined trajectories
- Position flights on feeder views

:::info
A flight may have an `ETA_FF` without tracking via a specific feeder fix. In this case, the time represents the expected transfer time to the TMA.
:::

## Trajectories

A trajectory is a set of times for a flight of a given aircraft type flying from a particular feeder fix, via a particular transition fix and approach type, to a specific runway threshold. Each trajectory contains:

- **Time-to-Go (TTG)**: The average flight time from the feeder fix to the runway threshold
- **Pressure**: Additional minutes that can be absorbed within the TMA (e.g., using an extended downwind leg)
- **Maximum Pressure**: The maximum delay that can be absorbed within the TMA

:::info
Pressure and Maximum Pressure are not yet simulated.
:::

![TMA Trajectory Example](../../../static/img/tma_trajectory.png)

## Runway Modes

A runway mode defines which runways are active for arrivals. Each runway mode specifies:

- Which runways are in use
- Landing rates for each runway
- Feeder fix preferences for runway assignment

vMaestro uses the active runway mode to determine how flights are assigned to runways and scheduled.
See [TMA Configuration](../system-operation/02-tma-configuration.md) for how to change runway modes during operation.

## Slots

Slots reserve runway capacity by preventing flights from being scheduled during a specific time period.
They are used for special operations, configuration changes, or other reasons requiring a gap in arrivals.

See [Slots](../system-operation/04-slots.md) for how to manage slots during operation, and [How Flights are Processed - Slots](./02-flight-processing.md#slots) for how slots affect scheduling.
