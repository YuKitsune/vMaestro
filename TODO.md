# Big Refactor

- [ ] Split Flight into two (FlightData for vatSys details and sequencing parameters, SequencedFlight for scheduling data like initial estimates, STA and STA_FF)
- [ ] Assign runway during insertion (required ctor arguments)
- [ ] Re-assign runway when changing TMA configuration

- [ ] Make Sequence immutable
    - [ ] Create a separate mutable sequence type
    - [ ] Convert mutable sequence to immutable sequence using scheduler
    - [ ] Move TMA configuration into session

- [ ] Review test cases
