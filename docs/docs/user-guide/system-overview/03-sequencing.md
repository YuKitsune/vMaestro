---
sidebar_position: 3
---

# Sequencing

This page explains how vMaestro assigns runways to flights and schedules landing times.

## Runway Assignment

vMaestro automatically assigns runways to arrivals based on the runway mode active at their `ETA`.

### Feeder Fix Based Assignment

If the runway mode specifies feeder fix preferences, flights via those feeder fixes are assigned to the corresponding runway.

### Automatic Assignment

If the runway mode does not specify feeder fix requirements, vMaestro calculates the `STA` for each available runway and assigns the one that results in the earliest landing time.

### Fallback Assignment

If a flight is not tracking via any feeder fix, the first runway defined in the runway mode is assigned.

### State Restrictions

vMaestro only assigns runways to **Unstable** flights. Once a flight becomes Stable, vMaestro will not change the runway assignment automatically. Controllers can still manually reassign runways.

## Scheduling Algorithm

When the sequence is calculated, vMaestro:

1. Orders flights by their `ETA`
2. Applies the runway's acceptance rate to ensure minimum separation
3. Delays flights as necessary to resolve conflicts
4. Respects flight states (frozen flights are not moved, stable flights are only displaced when forced)

### Conflict Resolution

When two flights would conflict (land within the acceptance rate window), vMaestro delays the later flight. The delay is applied by adjusting the `STA` forward in time.

### Priority Flights

Flights with manual delay limits are prioritised during scheduling. If their calculated delay would exceed the limit, they are moved forward by swapping with preceding flights until the delay is acceptable.

A flight will not move forward if:
- The preceding flight is Frozen or Landed
- Moving would place it inside a slot
- Moving would place it inside a runway mode change period
- The preceding flight also has a delay limit and an earlier landing estimate

## Slots

Slots are periods where no flights can be scheduled to land on a specific runway. They reserve runway capacity for special operations or configuration changes.

**Behaviour**:
- vMaestro will not schedule flights to land within a slot
- Flights may be scheduled at the start or end of a slot
- Only Frozen flights are permitted within a slot
- Non-frozen flights within a slot are automatically delayed until after the slot ends

See [Slots](../system-operation/04-slots.md) for how to manage slots.
