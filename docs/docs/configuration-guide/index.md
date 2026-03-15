---
displayed_sidebar: null
---

# Configuration Guide

This guide provides detailed information on configuring the MAESTRO Plugin for vatSys.
The configuration file (`Maestro.yaml`) defines all airport-specific settings including runway modes, trajectories, and display preferences.

## Configuration File Location

The `Maestro.yaml` file should be placed in your vatSys profile directory.
Maestro will automatically load this configuration on startup.

Maestro searches for the configuration file in the following locations (in order):

1. `{ProfileDirectory}/Plugins/Configs/Maestro/Maestro.yaml`
2. `{ProfileDirectory}/Plugins/Configs/Maestro.yaml`
3. `{ProfileDirectory}/Plugins/Maestro.yaml`
4. `{ProfileDirectory}/Maestro.yaml`
5. `{PluginDirectory}/Maestro.yaml` (plugin installation directory)

The first location where the file is found will be used.
For most installations, placing the file at `{ProfileDirectory}/Maestro.yaml` is recommended.

## Configuration Structure

The configuration file is divided into four main sections:

1. **Logging**: Log file settings
2. **Server**: Server configuration and permissions for online use
3. **Labels**: Shared label layouts and color definitions
4. **Airports**: Airport-specific configuration (runway modes, trajectories, views)

## Logging Configuration

Controls log file behavior for troubleshooting and diagnostics.

The log level determines how much detail is recorded in the log files.
More verbose logging levels create larger log files but provide more information for troubleshooting.
Use `Information` for normal operations, `Debug` or `Verbose` for troubleshooting issues.

```yaml
Logging:
  # Available values: Verbose, Debug, Information, Warning, Error, Fatal
  # - Verbose: Maximum detail (largest log files)
  # - Debug: Detailed diagnostic information
  # - Information: General operational messages (recommended)
  # - Warning: Warning messages only
  # - Error: Error messages only
  # - Fatal: Critical errors only (smallest log files)
  LogLevel: Information

  # Number of days to retain log files
  MaxFileAgeDays: 7
```

### Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `LogLevel` | string | Yes | - | Minimum log level to record. Options: `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal` |
| `MaxFileAgeDays` | integer | Yes | - | Number of days to retain log files before automatic deletion |

## Server Configuration

Configures the connection to the Maestro server for online mode.

The server URI must point to the SignalR hub endpoint of the Maestro server (typically ending in `/hub`).

Partitions allow multiple independent sequences to be active for the same airport simultaneously.
This is useful for separating live VATSIM operations from training sessions.
Each partition maintains its own sequence state and does not affect other partitions.

```yaml
Server:
  Uri: https://maestro.example.com/hub  # Must point to the /hub endpoint
  Partitions:
    - VATSIM          # Live network operations
    - SweatBox-1      # Training session 1
    - SweatBox-2      # Training session 2
  TimeoutSeconds: 30
  Permissions:
    ChangeTerminalConfiguration: [Flow]
    ChangeLandingRates: [Flow]
    MoveFlight: [Approach, Flow]
    ChangeFeederFixEstimate: [Enroute, Flow]
    ManageSlots: [Flow]
    InsertFlight: [Approach, Flow]
    MakePending: [Enroute, Approach, Flow]
    ChangeRunway: [Enroute, Approach, Flow]
    ManualDelay: [Enroute, Approach, Flow]
    MakeStable: [Enroute, Flow]
    Recompute: [Enroute, Flow]
    Desequence: [Enroute, Approach, Flow]
    Resequence: [Flow]
    RemoveFlight: [Enroute, Approach, Flow]
    ChangeApproachType: [Enroute, Approach, Flow]
```

### Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Uri` | string (URL) | Yes | - | SignalR endpoint for the Maestro server Hub |
| `Partitions` | array of strings | Yes | `["Default"]` | Network partitions for isolating sessions |
| `TimeoutSeconds` | integer | No | 30 | Maximum time to wait for server connection before failing |
| `Permissions` | object | Yes | - | Maps actions to roles that can perform them |

