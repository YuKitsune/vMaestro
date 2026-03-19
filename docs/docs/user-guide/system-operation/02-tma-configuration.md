---
sidebar_position: 2
---

# TMA Configuration

This page covers how to view and modify the TMA configuration during operation.

## TMA Configuration Button

The TMA Configuration button in the Configuration Zone displays the current runway mode.

![Image of the TMA Configuration button](../../../static/img/tma_config_button.png)

When a configuration change is scheduled for the future, the button text turns white and shows:
`[Current Configuration] → [New Configuration] at [Change Time]`

<!-- TODO: Update screenshot with change time -->
![Image of the TMA Configuration button with a future configuration](../../../static/img/tma_config_change_button.png)

## Changing the Configuration

Click the TMA Configuration button to open the TMA Configuration window.

![Image of the TMA Configuration window](../../../static/img/tma_config.png)

From this window you can:

- Select a predefined runway mode
- Adjust acceptance rates for each runway using the sliders
- Schedule when the configuration change takes effect

### Scheduling Configuration Changes

The validity period is controlled by two times:

- **Last STA in configuration** - The last landing time using the current configuration
- **First STA in configuration** - The first landing time using the new configuration

Use the arrow buttons to adjust these times:

- Single arrows change by 1 minute
- Double arrows change by 5 minutes

:::info
Flights scheduled to land after the "First STA in configuration" time will be processed using the new configuration.
:::

:::info
When a gap exists between the two times, no flights may land during that period.
Any flight with an estimate within this gap will be delayed until after the new configuration begins.
:::

## Runway Acceptance Rates

The Runway Acceptance Rates button displays each active runway with its current acceptance rate.

![Image of the Runway Acceptance Rates button](../../../static/img/rates_button.png)

The acceptance rate is the minimum time separation between successive landings on that runway.
