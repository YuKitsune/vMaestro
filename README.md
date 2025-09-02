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

- When the Maestro.json file is missing, vatSys gets flooded with exceptions
- Desequence and Information windows do not re-draw
- Window can be re-opened multiple times

# Roadmap

- [X] Publish notifications on FDP updates
- [X] Information window
- [X] Basic sequencing algorithm
- [X] Automatic runway assignment
- [X] Dependant runways
- [X] Recompute
- [X] Remove from sequence
- [X] Desequence
- [X] Change runway
- [X] Move flight (click and drag)
- [ ] Move flight (single click)
- [ ] Swap flights
- [X] Change runway mode
- [X] Change ETA FF
- [X] Zero Delay and priority flights
- [X] Insert slot
- [X] Insert flight
- [X] Insert departure
- [ ] Maximum delay (revisit High Priority and Zero Delay)
- [ ] Unit selector
- [ ] Approach types
- [ ] Separate enroute and TMA delays
- [ ] Coordination window
- [ ] Start sequencing on-demand
- [ ] Online mode
- [ ] Ladder scrolling
- [ ] GitHub actions CI
- [ ] Architecture Decision Record
- [ ] Docs