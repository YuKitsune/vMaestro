# TODO:

- [ ] Eurocat Look and Feel
    - [X] Pull theme from vatSys
    - [X] Create custom controls to follow vatSys theme
    - [X] Custom buttons
    - [X] Custom separators
    - [X] Clean up timeline
    - [X] Clean up combobox
    - [ ] Fix sector selector
    - [ ] Move inline styles into Theme and Style.xaml

- [ ] Configuration
    - [ ] Load configuration from file
    - [ ] Use configuration in TFMS window
    - [ ] Use configuration in Ladder
    - [ ] Use configuration in Sequence

- [ ] Data Modelling
    - [ ] Consider how sequences should be modelled
    - [ ] Consider internal infrastructure for online and offline modes
    - [ ] Implement domain models, handlers, tiny types, etc.

- [ ] Load arrivals from vatSys into sequences
- [ ] Display arrivals on ladder
- [ ] Ensure ladder re-draws when FDR updates
- [ ] Refresh ladder on timer
- [ ] Clock

- [ ] Sequencing algorithm
- [ ] Standalone mode
    - [ ] Automatically determine the sequence locally
- [ ] Online mode
    - [ ] Master publishes sequence to clients
    - [ ] Master can manually sequence and control landing rates
    - [ ] Clients get a read-only view of the sequence

- [ ] MVP
    - [ ] Rename to Maestro
    - [ ] Fix DPI scaling issue
    - [ ] Fix font issue

- [ ] Nice to haves
    - [ ] Debugger configuration for vatSys
    - [ ] Arrival list / GlobalOps backup
    - [ ] CSV or XLSX backup
    - [ ] Training mode
    - [ ] Customisable UI layout

Need to figure out:
- When is the initial FF and landing time set? Is it ever reset?
- Does the ladder switch from using the estimated times to scheduled times?
    - Are the scheduled times set from the beginning and constantly re-calculated?
    