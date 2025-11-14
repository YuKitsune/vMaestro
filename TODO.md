# Big Refactor

We're going to introduce a new pattern. From now on, the Session is the aggregate.
Session will contain:
- a list of Pending flights (awaiting to be sequenced)
- a list of De-sequenced flights (temporarily removed from the sequence)
- a list of sequenced flights (actively being sequenced, all flights in this list are **gauranteed** to be separated)
- the current runway mode
- the next ruwnay mode and it's start time(s) (if any)

Session and all of it's domain entities will be **immutable**.
To mutate the session, the caller must convert it to a "draft" (name TBD, but this is like a builder).
The "draft" exists to capture intent, allow mutations, and provides no validation whatsoever.

Once the modifications are completed, the "draft" should be passed to the Scheduler.
The Scheduler will perform the actions required to ensure all sequenced flights are appropriately separated from slots, runway changes, and eachother. It will then produce a type which can be "adopted" (name TBD) by the session.

This is to allow us to:
- Mutate the sequence without worrying about invalid data being persisted
- Reliably position flights without worrying about non-flight items in the sequence (i.e. slots and runway changes)

## Checklist

- [ ] Consider naming
    - Draft? Builder? Snapshot?
    - Adopt? Commit? Restore?

- [ ] Introduce SessionDraft and SequenceDraft
    - [ ] Mutable Session + Sequence
    - [ ] Pass to Scheduler to create a Session
    - [ ] Replace session when completed

- [ ] Move runway modes to the session
    - [ ] Consider encapsulating current and next runway mode in a new type

- [ ] Make Runway a ctor parameter for Flights
    - [ ] Assign runway on insertion
    - [ ] Store the runway and the acceptance rate in the flight

- [ ] Separate Flight data from Sequence data
    - [ ] Current estimates and sequence parameters go in the main Flight class
    - [ ] Initial estimates and scheduled times go in the Sequence data class
    - [ ] Re-assign runways when changing TMA configuration

- [ ] Review test cases

# Questions

If a runway change is scheduled for some time in the future, and a flight is delayed past the `LastLandingTimeInPreviousMode`, what happens?
Does the flight get moved to a runway in the new mode, or will it remain on its assigned runway?
Does the flight get scheduled between `LastLandingTimeInPreviousMode` and `FirstLandingTimeInNewMode`, or is it delayed until `FirstLandingTimeInNewMode`?
