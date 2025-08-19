<h1 align="center">
  vMaestro 
</h1>

<h3 align="center">
  A vatSys plugin emulating the Maestro Traffic Flow Management System.

  [![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/yukitsune/vmaestro/CI.yml?branch=main)](https://github.com/YuKitsune/vMaestro/actions/workflows/CI.yml)
  [![License](https://img.shields.io/github/license/YuKitsune/vMaestro)](https://github.com/YuKitsune/vMaestro/blob/main/LICENSE)
  [![Latest Release](https://img.shields.io/github/v/release/YuKitsune/vMaestro?include_prereleases)](https://github.com/YuKitsune/vMaestro/releases)

  <img src="./docs/README-screenshot.png" width="320" />
</h3>

> [!WARNING]
> This project is under active development and is not ready for active use on the VATSIM network.

# Installation

If you're building the project from source, the project files are configured to output the build artifacts to the Australian vatSys profile.
Building the project should automatically install the plugin for you.

Otherwise, place the plugin files into your vatSys plugins directory (`%documents%\vatSys Files\Profiles\Australia\Plugins\Maestro`).
You will also need to copy the `Maestro.json` file into your profile directory (`%documents%\vatSys Files\Profiles\Australia`).
If you have a large screen resolution, you may need to run the `dpiaware-fix.bat` file to disable DPI awareness for vatSys.

Logs will be written to the vatSys installation directory under `MaestroLogs`.

# Known Issues 

- NoDelay and ManualLandingTime flights are not separated from Stable flights
- When the Maestro.json file is missing, vatSys gets flooded with exceptions
- Desequence and Information windows do not re-draw
- The Debug window always moves to the back of the stack when moving your mouse cursor over a vatSys window.
- When a button is enabled, it's appearance is still disabled, but it is still interactable.
- The ladder occasionally stop updating.
- Desequence window can be re-opened multiple times.
- Diversions result in weird behaviour.
- Stable flights don't seem to get updated ETAs.
- Recompute, ETA_FF and STA_FF update, but STA doesn't.
- Resuming a desequenced flight does not work
- Q: Should times from vatSys be rounded to the nearest 30 secs?

# What's next?

- [ ] Revise roadmap, add and remove functionality as necessary
- [ ] Document the existing and outstanding functionality (what should it do)
- [ ] Fix known bugs
- [ ] Continue to MVP

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
    - [X] Recompute
    - [X] Remove from sequence
    - [X] Desequence
    - [X] Change runway mode
    - [X] Change runway rates
    - [ ] Insert flight
    - [ ] Change ETA FF

- [ ] Sequencing Algorithm Refinement
    - [ ] Revise speed-controls (`+` symbol, what does it actually mean, when it supposed to be used)
    - [X] Use FF and STAR suffix for ETI
    - [ ] Separate enroute and TMA delays
    - [ ] Consider separation at the feeder fix
    - [ ] Consider TMA delay modes (Normal, pressure, and max delay approach)
    - [ ] Factor GRIB winds into estimate calculations (If required)

- [ ] Terminal Configuration Refinement
    - [ ] Runway(s) to be used
    - [ ] Time of effect
    - [ ] STARs used in system calculations
    - [ ] Restrictions to be applied (E.g: B747 configured for certain runways only)

- [ ] Online Mode
    - [ ] Run sequencing code in a standalone server
    - [ ] Connect to Maestro server via WebSocket
    - [ ] Source configuration from server
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
    - [ ] Coordination window
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