### Permission Actions

The following actions can be configured with role-based permissions:

- `ChangeTerminalConfiguration` - Change runway modes
- `ChangeLandingRates` - Modify landing rate values
- `MoveFlight` - Reorder flights in the sequence
- `ChangeFeederFixEstimate` - Modify the ETA_FF via manual entry
- `ManageSlots` - Create, modify, or delete runway closure slots
- `InsertFlight` - Manuall add new flights to the sequence
- `MakePending` - Return flights to the pending list
- `ChangeRunway` - Change assigned runway for flights
- `ManualDelay` - Assign manual delays to flights
- `MakeStable` - Force flights to become Stable
- `Recompute` - Trigger sequence recomputation
- `Desequence` - Remove flights from the sequence
- `Resequence` - Move flights from the Desequenced list back into the Sequence
- `RemoveFlight` - Delete flights from the sequence
- `ChangeApproachType` - Change the assigned approach type for flights

The available roles are: `Enroute`, `Approach`, and `Flow`.
These roles correspond to the standard ATC positions that interact with Maestro.

## Labels Configuration

Defines global color schemes and reusable label layouts for flight display.

### Global Colors

```yaml
Labels:
  GlobalColours:

    # RGB colours for each flight State
    States:
      Unstable: 255, 205, 105
      Stable: 0, 0, 96
      SuperStable: 255, 255, 255
      Frozen: 96, 0, 0
      Landed: 0, 235, 235

    # RGB colours for each control action
    ControlActions:
      Expedite: 0, 105, 0
      NoDelay: 0, 0, 96
      Resume: 0, 0, 96
      SpeedReduction: 0, 235, 235
      PathStretching: 255, 255, 255
      Holding: 235, 235, 0
    
    # RGB color for flights sequenced in a deferred runway mode
    DeferredRunwayMode: 255, 255, 255
```

### Label Layouts

Label layouts define how flight information is displayed.
Each layout consists of items arranged from innermost to outermost.

```yaml
Labels:
  Layouts:
    - Identifier: Enroute
      Items:
        - {Type: LandingTime, Width: 2, Padding: 1}
        - {Type: Runway, Width: 3, Padding: 1, ColourSources: [RunwayMode, Runway]}
        - {Type: Callsign, Width: 10, Padding: 1, ColourSources: [State]}
        - {Type: ApproachType, Width: 1, Padding: 1, ColourSources: [ApproachType]}
        - {Type: HighSpeed, Width: 1, Padding: 0, Symbol: '+'}
        - {Type: ManualDelay, Width: 1, Padding: 0, ZeroDelaySymbol: '#', ManualDelaySymbol: '%'}
        - {Type: CouplingStatus, Width: 1, Padding: 1, UncoupledSymbol: '*'}
        - {Type: RequiredDelay, Width: 3, Padding: 1, ColourSources: [ControlAction]}
        - {Type: RemainingDelay, Width: 3, Padding: 0, ColourSources: [ControlAction]}
```

#### Label Item Types

| Type | Description | Special Properties |
|------|-------------|--------------------|
| `Callsign` | Aircraft callsign | |
| `AircraftType` | Aircraft type code | |
| `AircraftWakeCategory` | Wake turbulence category (L/M/H/J) | |
| `Runway` | Assigned runway | |
| `ApproachType` | Assigned approach type | |
| `LandingTime` | Scheduled landing time (STA) | |
| `FeederFixTime` | Feeder fix scheduled time (STA_FF) | |
| `RequiredDelay` | Total delay required | |
| `RemainingDelay` | Remaining delay to absorb | |
| `ManualDelay` | Manual delay indicator | `ZeroDelaySymbol`, `ManualDelaySymbol` |
| `HighSpeed` | High speed indicator | `Symbol` |
| `CouplingStatus` | Coupling status indicator | `UncoupledSymbol` |

