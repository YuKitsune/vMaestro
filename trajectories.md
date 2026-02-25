## Trajectories

### Overview

In MAESTRO, a trajectory is a pre-calculated set of times from a feeder fix to a runway, via a specific approach type, for a specific aircraft type or category.

Trajectories are pre-calculated, and stored in the `Maestro.json` file within the airport configuration.

Trajectories are currently modelled as `ArrivalConfiguration` in the code.

### Current State

The TTG is calculated ad-hoc using the `IArrivalLookup` whenever it is required.

The TTG is required when:
- receiving flight updates from vatSys, and calculating the ETA using ETA_FF + TTG (via `EstimateProvider`)
- recomputing flights via the `RecomputeRequestHandler`, and calculating the ETA using ETA_FF + TTG (via `EstimateProvider`)
- changing the `ETA_FF` manually via the `ChangeFeederFixEstimateRequestHandler`, and calculating the ETA using ETA_FF + TTG (via `EstimateProvider`)
- changing the approach type via the `ChangeApproachTypeRequestHandler`, and calculating the ETA using ETA_FF + TTG
- inserting a flight via the `InsertFlightRequestHandler`, and calculating the ETA using ETA_FF + TTG
- assiging the STA_FF from the `Sequence` using STA - TTG

Each time we need the TTG, we look it up based on parameters contained within the Flight.

### Future State

The airport configuration should provide a look-up table, where trajectories can be looked up based on:
- Feeder Fix
- Approach Type
- Runway
- Aircraft Category **OR** Type (one category, AND/OR multiple types can be defined per trajectory)

Terminal Trajectory data should contain:
- Distance (reserved for future use)
- Time To Go (as described above)
- Pressure (reserved for future use)
- Max Pressure (reserved for future use)

When a flight is to be inserted into the sequence from the `FlightUpdatedHandler`:
- and the flight has a valid feeder fix 
    - The first runway and approach type defined in the runway mode at the ETA of last fix are assigned
    - The trajectory is looked up based on the flights details, and runway+approach type we just assigned
    - The trajectory data is stored within the `Flight` model for future reference
- if the flight does not have a valid feeder fix
    - It is added to the pending list to be manually inserted later

When a flight is manually inserted into the sequence from the `InsertFlightRequestHandler`:
- and the flight is from the pending list
    - and the flight has a valid feeder fix
        - the trajectory should be looked up as though the flight were inserted via the `FlightUpdatedHandler`
    - and the flight does not have a valid feeder fix
        - an average trajectory will be calculated and assigned to the flight
        - the ETA_FF will be calculated as the ETA of the last waypoint in the flight plan route - the TTG from the average trajectory
        - this ETA_FF will need to be re-calculated as above as updates are received from vatSys via the `FlightUpdatedHandler`
- and the flight is not from the pending list (dummy flight)
    - an average trajectory will be calculated and assigned to the flight
    - the ETA_FF will be calculated as the target time as dictated by the insertion method - the TTG from the average trajectory

When the runway or approach type allocated to a flight changes, the trajectory must change.
When the trajectory allocated to a flight changes, the ETA needs to be re-calculated (ETA_FF + TTG), and the STA_FF needs to be re-calculated (STA - TTG)
Trajectory changes must be atomic with the runway or approach type change.

The `FeederFixEstimate` for a flight will no longer be nullable, even if the flight does not have a feeder fix.
If the flight does not have a feeder fix, the FeederFixEstimate will be calculated based on the ETA - the average TTG.
If ETA - Avg(TTG) is earlier than the current time, then `ActualFeederFixTime` will be set when the flight update is received.
