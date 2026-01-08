## Checklist

- [ ] Review failing test cases
- [ ] Replace `ArrivalRegex` with `ApproachType`
- [ ] Add `TransitionFix` to arrival configuration
- [ ] Add `ApproachType` to `Flight`
- [ ] Add a `Change Approach Type` function
- [ ] Add test cases
- [ ] Merge
- [ ] Consider modelling trajectories

# Questions

If a runway change is scheduled for some time in the future, and a flight is delayed past the `LastLandingTimeInPreviousMode`, what happens?
Does the flight get moved to a runway in the new mode, or will it remain on its assigned runway?
Does the flight get scheduled between `LastLandingTimeInPreviousMode` and `FirstLandingTimeInNewMode`, or is it delayed until `FirstLandingTimeInNewMode`?