#### Label Item Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Type` | string | Yes | Item type (see table above) |
| `Width` | integer | Yes | Display width in number of characters |
| `Padding` | integer | No | Padding in characters (default: 0) |
| `ColourSources` | array | No | Color sources in priority order |

#### Color Sources

Color sources determine what colors an item.
Multiple sources can be specified, with the first available source used:

- `Runway`: Color based on assigned runway, sourced from the airport colour configuration
- `ApproachType`: Color based on approach type, sourced from the airport colour configuration
- `FeederFix`: Color based on feeder fix, sourced from the airport colour configuration
- `State`: Color based on flight state, sourced from the global colour configuration
- `RunwayMode`: Color based on runway mode, sourced from the global colour configuration
- `ControlAction`: Color based on control action, sourced from the global colour configuration

## Airport Configuration

Each airport requires detailed configuration including runways, feeder fixes, trajectory data, and display views.

### Basic Airport Settings

```yaml
Airports:
  - Identifier: YSSY
    FeederFixes: [RIVET, WELSH, BOREE, YAKKA, MARLN]
    Runways: [34L, 34R, 16L, 16R, "07", "25"]

    # Default values
    DefaultAircraftType: B738
    DefaultPendingFlightState: Stable
    DefaultDepartureFlightState: Unstable
    DefaultDummyFlightState: Frozen
    ManualInteractionState: Stable

    # State transition times
    FlightCreationThresholdMinutes: 120
    MinimumUnstableMinutes: 5
    StabilityThresholdMinutes: 25
    FrozenThresholdMinutes: 15

    # Flight retention
    MaxLandedFlights: 5
    LandedFlightTimeoutMinutes: 10
    LostFlightTimeoutMinutes: 10
```

#### Airport Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Identifier` | string | Yes | - | ICAO airport code |
| `FeederFixes` | array | Yes | - | List of feeder fix identifiers that aircraft will be sequenced via |
| `Runways` | array | Yes | - | List of runway identifiers (note that quotes are required for runways that contain only numeric characters) |
| `DefaultAircraftType` | string | No | `B738` | Fallback aircraft type used when the type cannot be determined from flight data |
| `DefaultPendingFlightState` | string | No | `Stable` | Initial state for flights being inserted from the Pending list |
| `DefaultDepartureFlightState` | string | No | `Unstable` | Initial state for flights departing from designated departure airports |
| `DefaultDummyFlightState` | string | No | `Frozen` | Initial state for manually inserted flights |
| `ManualInteractionState` | string | No | `Stable` | State to transition flights to after any manual intervention by a controller |
| `FlightCreationThresholdMinutes` | integer | No | 120 | How far from landing (in minutes) flights must be before Maestro starts tracking them |
| `MinimumUnstableMinutes` | integer | No | 5 | Prevents flights from transitioning from Unstable too quickly, allowing time for ETA stabilization |
| `StabilityThresholdMinutes` | integer | No | 25 | Flights become Stable this many minutes before their feeder fix time (STA_FF) |
| `FrozenThresholdMinutes` | integer | No | 15 | Flights become Frozen this many minutes before landing (STA), preventing further changes |
| `MaxLandedFlights` | integer | No | 5 | Maximum number of landed flights to keep visible in the sequence in case of an overshoot |
| `LandedFlightTimeoutMinutes` | integer | No | 10 | Landed flights are removed after this duration |
| `LostFlightTimeoutMinutes` | integer | No | 10 | Flights that have not been updated by vatSys after this duration are removed |

Flight states: `Unstable`, `Stable`, `SuperStable`, `Frozen`

### Airport Colors

Define colors for runways, approach types, and feeder fixes.

```yaml
Airports:
  - Identifier: YSSY
    Colours:
      Runways:
        34L: 255, 0, 0
        34R: 0, 255, 0

      ApproachTypes:
        I: 255, 255, 0
        V: 0, 255, 255

      FeederFixes:
        RIVET: 96, 0, 0
        WELSH: 96, 0, 0
        BOREE: 0, 235, 235
```

All airport-specific colors are optional. Colors are RGB values (0-255).

