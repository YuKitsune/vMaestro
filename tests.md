## Flight Updated

- [X] When a flight is updated, and no instances are active for that flights destination, it is not tracked
- [X] When a flight is updated, and it is out of range, it is not tracked
- [X] When a flight is updated, and it's within range, it is tracked
- [X] When a flight is updated, and it's not tracking via a known feeder fix, it is added to the pending list
- [X] When a flight is updated, and it's on the ground at a departure airport, it is added to the pending list
- [X] When a flight is updated, and it's not currently tracked, a runway is assigned based on feeder fix preferences
- [X] When a flight is updated, and it's not currently tracked, and it's landing estimate is earlier than a stable flight, the flight is inserted before the stable one
- [X] When a flight is updated, and it's not currently tracked, and it's landing estimate is earlier than a superstable  flight, the flight is inserted after the sueprstable one
- [X] When a flight is updated, its estimates are updated
- [X] When a flight is updated, and a manual ETA_FF has been set, estimates are not updated
- [X] When a flight is updated, and it's unstable, it's re-positioned in the sequence based on it's estimate
- [X] When a flight is updated, and it's unstable, and its estimate is ahead of a stable flight, it does not overtake the stable flight
- [X] When a flight is updated, and it's unstable, it's sequence data is re-calculated
- [X] When a flight is updated, and it's last update was recent, the update is ignored
- [X] When a flight is updated, and it's within range, it's state is updated
- [X] When a flight is updated, and we are in slave mode, the update is relayed to the master

## Change feeder fix estimate

- [X] When changing feeder fix estimate, the feeder fix estimate is set, and manual flag is set
- [X] When changing feeder fix estimate, the landing estimate is re-calculated
- [X] When changing feeder fix estimate, the flight is repositioned based on the new landing estimate
- [X] When changing feeder fix estimate, and the flight is unstable, it is made stable
- [X] When changing feeder fix estimate, and it's not unstable, it's state is retained
- [X] When changing feeder fix estimate, and we are in slave mode, the request is relayed to the master

## Change Runway Mode

- [X] When changing runway mode, and the start time is now or earlier, all non-frozen flights from now onwards are re-assigned to the new runway mode, and the sequence is re-calculated
- [X] When changing runway mode, the Next runway mode and start times are set
- [X] When changing runway mode, non-frozen flights are not scheduled to land between the start and end times
- [X] When changing runway mode, flights scheduled to land after the start time are assigned to the new runway mode
- [X] When changing runway mode, the sequence is re-calculated from the point where the current runway mode ends
- [X] When changing runway mode, and we are in slave mode, the request is relayed to the master

## Change Runway

- [X] When changing runway, the new runway is assigned
- [X] When changing runway, the flight is re-positioned based on it's estimate
- [X] When changing runway, the sequence from where the flight was moved to is re-calculated
- [X] When changing runway, and the flight was unstable, it becomes stable
- [X] When changing runway, and the flight is not unstable, it's state is retained
- [X] When changing runway, and we are in slave mode, the request is relayed to the master

## Create Slot

- [X] When creating a slot, flights landing within the slot are delayed until the end of the slot
- [X] When creating a slot, frozen flights are not re-calculated
- [X] When creating a slot, and we are in slave mode, the request is relayed to the master

## Modify Slot

- [X] When modifying a slot, flights landing after the original start time or the new start time are re-calculated
- [X] when modifying a slot, flights within the slot are delayed until after the slot
- [X] When modifying a slot, frozen flights are not re-calculated
- [X] When modifying a slot, and we are in slave mode, the request is relayed to the master

## Delete Slot

- [X] When deleting a slot, the sequence is re-calculated from the start time of the slot
- [X] When deleting a slot, and we are in slave mode, the request is relayed to the master

## Desequence

- [X] When desequencing a flight, it is removed from the sequence and added to the de-sequenced list
- [X] When desequencing a flight, the sequence is re-calculated from where the flight was de-sequenced
- [X] When desequencing a flight, and we are in slave mode, the request is relayed to the master

## Insert Departure

- [X] When inserting a departure, landing estimate is derived from take-off time and EET
- [X] When inserting a departure, it is inserted based on it's landing estimate
- [X] When inserting a departure, the runway is assigned based on the feeder-fix preference
- [X] When inserting a departure, with an estimate earlier than a stable flight, the stable flight is delayed
- [X] When inserting a departure, with an estimate earlier than a suberstable flight, the inserted flight is delayed until after the superstable one
- [X] When inserting a departure, ahead of another flight, the departure is sequenced in front of the other flight
- [X] When inserting a departure, behind another flight, the departure is sequenced in behind of the other flight
- [X] When inserting a departure, the rest of the sequence is re-calculated
- [X] When inserting a departure, and we are in slave mode, the request is relayed to the master

