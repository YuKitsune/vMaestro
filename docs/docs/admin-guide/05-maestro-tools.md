---
sidebar_position: 5
---

# Maestro Tools

`Maestro.Tools` is a command-line utility for generating trajectory configuration from vatSys data files. It is intended for AIS teams preparing or updating trajectory data.

## Installation

Download the latest release from the vMaestro GitHub releases page. The tool is a self-contained, command-line executable and requires no additional runtime.

## Commands

### `extract-stars`

Extracts STAR geometry from a vatSys `Airspace.xml` file and outputs trajectory YAML ready for use in `Maestro.yaml`.

```
maestro-tools extract-stars --airspace <path> --config <path>
```

| Option | Required | Description |
|--------|----------|-------------|
| `--airspace` | Yes | Path to the vatSys `Airspace.xml` file |
| `--config` | Yes | Path to the `maestro-tools.yaml` configuration file |

For each airport in the config, the command:

1. Reads all STARs for that airport from `Airspace.xml`
2. Resolves fix coordinates from the intersections section
3. Computes the true bearing and distance for each consecutive fix pair
4. Computes a final segment from the last STAR fix to the runway threshold
5. Writes a trajectory YAML file to the configured output path

The output file contains one trajectory entry per (FeederFix, RunwayIdentifier, ApproachType, TransitionFix) combination found in the STARs, filtered to the configured feeder fixes.

:::info
The tool extracts geometry as defined in vatSys. For airports where operational routing differs from published STARs (vectoring areas, early transitions, etc.), AIS teams should manually edit the output YAML files. `PressureSegments` and `MaxPressureSegments` can be defined in `maestro-tools.yaml` or added directly to output files.
:::

## Configuration File

The tool is configured via a YAML file (conventionally named `maestro-tools.yaml`). It supports multiple airports in a single run.

```yaml
Airports:
  - ICAO: YSSY
    FeederFixes: [RIVET, WELSH, BOREE, YAKKA, MARLN]
    Output: trajectories/yssy.yaml
    PressureSegments:

      # Add a 3nm downwind extension for Pressure
      - FeederFix: RIVET
        RunwayIdentifier: 34L
        Segments:
          - {Identifier: P_DOWNWIND, Track: 155, DistanceNM: 3.0}
          # The base leg is already modelled in the normal trajectory, so we don't need to account for it here, since we're only extending the downwind leg
          # But we do have to extend the final leg by the same distance to compensate
          - {Identifier: P_FINAL, Track: 335, DistanceNM: 3.0}

    MaxPressureSegments:

      # An additional 2nm downdind extension is our Maximum Pressure
      # So we can extend the downwind leg by 5 nm in total before linear delay can no longer be absorbed in the TMA, and it must be absorbed in enroute
      - FeederFix: RIVET
        RunwayIdentifier: 34L
        Segments:
          - {Identifier: DOGLEG, Track: 090.0, DistanceNM: 2}
          - {Identifier: REJOIN, Track: 270.0, DistanceNM: 2}
```

### Airport Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `ICAO` | string | Yes | Airport ICAO code |
| `FeederFixes` | array | No | Only emit trajectories for these feeder fixes. If empty, all feeder fixes found in the STARs are included. |
| `Output` | string | Yes | Output file path for the generated YAML |
| `PressureSegments` | array | No | Pressure segments to attach to matching trajectories |
| `MaxPressureSegments` | array | No | Max-pressure segments to attach to matching trajectories |

### Segment Override Matching

`PressureSegments` and `MaxPressureSegments` each contain a list of overrides. An override matches a trajectory when all specified fields match:

| Field | Required | Description |
|-------|----------|-------------|
| `FeederFix` | Yes | Must match the trajectory feeder fix |
| `RunwayIdentifier` | Yes | Must match the trajectory runway |
| `TransitionFix` | No | If set, only matches trajectories with this transition fix |
| `ApproachType` | No | If set, only matches trajectories with this approach type |

Omitting `TransitionFix` or `ApproachType` matches all trajectories for the given feeder fix and runway regardless of those values.

## Workflow

A typical workflow for preparing trajectory data:

1. Obtain the vatSys `Airspace.xml` for the relevant dataset.
2. Create a `maestro-tools.yaml` with the airports and feeder fixes.
3. Run `extract-stars` to generate the trajectory YAML files.
4. Review and edit the output files:
   - Modify segments where operational routing differs from published STARs
   - Remove unnecessary segments (e.g., where vectoring begins early)
   - Add custom segments for vectoring areas not in vatSys
   - Define `PressureSegments` and `MaxPressureSegments` (in config or output files)
5. Copy the edited trajectories into the `Trajectories` section of `Maestro.yaml`.

When STARs are updated in the vatSys dataset, re-running `extract-stars` regenerates the segment geometry automatically. Pressure and max-pressure overrides defined in `maestro-tools.yaml` are preserved across regenerations, but manual trajectory edits in output files will be overwritten.