### Runway Modes

Runway modes define TMA configurations with landing rates and runway assignments.

```yaml
RunwayModes:
  - Identifier: 34IVA
    DependencyRateSeconds: 0          # Optional: inter-runway separation
    OffModeSeparationSeconds: 0       # Optional: separation for off-mode runways
    Runways:
      - Identifier: 34L
        ApproachType: I                # Optional: approach type for this runway
        LandingRateSeconds: 180        # Minimum separation between arrivals
        FeederFixes: [RIVET, WELSH]    # Optional: preferred feeder fixes
      - Identifier: 34R
        LandingRateSeconds: 180
        FeederFixes: [BOREE, YAKKA, MARLN]
```

#### Runway Mode Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Identifier` | string | Yes | - | Runway mode name |
| `DependencyRateSeconds` | integer | No | 0 | Additional separation applied when consecutive flights land on different runways within this mode (for dependent parallel operations) |
| `OffModeSeparationSeconds` | integer | No | 0 | Additional separation applied when a flight lands on a runway not defined in this mode |
| `Runways` | array | Yes | - | List of runway configurations active in this mode |

#### Runway Configuration Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Identifier` | string | Yes | - | Runway identifier (must match airport runways) |
| `ApproachType` | string | No | empty | The approach type to assign to flights assigned to this runway |
| `LandingRateSeconds` | integer | Yes | - | Minimum time separation between successive landings on this runway (acceptance rate) |
| `FeederFixes` | array | No | [] | Flights via these feeder fixes will be preferentially assigned to this runway |

### Trajectories

Trajectories define flight times from feeder fixes to runways for different aircraft types.

```yaml
Trajectories:
  # BOREE trajectories
  - {FeederFix: BOREE, Aircraft: [JET, DH8D], RunwayIdentifier: 34L, TimeToGoMinutes: 17}
  - {FeederFix: BOREE, Aircraft: [JET, DH8D], RunwayIdentifier: 34R, TimeToGoMinutes: 17}
  - {FeederFix: BOREE, Aircraft: [NONJET], RunwayIdentifier: 34L, TimeToGoMinutes: 20}
  - {FeederFix: BOREE, Aircraft: [NONJET], RunwayIdentifier: 34R, TimeToGoMinutes: 20}

  # RIVET trajectories
  - {FeederFix: RIVET, Aircraft: [JET, DH8D], RunwayIdentifier: 34L, TimeToGoMinutes: 15}
  - {FeederFix: RIVET, Aircraft: [JET, DH8D], RunwayIdentifier: 34R, TimeToGoMinutes: 18}
  - {FeederFix: RIVET, Aircraft: [NONJET], RunwayIdentifier: 34L, TimeToGoMinutes: 17}
  - {FeederFix: RIVET, Aircraft: [NONJET], RunwayIdentifier: 34R, TimeToGoMinutes: 20}
```

#### Aircraft Descriptors

Aircraft can be matched using:

- `ALL` - All aircraft types
- `JET` - Jet aircraft
- `NONJET` or `PROP` - Non-jet aircraft
- `LIGHT` or `L` - Light wake category
- `MEDIUM` or `M` - Medium wake category
- `HEAVY` or `H` - Heavy wake category
- `SUPER`, `SUPERHEAVY`, `S`, or `J` - Super heavy wake category
- Specific ICAO type code (e.g., `B738`, `A388`)

#### Trajectory Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `FeederFix` | string | Yes | - | Feeder fix identifier |
| `Aircraft` | array | Yes | - | Aircraft types or categories this trajectory applies to (see Aircraft Descriptors above) |
| `RunwayIdentifier` | string | Yes | - | Destination runway for this trajectory |
| `ApproachType` | string | No | empty | Restricts this trajectory to flights using a specific approach type |
| `ApproachFix` | string | No | empty | Restricts this trajectory to flights using a specific approach fix or STAR transition |
| `TimeToGoMinutes` | integer | Yes | - | Expected flight time in minutes from the feeder fix to landing on the runway |

