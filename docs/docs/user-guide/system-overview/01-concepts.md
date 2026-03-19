---
sidebar_position: 1
---

# Concepts

This page covers the terminology and abbreviations used throughout vMaestro.

## Terminology

| Term | Meaning |
| ---- | ------- |
| Feeder Fix (FF) | A point on the TMA boundary where arriving aircraft enter terminal airspace. |
| Managed Airport | The airport vMaestro is sequencing arrivals for. |
| Departure Airport | An airport typically within 30-minute flight time of the managed airport. Flights from these airports must be manually inserted into the sequence. |
| Close Airport | An airport within close proximity to the managed airport, typically within the TMA. |
| Acceptance Rate | The minimum time separation between successive landings on a runway. |
| Runway Mode | A predefined TMA configuration specifying which runways are active and their acceptance rates. |

## Time Abbreviations

| Abbreviation | Meaning |
| ------------ | ------- |
| `ETA` | Estimated Time of Arrival at the **runway** (landing time). |
| `STA` | **Scheduled** Time of Arrival at the runway, calculated by vMaestro. |
| `ETA_FF` | Estimated Time of Arrival at the **feeder fix**. |
| `STA_FF` | **Scheduled** Time of Arrival at the feeder fix, calculated by vMaestro. |
| `ATO` | Actual Time Over the feeder fix (recorded once the aircraft passes). |
| `ETI` | Estimated Time Interval from feeder fix to runway (trajectory time). |

## Delay Values

vMaestro displays two delay values on flight labels:

- **Required Delay** - The total delay vMaestro has assigned to achieve the scheduled landing time. This value remains fixed once calculated.
- **Remaining Delay** - The delay still to be absorbed. This decreases as the flight slows down or is vectored, eventually reaching zero when all delay has been absorbed.
