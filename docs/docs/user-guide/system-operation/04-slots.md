---
sidebar_position: 4
---

# Slots

Slots reserve runway capacity by preventing flights from being scheduled during a specific time period. They are used for special operations, configuration changes, or other reasons requiring a gap in arrivals.

For how slots affect sequencing, see [Sequencing - Slots](../system-overview/03-sequencing.md#slots).

## Creating a Slot

1. Right-click on the timeline in a runway view
2. Select `Insert Slot`
3. Adjust the start and end times
4. Click `OK`

![Insert Slot Window](../../../static/img/insert_slot_window.png)

The slot appears on the timeline based on the runway filters for that view.

![Slot displayed on the timeline](../../../static/img/timeline_slot.png)

When created:
- Non-frozen flights within the slot are delayed until after the slot ends
- The sequence after the slot is recalculated

## Modifying a Slot

1. Left-click the slot on the timeline
2. Adjust the start and end times in the window
3. Click `OK`

## Removing a Slot

1. Left-click the slot on the timeline
2. Click `Remove`

The sequence is recalculated with the slot removed.
