---
sidebar_position: 2
---

# Plugin Configuration

The `Maestro.yaml` configuration file defines all settings for the Maestro Plugin, including airport configuration, runway modes, trajectories, and display preferences.

## Configuration File Location

The configuration file should be placed in the vatSys profile directory.
vMaestro searches the following locations in order:

1. `{ProfileDirectory}/Plugins/Configs/Maestro/Maestro.yaml`
2. `{ProfileDirectory}/Plugins/Configs/Maestro.yaml`
3. `{ProfileDirectory}/Plugins/Maestro.yaml`
4. `{ProfileDirectory}/Maestro.yaml`
5. `{PluginDirectory}/Maestro.yaml`

For most installations, `{ProfileDirectory}/Maestro.yaml` is recommended.

## Configuration Structure

The configuration file contains the following sections:

1. **CheckForUpdates**: Plugin update behaviour
2. **Logging**: Log file settings
3. **Server**: Server connection and permissions
4. **Labels**: Colour schemes and label layouts
5. **Airports**: Airport-specific configuration

## Plugin Settings

### Update Checks

```yaml
CheckForUpdates: false
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CheckForUpdates` | boolean | `true` | Whether to check for updates on startup. Set to `false` to maintain control over plugin versions. |

<!-- TODO: Suggest disabling if you need to maintain control over which version of MAESTRO is used for compatibility with a self-hosted server -->

## Logging

```yaml
Logging:
  LogLevel: Information
  MaxFileAgeDays: 7
```

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `LogLevel` | string | Yes | Minimum level to log: `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal` |
| `MaxFileAgeDays` | integer | Yes | Days to retain log files before deletion |

Use `Information` for normal operation. Use `Debug` or `Verbose` for troubleshooting.

## Server Configuration

Configures the connection to the vMaestro server for online operation.

```yaml
Server:
  Uri: https://maestro.example.com/hub
  Environments:
    - VATSIM
    - SweatBox-1
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
    ModifyWinds: [Approach, Flow]
```

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Uri` | string | Yes | - | SignalR hub endpoint (must end in `/hub`) |
| `Environments` | array | Yes | `["Default"]` | Available environments for session isolation |
| `TimeoutSeconds` | integer | No | 30 | Connection timeout |
| `Permissions` | object | Yes | - | Maps actions to permitted roles |

### Environments

Environments allow multiple independent sequences for the same airport. Use cases include:

- Separating live operations from training
- Running concurrent training sessions

### Permissions

Available roles: `Enroute`, `Approach`, `Flow`

Available actions:

| Action | Description |
|--------|-------------|
| `ChangeTerminalConfiguration` | Change runway modes |
| `ChangeLandingRates` | Modify acceptance rates |
| `MoveFlight` | Reorder flights in the sequence |
| `ChangeFeederFixEstimate` | Modify ETA_FF manually |
| `ManageSlots` | Create, modify, or delete slots |
| `InsertFlight` | Add flights to the sequence |
| `MakePending` | Return flights to the pending list |
| `ChangeRunway` | Change assigned runway |
| `ManualDelay` | Assign delay limits |
| `MakeStable` | Force flights to Stable state |
| `Recompute` | Recalculate flight parameters |
| `Desequence` | Remove flights to desequenced list |
| `Resequence` | Return flights from desequenced list |
| `RemoveFlight` | Delete flights from sequence |
| `ChangeApproachType` | Change approach type |
| `ModifyWinds` | Manually adjust wind directions and speeds |

## Labels Configuration

Defines colours and layouts for flight labels.

### Global Colours

```yaml
Labels:
  GlobalColours:
    States:
      Unstable: 255, 205, 105
      Stable: 0, 0, 96
      SuperStable: 255, 255, 255
      Frozen: 96, 0, 0
      Landed: 0, 235, 235

    ControlActions:
      Expedite: 0, 105, 0
      NoDelay: 0, 0, 96
      Resume: 0, 0, 96
      SpeedReduction: 0, 235, 235
      PathStretching: 255, 255, 255
      Holding: 235, 235, 0

    DeferredRunwayMode: 255, 255, 255
```

Colours are RGB values (0-255).

### Label Layouts

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
        - {Type: RequiredDelay, Width: 3, Padding: 1, ColourSources: [RequiredControlAction]}
        - {Type: RemainingDelay, Width: 3, Padding: 0, ColourSources: [RemainingControlAction]}
```

#### Item Types

