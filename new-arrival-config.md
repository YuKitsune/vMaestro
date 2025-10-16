# New Arrival Config

We need to refactor how arrivals are handled within Maestro.

## Configuration

Arrivals should be defined in a separate CSV file.
Each arrival has the following fields:

- **Airport Identifier**: The airport this arrival blongs to
- **Feeder fix identifier**: The name of the feeder fix
- **Transition fix**: An optional field used to define multiple arrival intervals for the same arrival when multiple transitions exist
- **Runway identifier**: The runway this arrival applies to
- **Aircraft category**: The category of aircraft this arrival applies to
- **Aircraft types**: An optional list of aircraft type codes this arrival _can also_ apply to
- **Approach type**: An optional field used to separate arrival times for the same feeder fix and runway when multiple approach types exist
- **Interval**: The amount of time it takes to travel from the feeder fix to the runway
- **Distance**: An optional field for the number of track miles from the feeder fix to the runway

The main maestro config file should contain a reference to this CSV file.

The Maestro plugin should load the CSV file on startup and store the results in-memory.

## Matching Behaviour

If a flight has either a matching aircraft category, or aircraft type, the arrival can be applied to that flight. If neither match, then the arrival cannot be used with that flight.

If an arrival has a transition fix, and the flight does not have that fix in it's route, it cannot be applied to that flight.

Airport + Feeder Fix + Transition Fix (optional) + Runway + (Aircraft Category or Aircraft Type) + Approach Type (optional) = interval (and optional distance)

## Changes to Runways

Runway configurations must now include the available approach types.

The runway model should now be stored on the flight itself, along with the arrival configuration used for that flight.
This will reduce the number of lookups performed in the Sequence.

## Functions

### Flight Creation

When a flight is created, a runway should be assigned based on the runway mode active at their landing time, and the first matching arrival based on the feeder fix (and transition fix, if any), and aircraft category or type.

It should no longer be the responsibility of the `Sequence` type to assign the runway.

### Change Runway Mode

When a new runway mode is selected, all flights scheduled to land after the new mode becomes effective should be assigned to the new runway mode as though they're new flights.

### Change Runway

When the runway is changed, the the arrival should be updated to the first matching arrival based on the selected approach type and runway.

If a runway is selected that is off-mode, the default acceptance rate should be used for that runway.

### Change Approach Type (new)

When the approach type is changed, the arrival should be updated to the first matching arrival based on the selected approach type and runway.

We'll need to provide a method for looking up the available approach types based on the runway, so that we can populate the dropdown in the UI with valid approach types.

## TODOs

- [X] Add Approach Type to Flight
    - [X] DTOs and ViewModels
    - [X] Change Approach Type function
- [X] Introduce new arrival configuration
    - [X] Introduce new type
    - [X] Load from CSV file
- [ ] Lookup TTG from arrival configuration (30 mins)
    - [ ] Replace existing lookups with new configuration
- [ ] Clean up runway types (1 hr)
    - [ ] Split root runway config from runway mode runways
    - [ ] Specify a default or off-mode landing rate
- [ ] Revisit domain modelling (1hr)
    - [ ] Try storing the required separation and TTG on the flight model to prevent runtime lookups
