# Roadmap

- [X] Eurocat Look and Feel
    - [X] Pull theme from vatSys
    - [X] Create custom controls to follow vatSys theme
    - [X] Custom buttons
    - [X] Custom separators
    - [X] Clean up timeline
    - [X] Clean up combobox
    - [X] Fix sector selector
    - [X] Move inline styles into Theme and Style.xaml

- [X] Configuration
    - [X] Load configuration from profile
    - [X] Separate configuration access for offline and online modes

- [ ] Temporary feed
    - [ ] Load data from existing Maestro API
    - [ ] Publish estimates to existing Maestro API

- [X] Ladder configuration
    - [X] TMA view
    - [X] ENR view

- [ ] Offline Mode (MVP)
    - [X] Implement domain models, handlers, tiny types, etc.
    - [X] Publish notifications on FDP updates
    - [X] Information window
    - [X] Basic sequencing algorithm
    - [ ] Change runway mode
    - [ ] Change runway rates
    - [ ] Change ETA FF
    - [ ] Insert flight
    - [ ] Remove from sequence
    - [ ] Desequence
    - [ ] Recompute
    - [ ] Automatic runway assignment
    - [ ] Persist to sqlite database on disk

- [ ] Extras
    - [ ] Ladder scrolling
    - [ ] Revisit strong ID types
    - [ ] Calculate STA and ETA based on STAR distance and ground speed
    - [ ] Account for GRIB winds
    - [ ] Blockout periods
    - [ ] Insert slot
    - [ ] Zero Delay and priority flights
    - [ ] Departure list
    - [ ] Pending flights
    - [ ] Unit selector
    - [ ] Coordination

- [ ] Online mode
    - [ ] Connect to Maestro server
    - [ ] Source configuration from server
    - [ ] Redirect update notifications to server
    - [ ] Source sequence information from server
    - [ ] Allow Flow to modify the sequence
    - [ ] Authentication

- [ ] Look and feel final pass
    - [ ] Check border sizes and margins
    - [ ] Check font sizing
    - [ ] Size elements based on font width and height
    - [ ] Double check colors

- [ ] Nice to haves
    - [ ] Debugger configuration for vatSys
    - [ ] Arrival list / GlobalOps backup
    - [ ] CSV or XLSX backup
    - [ ] Sweatbox support
    - [ ] Customisable UI layout
    - [ ] Per-sequence online mode (E.g: YSSY offline, YMML online)

# Questions

- When is the initial FF and landing time set? Is it ever reset?
- Does the ladder switch from using the estimated times to scheduled times?
    - Are the scheduled times set from the beginning and constantly re-calculated?
- Do flight labels overlap on the ladder IRL?
- Do we need to consider feeder fix intervals as well as landing rates?

# Current Bugs

List of bugs I've noticed in the sim. Need to build test cases to replicate the scenarios. 

- Flights in the sim sometimes speed up and throw off the calculations.
    - Idea: Should we re-calculate the delay if the flight speeds up and there are no flights in front?