<!-- Suggest re-writing all of the flight insertion tests -->

## Insert Flight

- [X] When inserting a flight, at a specific time, the landing estimate is set to the provided time
- [X] When inserting a flight, at a specific time, the runway is set based on the runway mode at the provided time
- [X] When inserting a flight, at a specific time, the flight is inserted based on it's landing estimate

- [X] When inserting a flight, behind another one, the runway is set to the reference flights runway
- [X] When inserting a flight, behind another one, the landing time is set to the reference flights landing time plus the runways acceptance rate
- [X] When inserting a flight, behind another one, the flight is sequenced behind the reference flight

- [X] When inserting a flight, ahead of another one, the runway is set to the reference flights runway
- [X] When inserting a flight, ahead of another one, the landing time is set to the reference flights landing time
- [X] When inserting a flight, ahead of another one, the flight is sequenced ahead of the reference flight

- [X] When inserting a flight, the provided callsign is used
- [X] When inserting a flight, and no callsign is provided, a dummy callsign is used
- [X] When inserting a flight, the provided aircraft type is used
- [X] When inserting a flight, and no aircraft type is provided, defaults to medium jet

- [X] When inserting a flight, it is frozen

- [X] When inserting a flight, and we are in slave mode, the request is relayed to the master

## Insert Overshoot

- [X] When inserting an overshoot, at a specific time, the landing estiamte is set to the provided time
- [X] When inserting an overshoot, at a specific time, the runway is set based on the runway mode at the provided time
- [X] when inserting an overshoot, at a specific time, the flight is moved based on it's landing estimate

- [X] When inserting an overshoot, behind another one, the runway is set to the reference flights runway
- [X] When inserting an overshoot, behind another one, the landing time is set to the reference flights landing time plus the runways acceptance rate
- [X] When inserting an overshoot, behind another one, the flight is moved behind the reference flight

- [X] When inserting an overshoot, ahead of another one, the runway is set to the reference flights runway
- [X] When inserting an overshoot, ahead of another one, the landing time is set to the reference flights landing time
- [X] When inserting an overshoot, ahead of another one, the flight is moved ahead of the reference flight

- [X] When inserting an overshoot, it is frozen

- [X] When inserting an overshoot, and we are in slave mode, the request is relayed to the master

## Insert Pending

- [X] When inserting a pending flight, at a specific time, the landing estiamte is set to the provided time
- [X] When inserting a pending flight, at a specific time, the runway is set based on the runway mode at the provided time
- [X] when inserting a pending flight, at a specific time, the flight is inserted based on it's landing estimate
- [X] when inserting a pending flight, at a specific time, the flight is removed from the pending list

- [X] When inserting a pending flight, behind another one, the runway is set to the reference flights runway
- [X] When inserting a pending flight, behind another one, the landing time is set to the reference flights landing time plus the runways acceptance rate
- [X] When inserting a pending flight, behind another one, the flight is inserted behind the reference flight
- [X] When inserting a pending flight, behind another one, the flight is removed from the pending list

- [X] When inserting a pending flight, ahead of another one, the runway is set to the reference flights runway
- [X] When inserting a pending flight, ahead of another one, the landing time is set to the reference flights landing time
- [X] When inserting a pending flight, ahead of another one, the flight is inserted ahead of the reference flight
- [X] When inserting a pending flight, ahead another one, the flight is removed from the pending list

- [X] When inserting a pending flight, it is stable

- [X] When inserting a pending flight, and we are in slave mode, the request is relayed to the master

## Make Stable

- [X] When made stable, unstable flights become stable
- [X] When made stable, other flights are ignored

- [X] When made stable, and we are in slave mode, the request is relayed to the master

## Manual Delay

- [X] When manual delay is assigned, and the current delay does not exceed the maximum delay, the sequence is unaffected
- [X] When manual delay is assigned, and the current delay exceeds the maximum delay, the flight is moved forward until the maximum delay is no longer exceeded
- [X] When manual delay is assigned, and all preceeding flights are frozen, the sequence is unaffected
- [X] When manual delay is assigned, and we are in slave mode, the request is relayed to the master

## Move Flight