| Type | Description | Special Properties |
|------|-------------|--------------------|
| `Callsign` | Aircraft callsign | |
| `AircraftType` | Aircraft type code | |
| `AircraftWakeCategory` | Wake category (L/M/H/J) | |
| `Runway` | Assigned runway | |
| `ApproachType` | Approach type | |
| `LandingTime` | Scheduled landing time | |
| `FeederFixTime` | Scheduled feeder fix time | |
| `RequiredDelay` | Delay assigned at scheduling time | `Component` |
| `RemainingDelay` | Remaining delay to absorb | `Component` |
| `ManualDelay` | Manual delay indicator | `ZeroDelaySymbol`, `ManualDelaySymbol` |
| `HighSpeed` | High speed indicator | `Symbol` |
| `CouplingStatus` | Coupling status | `UncoupledSymbol` |

#### Item Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Type` | string | Yes | Item type |
| `Width` | integer | Yes | Display width in characters |
| `Padding` | integer | No | Padding in characters |
| `ColourSources` | array | No | Colour sources in priority order |

#### Delay Component

The `Component` property on `RequiredDelay` and `RemainingDelay` controls which portion of the split delay is displayed. Defaults to `Total`.

| Value | Description |
|-------|-------------|
| `Total` | Sum of enroute and terminal delay |
| `Enroute` | Enroute delay only |
| `Tma` | Terminal delay only |

#### Colour Sources

- `Runway` - From airport runway colours
- `ApproachType` - From airport approach colours
- `FeederFix` - From airport feeder fix colours
- `State` - From global state colours
- `RunwayMode` - From global runway mode colour
- `RequiredControlAction` - From global control action colours representing the required delay
- `RemainingControlAction` - From global control action colours representing the remaining delay

## Airport Configuration

Each airport requires its own configuration section.

### Basic Settings

```yaml
Airports:
  - Identifier: YSSY
    FeederFixes: [RIVET, WELSH, BOREE, YAKKA, MARLN]
    Runways: [34L, 34R, 16L, 16R, "07", "25"]

    DefaultAircraftType: B738
    DefaultPendingFlightState: Stable
    DefaultDepartureFlightState: Unstable
    DefaultDummyFlightState: Frozen
    ManualInteractionState: Stable

    FlightCreationThresholdMinutes: 120
    MinimumUnstableMinutes: 5
    StabilityThresholdMinutes: 25
    FrozenThresholdMinutes: 15

    MaxLandedFlights: 5
    LandedFlightTimeoutMinutes: 10
    LostFlightTimeoutMinutes: 10

    AverageLandingSpeed: 150
    UpperWindAltitude: 6000
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Identifier` | string | - | ICAO code |
| `FeederFixes` | array | - | Feeder fix identifiers |
| `Runways` | array | - | Runway identifiers (quote numeric-only runways) |
| `DefaultAircraftType` | string | `B738` | Fallback aircraft type |
| `DefaultPendingFlightState` | string | `Stable` | State for pending insertions |
| `DefaultDepartureFlightState` | string | `Unstable` | State for departures |
| `DefaultDummyFlightState` | string | `Frozen` | State for dummy flights |
| `ManualInteractionState` | string | `Stable` | State after manual changes |
| `FlightCreationThresholdMinutes` | integer | 120 | Tracking range in minutes |
| `MinimumUnstableMinutes` | integer | 5 | Minimum time in Unstable state |
| `StabilityThresholdMinutes` | integer | 25 | Minutes before ETA_FF to become Stable |
| `FrozenThresholdMinutes` | integer | 15 | Minutes before STA to become Frozen |
| `MaxLandedFlights` | integer | 5 | Landed flights to retain |
| `LandedFlightTimeoutMinutes` | integer | 10 | Landed flight retention time |
| `LostFlightTimeoutMinutes` | integer | 10 | Timeout for lost flights |
| `AverageLandingSpeed` | integer | 150 | Average landing speed (TAS) in knots for distance calculations |
| `UpperWindAltitude` | integer | 6000 | Altitude in feet for upper winds from GRIB data |
| `DefaultMaxEnrouteLinearDelayMinutes` | integer | `8` | Default enroute delay capacity used when no matching enroute trajectory is configured |

Flight states: `Unstable`, `Stable`, `SuperStable`, `Frozen`

### Airport Colours

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
```

All airport colours are optional. Values are RGB (0-255).

### Runway Modes

```yaml
RunwayModes:
  - Identifier: 34IVA
    DependencyRateSeconds: 0
    OffModeSeparationSeconds: 0
    Runways:
      - Identifier: 34L
        ApproachType: I
        LandingRateSeconds: 180
        FeederFixes: [RIVET, WELSH]
      - Identifier: 34R
        LandingRateSeconds: 180
        FeederFixes: [BOREE, YAKKA, MARLN]
```

#### Runway Mode Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Identifier` | string | - | Mode name |
| `DependencyRateSeconds` | integer | 0 | Additional separation for dependent runways |
| `OffModeSeparationSeconds` | integer | 0 | Separation for off-mode runways |
| `Runways` | array | - | Runway configurations |

