# Big Refactor

- [ ] Introduce SessionDraft and SequenceDraft
    - [ ] Mutable Session + Sequence
    - [ ] Pass to Scheduler to create a Session
    - [ ] Replace session when completed

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