- [X] When moving a flight, the flight is moved based on the target time
- [X] When moving a flight, the sequence is re-calculated
- [X] When moving a flight, it becomes stable
- [X] When moving a flight, the runway can be changed
- [X] When moving a flight, frozen flights are not moved
- [X] When moving a flight, stable/superstable/frozen/landed flights retain their state
- [X] When moving a flight, between two frozen flights with insufficient space, exception is thrown
- [X] When moving a flight, and we are in slave mode, the request is relayed to the master

## Recompute

- [X] When recomputing a flight, manual ETA_FF is removed
- [X] When recomputing a flight, manual delay is removed
- [X] When recomputing a flight, runway is re-assigned
- [X] When recomputing a flight, it is repositioned based on it's ETA_FF
- [X] When recomputing a flight, the state is updated based based on time
- [X] When recomputing a flight, and we are in slave mode, the request is relayed to the master

## Remove

- [X] When removing a flight, it is removed from the sequence and added to the pending list
- [X] When removing a flight, the sequence is re-calculated from where it was removed
- [X] When removing a flight, and we are in slave mode, the request is relayed to the master

## Resume

- [X] When resuming a flight, it is removed from the de-sequenced list and inserted into the sequence
- [X] When resuming a flight, it is inserted based on it's ETA
- [X] When resuming a flight, and it's ETA places it between two frozen flights with insufficient space, it is delayed until after the frozen flights
- [X] When resuming a flight, and we are in slave mode, the request is relayed to the master

## Swap Flights

- [X] When swapping flights, landing times, runways, and positions are swapped
- [X] When swapping flights, the sequence is not recomputed
- [X] When swapping flights, both flights become stable
- [X] When swapping flights, and we are in slave mode, the request is relayed to the master

## Sequence

- [ ] When a flight is inserted, and it's estimate is ahead of the flight in front of it, the inserted flight is delayed
- [ ] When a flight is inserted, and it's estimate is not in conflict with the next flight, the inserted flight is not delayed
- [ ] When a flight is inserted, and flights behind it have earlier landing times, the flights behind it are delayed
- [ ] When a flight is inserted, between two frozen flights, without enough space, exception is thrown

- [ ] When a flight is moved, the position in sequence is updated
- [ ] When a flight is moved forward, and the estimate is earlier than the preceeding flight, the moved flight receives no delay
- [ ] When a flight is moved forward, and the estimate is later than the preceeding flight, the moved flight receives no delay, and the preceeding flight is delayed behind the moved flight

- [ ] When a flight is moved backwards, and the estimate is earlier than the next flight, the moved flight receives no delay, and the next flight is delayed behind the moved flight
- [ ] When a flight is moved backwards, and the estiate is later than the next flight, neither flight is delayed

- [ ] When a flight is moved, between two frozen flights, without enough space, exception is thrown

- [ ] When flights are swapped, their landing times are swapped
- [ ] When flights are swapped, their ruwnays are swapped
- [ ] When flights are swapped, their ETA_FF is re-calculated
- [ ] When flights are swapped, the sequence is not re-calculated (apply an artificial delay to an unstable flight and assert the delay remains after swapping)

- [ ] When scheduling, unstable flights are re-scheduled based on their estimates
- [ ] When scheduling, stable flights are not re-scheduled
- [ ] When scheduling, stable flights are re-scheduled when flag is set
- [ ] When scheduling, and two flights are too close, the next flight in the sequence is delayed
- [ ] When scheduling, and two flights on different runways are too close, no separation is applied
- [ ] When scheduling, and two flights on dependent runways are too close, they are separated by the dependency rate
- [ ] When scheduling, and two flights are too close, but the next one cannot be delayed, the preceeding flight is moved behind the "fixed" flight
- [ ] When scheduling, and the delay exceeds the maximum delay, the flight is moved forward
- [ ] When scheduling, and the delay exceeds the maximum delay, but is less than the acceptance rate, the flight is not moved
- [ ] When scheduling, and the delay exceeds the maximum delay, but the flight cannot move forward, the flight moves as far farward as it possibly can
- [ ] When scheduling, and the landing time is within a slot, the flight is delayed until after the slot ends
- [ ] When scheduling, and the landing time is within a runway change, the flight is delayed until after the runway change
- [ ] When scheduling, and a delay is required, ReduceSpeed flow control is applied
- [ ] When scheduling, and no delay is required, ProfileSpeed flow control is applied
- [ ] When scheduling, and flights have moved, the order of the sequence is preserved between runways
- [ ] When scheduling, initial estimates are updated

- ChangeRunwayMode covered by other tests
- Slots covered by other tests