#### Runway Configuration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Identifier` | string | - | Runway identifier |
| `ApproachType` | string | - | Approach type to assign |
| `LandingRateSeconds` | integer | - | Acceptance rate |
| `FeederFixes` | array | [] | Preferred feeder fixes |

### Trajectories

Trajectories define the path from a feeder fix to a runway threshold as a sequence of segments. TTG is computed at runtime from segment geometry, aircraft approach speed, and upper wind.

Trajectories can be generated using the `maestro-tools extract-stars` command and edited manually to add pressure and max-pressure segments. See [Maestro Tools](05-maestro-tools.md) for details.

```yaml
Trajectories:
  - FeederFix: MARGO
    RunwayIdentifier: '23'
    ApproachType: A
    TransitionFix: BUGSU
    Segments:
      - {Identifier: BUGSU, Track: 143.3, DistanceNM: 28.7}
      - {Identifier: GLOBE, Track: 90, DistanceNM: 8.8}
      - {Identifier: ANPUT, Track: 57, DistanceNM: 7.7}
      - {Identifier: VIRAT, Track: 140.5, DistanceNM: 5}
      - {Identifier: '23', Track: 229.9, DistanceNM: 14}
```

#### Trajectory Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `FeederFix` | string | Yes | Feeder fix identifier |
| `RunwayIdentifier` | string | Yes | Destination runway |
| `ApproachType` | string | No | Restricts to a specific approach type (e.g., `I`, `V`) |
| `TransitionFix` | string | No | Restricts to routes passing through this fix (e.g. a common point on a STAR with transitions via the feeder fixes) |
| `Segments` | array | Yes | Ordered segments from feeder fix to runway threshold |
| `PressureSegments` | array | No | Segments representing a small delay-absorption extension |
| `MaxPressureSegments` | array | No | Segments representing the maximum delay-absorption capacity |

#### Segment Properties

Each segment in `Segments`, `PressureSegments`, and `MaxPressureSegments` has the following properties:

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Identifier` | string | No | Fix name at which this segment terminates (informational only) |
| `Track` | number | Yes | True bearing in degrees |
| `DistanceNM` | number | Yes | Segment length in nautical miles |

#### Pressure and Maximum Pressure

`PressureSegments` represent a small path extension ATC can use to absorb minor delays, such as extending the downwind leg. The time to fly these segments is added to TTG to produce P.

`MaxPressureSegments` represent the maximum delay that can be absorbed through vectoring or speed control within the TMA. This may include extended off-STAR routing or similar. The time to fly these segments is added to P to produce Pmax.

Both lists are optional. A trajectory with no pressure segments has P = Pmax = TTG.

```yaml
Trajectories:

  # RIVET -> 34L
  # Illustrates branching pressure trajectories
  - FeederFix: RIVET
    RunwayIdentifier: 34L
    Segments:
    - {Identifier: BIGEM, Track: 61.6, DistanceNM: 12.6}
    - {Identifier: TAMMI, Track: 61.5, DistanceNM: 9.9}
    - {Identifier: BOOGI, Track: 61.5, DistanceNM: 10}
    - {Identifier: DUDOK, Track: 133.6, DistanceNM: 5}
    - {Identifier: NASHO, Track: 167.9, DistanceNM: 7.1}
    - {Identifier: BASE, Track: 065.0, DistanceNM: 6.5}
    - {Identifier: FINAL, Track: 335.2, DistanceNM: 12.8}

    # Pressure trajectory: extends downwind after the NASHO segment from the normal trajectory
    Pressure:
      After: NASHO
      Segments: 
      - {Identifier: DOWNWIND, Track: 167.9, DistanceNM: 3.0}
      - {Identifier: BASE, Track: 065.0, DistanceNM: 6.5}
      - {Identifier: FINAL, Track: 335.2, DistanceNM: 15.8}
      
    # Max pressure trajectory: further extends downwind after NASHO
    MaxPressure:
      After: NASHO
      Segments:
      - {Identifier: DOWNWIND, Track: 167.9, DistanceNM: 6.0}
      - {Identifier: BASE, Track: 065.0, DistanceNM: 6.5}
      - {Identifier: FINAL, Track: 335.2, DistanceNM: 18.8}
```

##### Pressure and Maximum Pressure Properties

Each segment in `Segments`, `PressureSegments`, and `MaxPressureSegments` has the following properties:

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `After` | string | Yes | The name of the segment in the normal trajectory where this trajectory extends from |
| `Segments` | array | Yes | Ordered segments from the branching point to runway threshold |

### Enroute Trajectories

Enroute trajectories define how much delay can be absorbed in the enroute phase for flights arriving via each feeder fix. When a flight's route passes through a matching entry point, that trajectories values are used. Flights with no matching entry use `DefaultMaxEnrouteLinearDelayMinutes`.

```yaml
EnrouteTrajectories:
  - EntryPoint: VELGI
    FeederFix: RIVET
    MaxEnrouteLinearDelayMinutes: 8
    ShortcutTimeToGainMinutes: 3
  - EntryPoint: NONUP
    FeederFix: RIVET
    MaxEnrouteLinearDelayMinutes: 6
