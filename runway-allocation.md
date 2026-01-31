
## Summary

### Runway Allocation

Runways must be allocated based on runway allocation rules defined in the runway mode.

Runway allocation rules will consider the feeder fix and the aircraft type and/or wake category when assigning a runway.
Feeder fixes and aircraft types can be assigned to multiple runways, in which case the runway which yields the earliest STA will be assigned.

Example:

```json
{
    "AircraftType": "A388",
    "Runways": ["34L"] // Always assign A388 to 34L
}

{
    "FeederFix": "MARLN",
    "AircraftWake": "Heavy",
    "Runways": ["34L"] // 34L should always be allocated heavies arriving via MARLN
}

{
    "FeederFix": "MARLN",
    "Runways": ["34R", "34L"] // All other aircraft via MARLN can be assigned to either 34R or 34L, but 34R is preferred
}
```

When a flight is created, and it is not tracking via a feeder fix, it should be assigned the first runway defined in the current runway mode.

When sequencing a flight, the runway will be allocated based on the runway allocation rules in the current runway mode.
If more than one runway is available based on the allocation rules, runway assignment is postponed until the scheduling phase.

During the scheduling phase, if the aircraft has not been allocated a runway during the sequencing phase, the STA on each possible runway will be computed, and the runway which yields the earliest STA will be allocated.

The runway is not re-allocated for stable flights unless the Recompute function is used.

### Approach Allocation

Each runway in a runway mode must define a default approach type.

Any time a runway is allocated to a flight, the approach type must also be allocated.

The allocated approach type will be the default one for the allocated runway in the current runway mode.
If a matching arrival configuration cannot be found, the default TTG for the airport configuration shall be used.

A default TTG should be added to the airport configuration in the event that a matching arrival configuration based on the search critaria cannot be found.

Controllers may still override the allocated approach type if desired.

### Runway Separation

The runway mode will allow for multiple arrival runways to be configured.
Each runway in the runway mode will define an accpetance rate.

Additionally, a new dependency rate can be added to the runway mode to define the required separation between multiple runways.
If the dependency rate is omitted, no additional separation is applied from flights landing on the other runways.
If the dependency rate is specified, flights landing on runway I must be separated by the dependency rate from flights landing on runway J.

## Checklist

- [X] Introduce new runway assignment rules
- [X] Introduce a service to fetch the available runways

- [X] Make assigned runway nullable
- [ ] ~~Move runway assignment into a separate service~~
    - [ ] Assign runway and approach type in one go
    - [ ] Use default TTG if none can be found based on search criteria

- [X] Get available runways before scheduling
    - [X] Don't re-assign runway for stable flights
    - [ ] Re-run the scheduling if multiple runways are available
    - [ ] Assign to the runway/approach type with the earliest ETA

- [ ] When a flight is created and no FF is found, assign the default runway (first on the config)
- [ ] Clear assigned runway on recompute

- [X] Introduce dependency rate
    - [X] Apply dependency rate to aircraft landing on other runways
