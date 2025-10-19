# vMaestro Roadmap

## Investigate flight ordering

When testing on sweatbox, flights seemed to end up in a wacky order. Make sure unstable flights are properly being repositioned each FDP update. Double check unit tests.

`WhenFlightIsInserted_AfterAnotherFlight_TheFlightIsInsertedBehindTheReferenceFlightAndAnyTrailingConflictsAreDelayed` demonstrates part of the problem.

Consider re-ordering flights to reduce delays (i.e: ideal position is in front of the next flight, and there is sufficient space to move there)

## Remove hybrid estimate calculations

Estimates are sourced from system estimates until `MinimumRadarEstimateRange`, then the average between a BRL estimate (GS / dist) and the system estimate is used. This tends to be somewhat inaccurate.

- [ ] Remove hybrid estimate calculation
- [ ] Allow either the BRL method, or the system estimate method in the configuration
- [ ] Source winds from GRIB and factor them into system estimates (include a feature toggle for testing)

## Arrival Configuration Overhaul

- [ ] Move arrival configurations into a separate `csv` file
- [ ] Introduce transition fixes
- [ ] Introduce an approach types
    - [ ] Introduce "Change Approach Type" request and handler
- [ ] Assign runway based on arrivals matching the runway mode, feeder, and transition fixes
    - [ ] Filter runway options based on feeder fix in the UI
- [ ] Remove runway requirements and preferences
- [ ] Store the processed arrival and runway mode on the Flight
    - [ ] Set landing estimate based on ETA_FF + arrival TTG
    - [ ] Set STA_FF using STA - arrival TTG
    - [ ] If ATO_FF is set, ETA should be ATO_FF + arrival TTG (this value won't change after passing FF, this is accurate)

## Manual Delay

- [ ] Replace the `ZeroDelay` field with a `ManualDelay` field. This should be a `TimeSpan` representing the maximum delay that can be allocated to a flight.

## Coordination System

- [ ] Implement the coordination system with pre-canned messages

## Documentation

- [ ] Write documentation for ATC usage
- [ ] Write documentation for configuration
- [ ] Architecture decision record

## Things to revisit after release

- [ ] Insert flight from feeder-fix ladders

## TMA Delay

- [ ] Include TMA pressure in arrival/runway mode configuration
- [ ] Separate enroute and TMA delay

## Account for "Close" airports

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
- [ ] Implement ladder scrolling

### WinForms Compatibility

- [ ] Revisit the `*` WPF size, and it's compatibility with WinForms.

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
