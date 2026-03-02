
# Blockers

- Goal: Add trajectory data to flight model
    - Requirement: Trajectory should be required
        - Blocker: Flight.Reset() method clears everything.
        - Solution: Keep the trajectory in place, and re-calculate on re-insertion