```

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `EntryPoint` | string | Yes | Waypoint along the flight plan route where the enroute phase begins |
| `FeederFix` | string | Yes | Feeder fix this entry applies to |
| `MaxEnrouteLinearDelayMinutes` | integer | Yes | Maximum delay absorbable in the enroute phase via speed reduction or holding |
| `ShortcutTimeToGainMinutes` | integer | No | Time that can be gained by flying a direct routing through the enroute area. Default: `0` |

### Departure Airports

```yaml
DepartureAirports:
  - {Identifier: YPMQ, Aircraft: [JET], Distance: 209, EstimatedFlightTimeMinutes: 44}
  - {Identifier: YPMQ, Aircraft: [NONJET], Distance: 209, EstimatedFlightTimeMinutes: 41}
```

| Property | Type | Description |
|----------|------|-------------|
| `Identifier` | string | ICAO code |
| `Aircraft` | array | Aircraft types/categories |
| `Distance` | number | Distance in nautical miles |
| `EstimatedFlightTimeMinutes` | integer | Flight time to the managed airport |

### Views

Define how the sequence is displayed.

```yaml
Views:
  - Identifier: GUN/BIK
    LabelLayout: Enroute
    TimeWindowMinutes: 30
    Reference: FeederFixTime
    Direction: Down
    Ladders:
      - FeederFixes: [RIVET]
        Runways: []
      - FeederFixes: [WELSH]

  - Identifier: RWY
    LabelLayout: TMA
    TimeWindowMinutes: 45
    Reference: LandingTime
    Ladders:
      - Runways: [34L, 16R, "07"]
      - Runways: [34R, 16L, "25"]
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Identifier` | string | - | View name |
| `LabelLayout` | string | - | Label layout identifier |
| `TimeWindowMinutes` | integer | - | Time range to display |
| `Reference` | string | - | `FeederFixTime` or `LandingTime` |
| `Direction` | string | `Down` | `Up` or `Down` |
| `Ladders` | array | - | Ladder configurations |

#### Ladder Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `FeederFixes` | array | [] | Filter by feeder fixes |
| `Runways` | array | [] | Filter by runways |

### Coordination Messages

```yaml
GlobalCoordinationMessages:
  - WX Dev have commenced.
  - Standby for configuration change.
  - Flow Planning in progress - do not use delay times until advised.

FlightCoordinationMessages:
  - '{Callsign} MEDEVAC'
  - '{Callsign} diverting'
  - '{Callsign} request high speed descent'
```

Flight messages support `{Callsign}` placeholder.

## Aircraft Performance Configuration

vMaestro need to know the average descent speed of each aircraft in order to calculate it's trajectory.

The performance data can be extracted from the vatSys `Performance.xml` file using the [Maestro Tools CLI](./05-maestro-tools.md).

```yaml
AircraftPerformance:
  - {TypeCode: B738, DescentSpeedKnots: 220, IsJet: true,  WakeCategory: Medium}
  - {TypeCode: A320, DescentSpeedKnots: 210, IsJet: true,  WakeCategory: Medium}
  - {TypeCode: B77W, DescentSpeedKnots: 250, IsJet: true,  WakeCategory: Heavy}
  - {TypeCode: A388, DescentSpeedKnots: 260, IsJet: true,  WakeCategory: Super}
  - {TypeCode: DH8D, DescentSpeedKnots: 160, IsJet: false, WakeCategory: Medium}
  - {TypeCode: PC12, DescentSpeedKnots: 120, IsJet: false, WakeCategory: Light}
```

### Aircraft Performance Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `TypeCode` | string | Yes | The ICAO type code for the aircraft |
| `DescentSpeedKnots` | string | Yes | The average true airspeed at which the aircraft will descend through the TMA |
| `IsJet` | bool | Yes | Whether or not the aircraft is a Jet |
| `WakeCategory` | string | Yes | The wake turbulence category, either `Light`, `Medium`, `Heavy`, or `SuperHeavy` |

## Validation

vMaestro validates configuration on startup:
- Required fields are present
- Runway identifiers match airport runways
- Feeder fixes match airport feeder fixes
- Referenced label layouts exist
- Aircraft descriptors are valid

Check log files for error details if configuration fails to load.

## Example Configuration

See `Maestro.example.yaml` for a complete annotated example.
