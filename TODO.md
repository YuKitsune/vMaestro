# TODOs

- [ ] Trim down the Sequence aggregate
    - [X] Move the Pending list into the Session
    - [X] Move the De-sequenced list into the Session
    - [ ] Add Momento for Session, and synchronise it with the server
    - [ ] Introduce simpler sequence interaction methods
        - Insert at index: Insert a new flight at the specified index
        - FindFlight: Returns the flight with the specified callsign, otherwise null
        - Move to index: Move the flight at index `i` to index `j`
        - Swap: Swap the items at indexes `i` and `j`
        - Remove: Remove the item at the specified index
        - FirstIndexOf: Find the index of the first item matching the specified predicate
    - [ ] Extract logic from Sequence methods into the MediatR handlers that use them
- [ ] Fix runway assignment
    - We frequently need to lookup the runway mode and feeder fix preferences to determine which runway to assign to a flight. This is getting repetitive and teadous to figure out.
- [ ] Fix runway checks
    - We often filter for flights on the same runway, but we never account for flights on related runways (i.e. dependent runways)
    - Will likely need to store the whole runway type on the flight to do this
- [ ] Review tests cases
