# Sequence Decomposition

## Problem

`Sequence.cs` conflates two concerns: managing the flight list and running the MAESTRO scheduling algorithm. The core `Schedule(int startIndex)` method handles sequencing, runway assignment, STA computation, separation enforcement, and delay distribution in a single 300-line pass. Flights are mutated in-place across multiple calls (`SetRunway`, `SetApproachType`, `SetSequenceData`), leaving them in partially-updated intermediate states if anything fails mid-computation.

## Goals

- Model each MAESTRO algorithm stage as an individually testable service
- `Sequence` becomes a pure state bag - it does not run the algorithm
- `Flight` becomes an immutable record - it is only ever constructed once all stages have produced their results, eliminating partial state entirely

---

## Target architecture

### Flight is an immutable record

`Flight` is a C# `record`. It is only constructed when all algorithm stages have completed. There is no intermediate state - the old `Flight` instance remains valid until the new one is fully constructed and applied.

```
Flight = new Flight(flightDataRecord, schedulingResult, delayResult, modeResult)
```

### Sequence is a state bag

`Sequence` retains:
- The ordered flight list (read operations: `FindFlight`, `IndexOf`, `Flights`)
- Runway mode management (`CurrentRunwayMode`, `ChangeRunwayMode`, `GetRunwayModeAt`)
- Slot management (`CreateSlot`, `ModifySlot`, `DeleteSlot`)
- Wind properties
- Serialization (`ToDto`, `Restore`)
- `BuildSequence()` - produces the interleaved list of flights, slots, and runway mode changes used by the scheduler as context

`Sequence` loses all algorithm logic: `Schedule()`, `GetRunways()`, `EvaluateRunwayOption()`, and all nested helpers.

`Sequence.Apply(IReadOnlyList<Flight>)` is the single mutation point. It receives a fully-formed, consistent batch of flights and replaces the corresponding entries in the flight list. As a defence-in-depth measure, it validates that no two flights on the same runway are closer than the acceptance rate before committing.

### The pipeline (per-flight)

Each stage produces a typed result. No stage mutates a `Flight` directly.

```
FlightDataRecord  +  existing Flight (context)
        |
        v  [Stage 1] ETA calculation
        |
        v  [Stage 2+3] Sequencing + Scheduling (combined, iterative)
        |    Determines position in sequence and computes STA with separation.
        |    Runs ISequencer + IScheduler in a loop until delay is within
        |    the priority limit, or the flight cannot move further forward.
        |
        v  [Stage 4a] IDelayCalculator
        |    Distributes total delay between enroute and TMA phases.
        |    Wraps existing DelayStrategyCalculator logic.
        |
        v  [Stage 4b] IModeCalculator  (or explicit mode from handler)
        |    Computes State from scheduling/delay outputs and time thresholds.
        |    Handler may bypass this and supply State directly (e.g. manual freeze).
        |
        v  [Stage 5] Optimisation (not yet implemented)
        |
        v
   new Flight(...)   <-- fully formed, consistent
```

### FlightProcessor - the aggregate service

`IFlightProcessor` encapsulates all stages for a single flight. Handlers never call individual stage services directly.

```csharp
public interface IFlightProcessor
{
    // Processes the supplied flights as a left-fold:
    // each flight is computed against the sequence context PLUS the
    // already-computed results of flights earlier in the list.
    // Returns the full result set for a single atomic Apply().
    IReadOnlyList<Flight> Process(
        Sequence sequence,
        IReadOnlyList<FlightProcessingParameters> parameters);
}
```

The fold pattern ensures consistency: by the time any flight is processed, all flights ahead of it in the list are committed to the accumulated context with known STAs. Nothing is written to `Sequence` until `Apply()` is called with the complete batch.

```csharp
// Conceptual implementation
IReadOnlyList<Flight> Process(Sequence sequence, IReadOnlyList<FlightProcessingParameters> parameters)
{
    return parameters.Aggregate(
        seed: new List<Flight>(),
        func: (accumulated, param) =>
        {
            var flight = ProcessSingle(sequence, accumulated, param);
            return [..accumulated, flight];
        });
}
```

`ProcessSingle` uses both `sequence` (frozen/landed anchors and flights not in this batch) and `accumulated` (flights already processed in this batch) as separation context, with `accumulated` taking precedence for flights it contains.

