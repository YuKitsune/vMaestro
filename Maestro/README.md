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

- [X] Ladder configuration
    - [X] TMA view
    - [X] ENR view

- [ ] Offline Mode
    - [X] Implement domain models, handlers, tiny types, etc.
    - [X] Publish notifications on FDP updates
    - [ ] Information window
    - [ ] Basic sequencing algorithm
    - [ ] Change runway mode
    - [ ] Change runway
    - [ ] Blockout period
    - [ ] Insert flight
    - [ ] Insert slot
    - [ ] Change ETA FF
    - [ ] Zero Delay
    - [ ] Remove from sequence
    - [ ] Desequence

- [ ] Extras
    - [ ] Revisit strong ID types
    - [ ] Ladder scrolling
    - [ ] Departure list
    - [ ] Pending flights
    - [ ] Account for GRIB winds
    - [ ] Unit selector
    - [ ] Stagger rate
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
    - [ ] Font-based sizing
    - [ ] Double check colors

- [ ] MVP
    - [ ] Fix DPI scaling issue

- [ ] Nice to haves
    - [ ] Debugger configuration for vatSys
    - [ ] Arrival list / GlobalOps backup
    - [ ] CSV or XLSX backup
    - [ ] Sweatbox support
    - [ ] Customisable UI layout
    - [ ] Per-sequence online mode (E.g: YSSY offline, YMML online)

Need to figure out:
- When is the initial FF and landing time set? Is it ever reset?
- Does the ladder switch from using the estimated times to scheduled times?
    - Are the scheduled times set from the beginning and constantly re-calculated?
    