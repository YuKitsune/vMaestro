# MAESTRO Plugin Roadmap

## V1 Release

### Revise all test cases

- [ ] Delete redundant or inaccurate test cases
- [ ] Compare remaining test cases with reference material

### Documentation

- [X] Write documentation for ATC usage
- [X] Write documentation for configuration and server setup
- [-] Document limitations and differences compared to the real system

### Refactoring

- [X] Refactor flight insertion handlers (Consider combining or separating them)
- [X] Remove (or trim down) SequenceMessage and introduce smaller DTOs or notifications (consider CRDTs) to reduce the size of the sequence when serialised
- [X] Improve separation between domain models, DTOs, and view models
- [X] Explore calculating TTG at runtime based on STAR track and upper winds
- [X] Make delaying action times configurable (lightweight enroute trajectory config?)
- [ ] Decompose Plugin.cs into multiple separate services (WindCheckService, vatSysEventAdapter, DpiAwarenessHelper, etc.)

### Server Deployment

- [X] Package server binary
- [X] Build and publish server container
- [X] Separate docs and server

## Future Enhancements

### Achieved Rates

- [X] 10,000 ft and 6,000 ft winds (with configuration view)
- [X] Achieved rates
- [X] Units selector (NM, aircraft/hr, seconds)
- [X] UTC time

### Testing Clean-up

- [ ] Consider moving some of the sequence logic into the individual handlers so they can be tested more easily.
- [ ] Use a Mock sequence, that doesn't do any scheduling
- [ ] Test scheduling separately
- [ ] Clean up Flight builder so that you can't build an invalid flight (make ETA and ETA_FF mutually exclusive? Or calculate a TTG based on ETA - ETA_FF if no trajectory is set?)
- [ ] Clean up the handler tests to remove all the unnecessary setup (i.e. trajectories)

### Internal Documentation

- [ ] Document instances, sessions, interactions between vatSys and Maestro.Core, etc.
- [ ] ADR
- [ ] Practices (i.e. don't assert on logs, etc.)

### Algorithm Overhaul

Revisit the sequencing and scheduling algorithms.

- [X] ETA_FF = ETA - average TTG when no FF is set.
- [X] Calculate delaying actions in the scheduling algorithm.
- [ ] Model enroute trajectories.
- [ ] Model TMA pressure
- [ ] Model runway allocation strategies (geo, preferred, mixed)
- [X] Model runway dependencies (dependent, semi-dependent, and independent)
- [ ] Apply GRIB winds

### Refactor Sequence Aggregate

Separate the `Sequence` aggregate into a read-only aggregate (source of truth), and a mutable builder (dirty state).
Use the builder to propose changes to the sequence, and combine it with a new `Scheduler` to ensure flights are scheduled and separated appropriately.

Pseudocode:
```cs
var sequence = repository.GetSequenceFor(airportIdentifier);

var sequenceBuilder = SequenceBuilder.From(sequence);

sequenceBuilder.Insert(10, newFlight);

var newSequence = sequenceBuilder.Build(scheduler);

sequence.Apply(newSequence);

repository.UpdateSequence(sequence);
```

- [ ] Separate `Flight` into multiple parts, such that FlightPlan data from vatSys can be updated independently of the sequencing and scheduling data
    - Consider separating the flight into a mutable and immutable types, or;
    - Split flight into `FlightPlan` + `ScheduleParameters` + `ScheduleResult`.
        - FlightPlan contains data from vatSys
        - `ScheduleParameters` are used to schedule the flight and build the sequence
        - `ScheduleResult` contains the STA etc.
        - `FlightPlan` and `ScheduleParameters` remain mutable
        - `ScheduleResult` controlled by the `Sequence` aggregate

- [ ] Make the `Sequence` aggregate read-only
    - [ ] Create a separate SequenceBuilder type for mutations (accepts an existing sequence as a starting point, requires a scheduler to build a concrete Sequence)

- [ ] Add tests
    - [ ] SequenceBuilder: Building from existing sequences, ordering and prioritising flights
    - [ ] Sequence: Accepts changes from builder
    - [ ] Scheduler: Applying required separation between flights

### WinForms Compatibility

- [ ] Revisit the `*` WPF size, and it's compatibility with WinForms.
- [ ] Consider Avalonia instead of WPF

### Session Management Overhaul

- [ ] Decouple Session from `MaestroConnection`
- [ ] Remove IMediator from `MaestroConnection` and make it observable
- [ ] Reconsider how messages get routed to the server (custom middleware, custom mediator, intermediate dispatcher, etc.)
- [ ] Show server options when starting a session initially
- [ ] Allow flow to declare their offline sequence as the source of truth before connecting, so they don't inherit a dirty sequence
- [ ] Show red background when in offline mode
- [ ] Show amber background when reconnecting
