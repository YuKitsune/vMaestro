# TODO:

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

- [ ] Data Modelling
    - [ ] Consider how sequences should be modelled
    - [ ] Consider internal infrastructure for online and offline modes
    - [ ] Implement domain models, handlers, tiny types, etc.

- [ ] Temporary feed
    - [ ] Load data from existing Maestro API

- [ ] Load arrivals from vatSys into sequences
- [ ] Display arrivals on ladder
- [ ] Ensure ladder re-draws when FDR updates
- [ ] Refresh ladder on timer
- [ ] Clock

- [ ] Standalone mode
    - [ ] Sequencing algorithm
    - [ ] Automatically determine the sequence locally

- [ ] Online mode
    - [ ] Load configuration from maestro server
    - [ ] Master publishes sequence to clients
    - [ ] Master can manually sequence and control landing rates
    - [ ] Clients get a read-only view of the sequence

- [ ] MVP
    - [ ] Rename to Maestro
    - [ ] Fix DPI scaling issue

- [ ] Nice to haves
    - [ ] Debugger configuration for vatSys
    - [ ] Arrival list / GlobalOps backup
    - [ ] CSV or XLSX backup
    - [ ] Training mode
    - [ ] Customisable UI layout
    - [ ] Per-sequence online mode (E.g: YSSY offline, YMML online)

Need to figure out:
- When is the initial FF and landing time set? Is it ever reset?
- Does the ladder switch from using the estimated times to scheduled times?
    - Are the scheduled times set from the beginning and constantly re-calculated?
    