---

## Processing order and sequence ordering

The `Sequence` flight list is maintained in **STA order**. Frozen and landed flights are fixed anchors.

Unstable flights are repositioned by **ETA** (not STA). Currently this happens in `FlightUpdatedHandler`, which moves each unstable flight to the position where its `LandingEstimate` fits in the STA-ordered list, subject to the constraint that it cannot move before any stable flight on the same runway. Scheduling then runs from that position forward.

The parameters list passed to `FlightProcessor.Process()` must reflect this same ordering. It is **not** a simple sort by priority then ETA - the ordering is:
- Stable/frozen/landed flights occupy fixed positions in STA order (anchors)
- Unstable flights are positioned by ETA within the gaps between stable flights, respecting the constraint that they cannot precede a stable flight on the same runway

The existing repositioning logic in `FlightUpdatedHandler` and the `EvaluateRunwayOption` / `FindInsertionPointForMaximumDelay` helpers in `Sequence.cs` capture the correct behaviour and should be preserved when extracting `ISequencer` and `IScheduler`.

---

## New interfaces

### `ISequencer`
Determines where a single flight should be positioned in the sequence, based on ETA, priority, and the current committed state.

### `IScheduler`
Given a flight's target position, computes the STA by enforcing separation from preceding flights (same runway: acceptance rate, same mode different runway: dependency rate, off-mode: off-mode separation rate). Handles runway assignment if not yet allocated.

### `IDelayCalculator`
Computes enroute and TMA delay distribution and control action. Formalises the existing `DelayStrategyCalculator` static class as an injectable interface. `DelayResult` supersedes `DelayDistribution`.

### `IModeCalculator`
Computes `State` from scheduling outputs, delay, current time, and airport configuration thresholds. Extracts the logic currently in `Flight.UpdateStateBasedOnTime()`.

### `IRunwaySelector`
Pure function. Determines eligible runway options for a flight given the active runway mode. Extracted from `GetRunways()` in `Sequence.cs` (marked with a TODO comment).

### `ISequencePositionEvaluator`
Computes earliest valid landing time at a given sequence position, enforcing separation from all preceding items (flights, slots, runway mode changes). Extracted from `EvaluateRunwayOption()` and its nested helpers.

---

## Parameter and result types

| Type | Stage | Purpose |
|------|-------|---------|
| `FlightProcessingParameters` | Input to `IFlightProcessor` | FDR data + existing flight context + optional overrides (runway, explicit mode, explicit position) |
| `SchedulingResult` | Output of sequencing+scheduling | Position, runway, approach type, trajectory, STA, STA_FF |
| `DelayResult` | Output of `IDelayCalculator` | Enroute delay, TMA delay, control action, flow controls. Supersedes `DelayDistribution`. |
| `ModeResult` | Output of `IModeCalculator` (or explicit) | The computed or explicitly set `State` |
| `RunwayOption` | Internal to scheduling | Runway identifier, approach type, required separation. Promoted from private record in `Sequence.cs`. |

`Flight` constructor: `new Flight(FlightDataRecord, SchedulingResult, DelayResult, ModeResult)`.

---

## Handler examples

**Periodic scheduling loop:**
```csharp
// Parameters built in sequence order (STA order for stable flights,
// ETA-positioned for unstable). See "Processing order" section above.
var parameters = BuildParametersInSequenceOrder(sequence, flightDataRecords, airportConfig, clock);
var results = flightProcessor.Process(sequence, parameters);
sequence.Apply(results);
```

**ChangeRunway (manual, subject + trailing):**
```csharp
var existing = sequence.FindFlight(callsign);
var startIndex = sequence.IndexOf(existing);

// Parameters for subject flight (with runway override) + all trailing flights.
// FlightProcessor folds over these, each seeing the accumulated results of prior ones.
var parameters = sequence.Flights
    .Skip(startIndex)
    .Select(f => new FlightProcessingParameters(
        FlightDataRecord: flightDataRecords[f.Callsign],
        ExistingFlight: f,
        Config: airportConfig,
        Now: clock.UtcNow(),
        RunwayOverride: f.Callsign == callsign ? newRunway : null))
    .ToList();

sequence.Apply(flightProcessor.Process(sequence, parameters));
```