You must define trajectories for all combinations of feeder fixes, runways, and aircraft categories you expect to sequence.

If a trajectory is not defined for a particular flight, all trajectories matching that aircrafts type will be combined into an average.

### Departure Airports

Define flight times from departure airports to the sequenced airport.

```yaml
DepartureAirports:
  - {Identifier: YPMQ, Aircraft: [JET], Distance: 209, EstimatedFlightTimeMinutes: 44}
  - {Identifier: YPMQ, Aircraft: [NONJET], Distance: 209, EstimatedFlightTimeMinutes: 41}
```

#### Departure Airport Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Identifier` | string | Yes | - | ICAO code of departure airport |
| `Aircraft` | array | Yes | - | Aircraft types or categories this entry applies to (see Aircraft Descriptors above) |
| `Distance` | number | Yes | - | Distance in nautical miles (reserved for future use) |
| `EstimatedFlightTimeMinutes` | integer | Yes | - | Expected flight time from this departure airport to the sequenced airport, used to calculate ETAs for departures |

### Views

Views define how the sequence is displayed for different controller positions.

```yaml
Views:
  - Identifier: GUN/BIK
    LabelLayout: Enroute              # References a label layout by identifier
    TimeWindowMinutes: 30             # Time window to display
    Reference: FeederFixTime          # Options: FeederFixTime or LandingTime
    Direction: Down                   # Optional: Up or Down (default: Down)
    Ladders:
      - FeederFixes: [RIVET]          # Filter by feeder fixes
        Runways: []                   # Optional: filter by runways
      - FeederFixes: [WELSH]
        Runways: []

  - Identifier: RWY
    LabelLayout: TMA
    TimeWindowMinutes: 45
    Reference: LandingTime
    Ladders:
      - Runways: [34L, 16R, "07"]     # Filter by runways
        FeederFixes: []               # Optional: filter by feeder fixes
      - Runways: [34R, 16L, "25"]
```

#### View Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Identifier` | string | Yes | - | View name |
| `LabelLayout` | string | Yes | - | References a label layout defined in the Labels section |
| `TimeWindowMinutes` | integer | Yes | - | The time range (in minutes) to display on the vertical timeline |
| `Reference` | string | Yes | - | What time to use for vertical positioning: `FeederFixTime` (STA_FF) or `LandingTime` (STA) |
| `Direction` | string | No | `Down` | Which direction the timeline scrolls: `Up` (newest at bottom) or `Down` (newest at top) |
| `Ladders` | array | Yes | - | List of ladder (column) configurations to display side-by-side |

#### Ladder Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `FeederFixes` | array | [] | Shows only flights via these feeder fixes in this ladder (empty array shows all feeder fixes) |
| `Runways` | array | [] | Shows only flights assigned to these runways in this ladder (empty array shows all runways) |

Each ladder represents a column in the display.
Flights matching the filters appear in that ladder.

### Coordination Messages

Define templates for coordination messages between controllers.

```yaml
GlobalCoordinationMessages:
  - WX Dev have commenced.
  - Standby for configuration change.
  - Flow Planning in progress - do not use delay times until advised.
  - Maestro delay times accurate.
  - Contingency - No FMP.
  - General Holding.

FlightCoordinationMessages:
  - '{Callsign} MEDEVAC'
  - '{Callsign} diverting'
  - '{Callsign} request high speed descent'
  - '{Callsign} subject to CDO'
```

Flight coordination messages can use `{Callsign}` placeholder which will be replaced with the actual callsign.

## Complete Example

See `Maestro.example.yaml` for a complete annotated example configuration.

## Configuration Validation

When Maestro loads your configuration, it will validate:

- All required fields are present
- Runway identifiers in modes match the airport's runway list
- Feeder fixes in trajectories match the airport's feeder fix list
- Label layouts referenced in views exist
- All aircraft descriptors are valid

Check the log files if Maestro fails to load your configuration.
The error message will indicate what needs to be corrected.
