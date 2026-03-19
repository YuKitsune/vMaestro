---
sidebar_position: 1
---

# Interface

This page describes the Maestro user interface.

## Starting Maestro

1. Click the `TFMS` button on the vatSys menu bar
2. Select an airport from the menu

![Screenshot of a blank Maestro window](../../../static/img/maestro_blank.png)

:::tip
If the `TFMS` menu item does not appear, refer to the [installation instructions](../../admin-guide/01-plugin-installation.md).
:::

## Window Layout

The Maestro window is divided into two sections:

<!-- TODO: Combine screenshots, and remove references to "zones" -->
![Diagram of the Maestro window](../../../static/img/window_diagram.png)

The **upper section** displays:

- Online status indicator
- TMA configuration button
- Runway acceptance rates
- Online setup button

The **lower section** contains:

- Action buttons (`DEPS`, `COORD`, `DESQ`)
- View selector buttons
- Sequence ladders

![Diagram of the Sequence Display Zone](../../../static/img/sequence_display_zone_diagram.png)

## TMA Configuration

The upper section displays the current TMA configuration and runway acceptance rates. Click the TMA configuration button to change runway modes or adjust acceptance rates.

The online status indicator shows the connection state when operating in online mode.

See [TMA Configuration](./02-tma-configuration.md) for details on changing the configuration.

## Action Buttons

| Button | Purpose |
| ------ | ------- |
| `DEPS` | Open the pending departures list |
| `COORD` | Open the coordination window |
| `DESQ` | Open the desequenced flights list (turns white when flights exist) |

## Views

The view buttons switch between different displays of the sequence. Each view defines:

- One or more **ladders** with filters for specific runways or feeder fixes
- A **time reference** — either landing time (`STA`) or feeder fix time (`STA_FF`)
- A **label layout** and **colour scheme** for flight labels

Views using `STA` are often called **Runway views**, while views using `STA_FF` are called **Feeder views**.

### Ladders

Ladders are vertical timelines displaying flights in the sequence. Each tick represents one minute. Flights are positioned on the ladder based on the view's time reference.

Buttons to the left of the ladders control scrolling:

- **Up/Down Arrows** - Scroll 15 minutes
- **Center Button** - Reset to current time

When scrolled, the time reference at the bottom turns blue.

When more than two ladders are present, additional buttons become available for horizontal scrolling.

### Flight Labels

Flight labels are mirrored on each side of the ladder.

<!-- TODO: Include screenshot with mirrored labels -->
![Image of a flight label](../../../static/img/flight_label.png)

Labels may include:

- Aircraft callsign
- Aircraft type code
- Wake turbulence category
- Assigned runway
- Assigned approach type
- Landing Time (STA)
- Feeder Fix Time (STA_FF)
- Total delay assigned
- Remaining delay to be absorbed
- Manual Delay Indicator
- High Speed Indicator
- Coupling Status Indicator

Right-click a flight label to access actions like changing runway, adjusting delay, or removing the flight. See [Flight Management](./03-flight-management.md) for details on each action.
