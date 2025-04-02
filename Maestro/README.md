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

- [ ] Ladder configuration
    - [ ] TMA view
    - [ ] ENR view

- [ ] Offline Mode
    - [ ] Consider how sequences should be modelled
    - [ ] Implement domain models, handlers, tiny types, etc.
    - [ ] Publish notifications on FDP updates
    - [ ] Sequencing algorithm

- [ ] Online mode
    - [ ] Connect to Maestro server
    - [ ] Source configuration from server
    - [ ] Redirect update notifications to server
    - [ ] Source sequence information from server
    - [ ] Allow Flow to modify the sequence
    - [ ] Authentication

- [ ] MVP
    - [ ] Fix DPI scaling issue
    - [ ] Loading indicator

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
    