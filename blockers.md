
# Blockers

- Goal: Add trajectory data to flight model
    - Requirement: Trajectory should be required
        - Blocker: Flight.Reset() method clears everything.
            - Need a different way to model Recompute, Remove, Desequence, and MakePending
                - Need to store the vatSys provided details somewhere in-memory
                - Need to store Pending flights as a separate type
                - Need to store Removed flights as a separate type
                - Need to clean up Pending and Removed flights when vatsim pilots disconnect
                - Need to model Desequenced flights as a separate state
                    - Need to make Sequence cope with desequenced flights