**Manual freeze (explicit mode, bypasses IModeCalculator):**
```csharp
var existing = sequence.FindFlight(callsign);
var parameters = new FlightProcessingParameters(
    FlightDataRecord: flightDataRecords[callsign],
    ExistingFlight: existing,
    Config: airportConfig,
    Now: clock.UtcNow(),
    ExplicitMode: State.Frozen);

sequence.Apply(flightProcessor.Process(sequence, [parameters]));
```

---

## Priority and max delay

Per-priority maximum delay is configurable in `AirportConfiguration` (default: 5 minutes). When a flight's delay in excess of its natural delay exceeds the limit for its priority class, it is treated as the next class up.

The sequencing+scheduling loop within `FlightProcessor` handles this: after computing an STA that would exceed the limit, it attempts to move the flight to an earlier position. It stops when either the delay is within limits or the flight cannot move further (blocked by a higher-priority flight, frozen flight, slot, or runway mode change boundary).

---

## Implementation order

Each step must leave existing tests green before proceeding.

1. **Promote `ISequenceItem` types** - move private nested types (`FlightSequenceItem`, `SlotSequenceItem`, `RunwayModeChangeSequenceItem`) to internal files in `Model/`. No behaviour change.

2. **Promote `RunwayOption`** - move private record to `Model/RunwayOption.cs`. No behaviour change.

3. **Introduce result types** - create `SchedulingResult`, `DelayResult`, `ModeResult`, `FlightProcessingParameters`. No wiring yet.

4. **Extract `IRunwaySelector` + tests** - move `GetRunways()` to `RunwaySelector`. Tests: off-mode runway, feeder fix matching, no match fallback, stable flight preserves runway.

5. **Extract `ISequencePositionEvaluator` + tests** - move `EvaluateRunwayOption()` and all helpers. Tests: same-runway separation, dependency-runway separation, off-mode separation, slot avoidance, runway change window, max-delay constraint.

6. **Extract `IScheduler` + tests** - implement `Scheduler` using `IRunwaySelector`, `ISequencePositionEvaluator`, `ITrajectoryService`. Wire into existing `Sequence.Schedule()` temporarily. Tests: multi-flight separation, runway assignment.

7. **Extract `ISequencer` + tests** - implement `Sequencer` for ETA-based positioning. Tests: ETA ordering, stable anchor constraint, max-delay priority graduation.

8. **Introduce `IDelayCalculator` and `IModeCalculator`** - wrap `DelayStrategyCalculator`, extract `UpdateStateBasedOnTime()`. Existing `DelayStrategyCalculatorTests` must still pass.

9. **Make `Flight` immutable** - convert to `record`. Implement `new Flight(fdr, schedResult, delayResult, modeResult)`. Add `Sequence.Apply(IReadOnlyList<Flight>)`.

10. **Implement `IFlightProcessor`** - wire all stages into the fold. Update `FlightUpdatedHandler` and all request handlers to use `IFlightProcessor` + `sequence.Apply()`.

11. **Remove `Schedule()` from `Sequence`** - remove `Schedule(int)`, `Schedule(Flight, ...)`, `GetRunways()`, `EvaluateRunwayOption()`, `ITrajectoryService` dependency. Remove `Move()`, `Insert()`, `Remove()` mutation methods (replaced by `Apply()`). Update `MaestroInstanceManager.cs:72`.

12. **(Future) Merge `Sequence` into `Session`** - once `Sequence` is a pure state bag, its contents can live directly on `Session`.

---

## Critical files

- `source/Maestro.Core/Model/Sequence.cs` - primary decomposition target (1005 lines)
- `source/Maestro.Core/Model/Flight.cs` - becomes immutable record
- `source/Maestro.Core/Handlers/FlightUpdatedHandler.cs` - primary consumer, contains unstable flight repositioning logic
- `source/Maestro.Core/Hosting/MaestroInstanceManager.cs` - sole `Sequence` construction site (line 72)
- `source/Maestro.Core.Tests/Builders/SequenceBuilder.cs` - update as constructor changes through steps
- `source/Maestro.Core.Tests/Model/SequenceTests.cs` - acceptance criteria; must stay green throughout
