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
4. Appends any user-defined segments from the config (additional, pressure, max-pressure)
5. Writes a trajectory YAML file to the configured output path

The output file contains one trajectory entry per (FeederFix, RunwayIdentifier, ApproachType, TransitionFix) combination found in the STARs, filtered to the configured feeder fixes.

:::info
Output files contain only `Segments` computed from the STAR geometry. `PressureSegments` and `MaxPressureSegments` must be defined manually in the `maestro-tools.yaml` config, or added directly to the output file after generation.
:::

## Configuration File

The tool is configured via a YAML file (conventionally named `maestro-tools.yaml`). It supports multiple airports in a single run.

```yaml
Airports:
  - ICAO: YSSY
    FeederFixes: [RIVET, WELSH, BOREE, YAKKA, MARLN]
    Output: trajectories/yssy.yaml
    AdditionalSegments:

      # These segments are appended to the RIVET/34L trajectory to model the base and final legs
      - FeederFix: RIVET
        RunwayIdentifier: 34L
        Segments:
          - {Identifier: BASE, Track: 065, DistanceNM: 4}
          - {Identifier: FINAL,  Track: 335.0, DistanceNM: 12}

    PressureSegments:

      # Add a 3nm downdind extension for Pressure
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
| `AdditionalSegments` | array | No | User-defined segments that replace the auto-computed runway segment for matching trajectories |
| `PressureSegments` | array | No | Pressure segments to attach to matching trajectories |
| `MaxPressureSegments` | array | No | Max-pressure segments to attach to matching trajectories |

### Segment Override Matching

`AdditionalSegments`, `PressureSegments`, and `MaxPressureSegments` each contain a list of overrides. An override matches a trajectory when all specified fields match:

| Field | Required | Description |
|-------|----------|-------------|
| `FeederFix` | Yes | Must match the trajectory feeder fix |
| `RunwayIdentifier` | Yes | Must match the trajectory runway |
| `TransitionFix` | No | If set, only matches trajectories with this transition fix |
| `ApproachType` | No | If set, only matches trajectories with this approach type |

Omitting `TransitionFix` or `ApproachType` matches all trajectories for the given feeder fix and runway regardless of those values.

### Additional Segments

When an `AdditionalSegments` override matches a trajectory, its segments replace the auto-computed segment from the last STAR fix to the runway threshold. This is used for airports where STARs terminate before the initial approach fix and ATC provides radar vectoring for the remainder (for example, at Sydney where STARs terminate before the IAF and the downwind, base, and final legs are not encoded in vatSys).

The additional segments should describe the complete path from the STAR terminus to the runway threshold.

## Workflow

A typical workflow for preparing trajectory data:

1. Obtain the vatSys `Airspace.xml` for the relevant dataset.
2. Create a `maestro-tools.yaml` with the airports, feeder fixes, and any known additional segments.
3. Run `extract-stars` to generate the trajectory YAML files.
4. Review the output and manually add `PressureSegments` and `MaxPressureSegments` as required, either in the config or directly in the output files.
5. Copy the output into the `Trajectories` section of `Maestro.yaml`.

When STARs are updated in the vatSys dataset, re-running `extract-stars` regenerates the segment geometry automatically. Any pressure or max-pressure overrides defined in `maestro-tools.yaml` are preserved across regenerations.
