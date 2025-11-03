# System Operation

## Starting Maestro

Maestro can be started by clicking the `TFMS` button on the vatSys menu bar.

TODO.

:::tip
If you do not see the `TFMS` menu item, refer to the [installation instructions](./01-installation.md).
:::

## User Interface

The Maestro window is divided into two sections.

The **Configuration Zone** provides access to:

- Online status (see [Online Operation](#online-operation))
- TMA configuration
- Runways in use and relevent acceptance rates
<!-- - Wind speed and direction (6,000 ft and surface level winds) -->
<!-- - Achieved landing rates -->
<!-- - Units selector -->
<!-- - UTC time -->
- Online setup

The **Sequence Display Zone** provides access to:

- Buttons for interacting with the sequence
- View selection buttons
- Sequence ladders

### Configuration Zone

#### Online Status Indicator

The Online Status Indicator displays the status of the connection to the Maestro server.

| Status | Meaning |
| ------ | ------- |
| `OFFLINE` | **The sequence is not synchronused with other clients.** All processing is done locally, and all functions are available. |
| `READY` | A connection to the Maestro server has been established, but no data is synchronised. This will appear when connecting to the server before connecting to the VATSIM network. |
| `FMP` | Connected to the server with the `Flow` role. All processing is done locally and the sequence is shared to all other connected clients. |
| `APP` | Connected to the server with the `Approach` role. Access to certain functions may be restricted. |
| `ENR` | Connected to the server with the `Enroute` role. Access to certain functions may be restricted. |
| `ENR/FMP` or `APP/FMP` | Connected to the server with the `Enroute` or `Approach` role, but there is no dedicated `FMP` controller online. All functions are available. |
| `OBS` | Connected to the server with the `Observer` role. The sequence is read-only, and all modifications are disallowed. |

#### TMA Configuration

The TMA Configuration button will display the currently selected TMA configuration.

<!-- TODO: TMA Configuration Button Screenshot -->

When a configuration change is scheduled to occur in the future, the text of the button will turn white, and the button will read `[Current Configuration] → [New Configuration]`.

<!-- TODO: TMA Configuration Button with future configuration screenshot -->

#### Runway Acceptance Rates

The Landing Rates button displays each active runway along with the specified acceptance rates.

<!-- TODO: Runway Acceptance Rates Button Screenshot -->

<!-- #### Winds -->

<!-- #### Achieved Landing Rates -->

<!-- #### Units Selector -->

#### Online Setup

The `SETUP` button opens the `Online Setup` window, where connection details can be provided for connecting to the Maestro server.

### Sequence Display Zone

The top of the Sequence Display Zone contains various buttons with the following purposes:

| Button | Purpose |
| ------ | ------- |
| `DEPS` | Opens the `Insert a Flight` window, allowing pending flights from departure airports to be manually inserted into the sequeuce. |
| `COORD` | Opens the `Coordination` window allowing pre-defined messages to be sent to other controllers. |
| `DESQ` | Opens the `Desequenced` window, showing a list of flights that have been de-sequenced. The text of this button will become white when at least one flight exists in the list. |
| Views | The remaining buttons correspond to pre-defined views that control what is displayed in the lower part of the Sequence Display Zone. |

The lower part of the Sequence Display Zone contains one or more timelines as defined by the selected view.

There are three buttons to the left of the timelines allowing the timelines to be scrolled up or down in 15 minute increments. The center button will reset to the current time. If the timelines are scrolled, the axis reference time at the bottom <!-- (or top) --> of the timeline will turn white.

#### View Configuration

Each view can be configured as follows:

- Time horizon: The duration of the sequence displayed on the screen.
- Number of timelines: Up to 4 timelines can be displayed.
- Timeline direction: Timelines may move up or down the screen.
- Timeline reference: Flights can be displayed based on their `STA` (Runway view) or their `STA_FF` (Feeder view).
- Runway and Feeder Fix filters: Each timeline can be filtered to display flights depending on their assigned runway or Feeder Fix.

The content of flight labels can also be configured to display the following information:

- Callsign
- Aircraft type
- Aircraft weight class
- Allocated runway
- Approach type
- `STA` either in absolute (e.g. 12:15) or relative format (e.g. landing in 25 minutes)
- `STA_FF` either in absolute (e.g. 12:15) or relative format (e.g. transfer in 5 minutes)
- Total delay (The total amount of delay the flight needs to absorb to land at the calculated `STA`)
- Current delay (The delay yet to be absorbed)
- Coupling status indicator
- Manual delay indicator
- Control action indicator

Each of the flight label fields can be color coded depending on:

- Destination
- Allocated runway
- Approach type
- Feeder Fix
- Maestro State
- Runway configuration (i.e. whether the flight has been processed with the current or future runway configuration)

<!-- TODO: Include a labelled diagram of the timeline -->

## Modifying the TMA Configuration

<!-- TODO: Document TMA configuration and rates change -->

## Inserting Flights

<!-- TODO: Insert from departure  -->
<!-- TODO: Insert dummy flights -->
<!-- TODO: Insert overshoot flights -->

## Modifying Flights

<!-- TODO: Document each function in the flight label contex menu -->

### View Flight Information

### Re-compute

### Change Runway

### Change `ETA_FF`

### Remove

### De-sequence

### Manual Delay

## Slots

<!-- TODO: Creating, modifying, and deleting slots -->

## Coordination

<!-- TODO: Coordination docs -->

## Online Operation

<!-- TODO: Connect to a server, defer to separate docs -->

---

## Interactions

### Change TMA Configuration

To change the TMA Configuration, click the TMA Configuration button in the Configuration Zone. This will open the `TMA Configuration` Window.
Here, a pre-defined TMA configuration can be selected, or the current configuration can be modified.
Runway acceptance rates can be changed by adjusting the sliders for each runway.

The validity period of the configuration can be adjusted using the `Last STA in configuration ...` and `First STA in configuration ...` times.
The single-arrow buttons change the time in 1 minute increments, and the two-arrow buttons in 5 minute increments.

Flights scheduled to land after the `First STA in configuration ...` time will be processed using the new configuration.

When a gap exists between the `Last STA ...` and `First STA ...` times, no flights may land during that period of time. Any flight with an estimate within this gap will be delayed until after the `First STA ...` time.

<!-- TODO: TMA Configuration Window Screenshot -->

<!-- ### Change Acceptance Rates -->

<!-- To change the acceptance rates, click the Runway Acceptance Rates button in the Configuration Zone. This will open the `Runway Acceptance Rates` window.
Here, the acceptance rates can be changed by adjusting the sliders for each runway.

The validity period of the rates change can be adjusted using the `Change rates at` time.
The single-arrow buttons change the time in 1 minute increments, and the two-arrow buttons in 5 minute increments.

Flights scheduled to land after the `Change rates at` time will be processed using the new acceptance rates. -->

<!-- TODO: Runway Acceptance Rates Window Screenshot -->

<!-- ### Change displayed units -->

### Insert a Flight from a Departure Airport

To insert a flight from a departure airport, click the `DEPS` button in the Sequence Display Zone. This will open the `Insert a Flight` window.

Select the flight from the Pending list, and adjust the Take-Off time as required.

When the flight is inserted, it will become `Stable`.
The `ETA` will be set to the Take-Off time selected plus the `EET` specified in the flight plan.

Once the flight has departed and become coupled, the `ETA_FF` and `ETA` will be updated based on the estimates provided by vatSys.

<!-- TODO: Screenshot of Insert A Flight window -->

### Send Coordination Messages

Coordination messages can be sent to other controllers in one of two ways.

#### General Coordination Messages

General coordination messages can be sent by clicking the `COORD` button in the Sequence Display Zone. This will open the `Coordination` window with a list of general coordination messages.

<!-- TODO: -->

### Re-Sequence a De-sequenced Flight

To re-insert a de-sequenced flight, click the `DESQ` button in the Sequence Display Zone to open the `Desequenced Window`. Select the flight to re-sequence, then click `RESEQUENCE`.

Re-sequenced flights are placed in the sequence at a position based on their last `ETA_FF` received from vatSys. The rest of the sequence will be re-computed. Re-sequenced flights become `Stable` immediately.

<!-- TODO: Screenshot of Desequenced window -->

###

### Insert a Flight on the timeline


TODO:
