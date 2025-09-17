# New Scheduler Algorithm

## Step 1: Determine the landing order

Flights are to be ordered based on their landing times.
Landed, Frozen, SuperStable, and Stable flights are to be ordered by their scheduled landing time.
Unstable flights are to be ordered by their estimated landing time.

## Step 2:

The sequence it scanned from the earliest to the latest flight to ensure flights are appropriately spaced behind the one in front of them (same runway, or dependent runway) given the runway mode at their scheduled landing time.

### Unstable Flights

If a flight is unstable, it's sequence position is recomputed and altered if necessary as other aircraft enter / leave the sequence.

### Optimization

Optimisation is a process in which Maestro computes whether a flight can use a non-preferred runway to reduce it's overall delay.

Optimization should be applied to unstable flights to determine which runway they should be assigned to.
Their landing time should be calculated for each active runway in the runway mode, and the one with the earliest time is assigned.

### Stable Flights

Stable flights will keep their position in the sequence unless a new flight appears or disappears before it.

### SuperStable Flights

SuperStable flights are fixed in position. New flights must be positioned after it unless explicitly inserted before it by controller input.

### Frozen and Landed Flights

No changes are made to flights in these states.

## New Flights

When a new flight is introduced into the sequence, it should be inserted based on it's ETA, but not before any SuperStable flights.

## Slots

Slots are periods of time in which no flight, unless Frozen, may land.
Slots are defined for specific runways.
If a flight's landing time is after the slots start time, or before it's end time (whether due to a scheduled delay, or naturally) it should be delayed until the slots end time.
Optimization may result in the flight being moved to another available runway to reduce the overall delay.

Consider using Polymorphism and including the slot in the list.

## Moving Flights

Controllers can move flights manually throughout the sequence.

When a flight is moved, the rest of the sequence should be re-calculated.

A flight cannot be inserted between two frozen flights where the separation is lower than twice the minimum separation allowed.

A flight that is moved to a position more than 1 runway separation from the preceding flight is automatically moved forward towards this position to minimise the delay

## Flight Insertion

Controllers can insert new flights manually into the sequence.

Insertion of a flight can be done in two ways:
1. Relative to another flight (i.e. Before QFA1, or After QFA2)
2. At a specific time
