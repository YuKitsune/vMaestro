> *Warning*
> This project is under active development and is not ready for active use on the VATSIM network.


# Installation

The project files are configured to output the build artifacts to the Australian vatSys profile.
Building the project should automatically install the plugin for you.

If you have a large screen resolution, you may need to run the `dpiaware-fix.bat` file to disable DPI awareness for vatSys.

# Known Bugs 

After a flight has been stablised, the estimates move forwards causing the flight to show a negative delay. Recompute doesn't appear to fix it.

Switching from one view to another can sometimes cause vatSys to crash.

The Debug window always moves to the back of the stack when moving your mouse cursor over a vatSys window.

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
    
- [ ] Offline Mode (MVP)
    - [X] Implement domain models, handlers, tiny types, etc.
    - [X] Publish notifications on FDP updates
    - [X] Information window
    - [X] Basic sequencing algorithm
    - [X] Automatic runway assignment
    - [ ] Recompute
    - [ ] Remove from sequence
    - [ ] Desequence
    - [ ] Change ETA FF
    - [ ] Change runway mode
    - [ ] Change runway rates
    - [ ] Insert flight

- [ ] Internal System Tasks
    - [ ] Remove flights after landing (STA + configurable time)
    - [ ] Remove flights after disconnecting

- [ ] Sequencing Algorithm Refinement
    - [ ] Factor GRIB winds into estimate calculations
    - [ ] Use FF and STAR suffix for ETI
    - [ ] Separate enroute and TMA delays
    - [ ] Consider separation at the feeder fix
    - [ ] Consider TMA delay modes (Normal, pressure, and max delay approach)
    - [ ] Apply optimisations (i.e. runway selection)

- [ ] Online Mode
    - [ ] Run sequencing code in a standalone server
    - [ ] Connect to Maestro server via WebSocket
    - [ ] Source airport configuration from server
    - [ ] Redirect notifications and requests to server
    - [ ] Source sequence information from server
    - [ ] Allow Flow to modify the sequence
    - [ ] Authentication

- [ ] Extras
    - [ ] Ladder scrolling
    - [ ] Account for GRIB winds
    - [ ] Blockout periods
    - [ ] Insert slot
    - [ ] Zero Delay and priority flights
    - [ ] Departure list
    - [ ] Pending flights
    - [ ] Unit selector
    - [ ] Coordination
    - [ ] Revisit strong ID types

- [ ] Fault Tolerance
    - [ ] Ensure exceptions are contained and recovered from. Do not crash vatSys.
    - [ ] Persist offline sequences to sqlite in case of a restart.

- [ ] Look and feel improvements
    - [ ] Refine border sizes and margins
    - [ ] Check font sizing
    - [ ] Size elements based on font width and height
    - [ ] Check colors

- [ ] Nice to haves
    - [ ] Custom debugger configuration (start and attach to vatSys)
    - [ ] Arrival list / GlobalOps backup
    - [ ] Per-sequence online mode (E.g: YSSY offline, YMML online)
