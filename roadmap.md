# vMaestro Roadmap

## Housekeeping

- [ ] Check-in on GitHub issues. Try to reproduce the issues, fix them, or close them.

## Automatic Updates

- [ ] Implement automatic plugin updates via the plugin manager

## Refactor Sequence Aggregate

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

## Configuration Overhaul

Introduce support for transitions and approach types.
Configuration will be split into multiple files, one per airport.
Configuration files will likely require a new format to support tabular data such as arrivals.

- [ ] Re-design configuration types
    - [ ] Introduce transition fixes
    - [ ] Introduce approach types
- [ ] Introduce "Change Approach Type" request and handler
- [ ] Assign runway based on arrivals matching the runway mode, feeder, and transition fixes
    - [ ] Filter runway options based on feeder fix in the UI
- [ ] Remove runway requirements and preferences
- [ ] Store the processed arrival and runway mode on the Flight
    - [ ] Set landing estimate based on ETA_FF + arrival TTG
    - [ ] Set STA_FF using STA - arrival TTG
    - [ ] If ATO_FF is set, ETA should be ATO_FF + arrival TTG (this value won't change after passing FF, this is accurate)
- [ ] Consider new config file format

## Algorithm Overhaul

> **Note**
> Consult VATPAC before making a move on this. Keeping things realistic could significantly increase the workload for the AIS team.
> It may be easier to use vatSys performance data along with the track-miles of each arrival, rather than using time.

Revisit the sequencing and scheduling algorithms.

- [ ] ETA_FF = ETA - average TTG when no FF is set
- [ ] Revise insertions based on ETA. Some should be using ETA_FF.
- [ ] Specify delaying actions in the scheduling algorithm
- [ ] Model enroute and TMA trajectories
- [ ] Model runway allocation strategies (grographic, preferred, mixed)
- [ ] Model runway dependencies (dependent, semi-dependent, and independent)
- [ ] Account for GRIB winds

## Unit test review

- [ ] Compare test cases with reference material

## Model "Close" airports

- [ ] Flights within 25 mins flight time of the FF are from "Close" airports (e.g: Inside the TMA)
- [ ] Flights from "Close" airports will be added to the pending list

### Configuration Zone enhancements

- [ ] 10,000 ft and 6,000 ft winds (with configuration view)
- [ ] Achieved rates
- [ ] Units selector (NM, aircraft/hr, seconds)
- [ ] UTC time

### Ladder and Timeline enhancements

- [ ] Add support for multiple ladders and timelines
- [ ] Implement configuration for custom label layout and colors

### WinForms Compatibility

- [ ] Revisit the `*` WPF size, and it's compatibility with WinForms.
- [ ] Consider Avalonia instead of WPF

## Documentation

- [X] Write documentation for ATC usage
- [ ] Write documentation for configuration
- [ ] Architecture decision record

### Refactoring

- [ ] Refactor flight insertion handlers (Consider combining or separating them)
- [ ] Improve separation between domain models, DTOs, and view models
- [ ] Remove (or trim down) SequenceMessage and introduce smaller DTOs or notifications (consider CRDTs) to reduce the size of the sequence when serialised
- [ ] Consider moving some of the sequence logic into the individual handlers so they can be tested more easily.

#### Session Management Overhaul

- [ ] Decouple Session from `MaestroConnection`
- [ ] Remove IMediator from `MaestroConnection` and make it observable
- [ ] Reconsider how messages get routed to the server (custom middleware, custom mediator, intermediate dispatcher, etc.)
- [ ] Show server options when starting a session initially
- [ ] Allow flow to declare their offline sequence as the source of truth before connecting, so they don't inherit a dirty sequence
- [ ] Show red background when in offline mode
- [ ] Show amber background when reconnecting