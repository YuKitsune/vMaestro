# TODO:

- [ ] Eurocat Look and Feel
    - [X] Pull theme from vatSys
    - [X] Create custom controls to follow vatSys theme
    - [X] Custom buttons
    - [X] Custom separators
    - [X] Clean up timeline
    - [ ] Clena up combobox

- [ ] Configuration
    - [ ] Load configuration from file
    - [ ] Use configuration in TFMS window
    - [ ] Use configuration in Ladder
    - [ ] Use configuration in Sequence

- [ ] Load arrivals from vatSys into sequences
- [ ] Consider how to model sequences
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

- [ ] Rename to Maestro

- [ ] Nice to haves
    - [ ] Debugger configuration for vatSys
    - [ ] Arrival list / GlobalOps backup
    - [ ] CSV or XLSX backup
    - [ ] Training mode
    - [ ] Customisable UI layout

Need to figure out:
- When is the initial FF and landing time set? Is it ever reset?
- When does the ladder switch from using the estimated times to scheduled times?
    - Are the scheduled times set from the beginning and constantly re-calculated?
    