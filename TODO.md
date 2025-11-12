# TODOs

- [ ] Trim down the Sequence aggregate
    - [ ] Move the Pending list into the Session
    - [ ] Move the De-sequenced list into the Session
    - [ ] Add Momento for Session, and synchronise it with the server
    - [ ] Introduce simpler sequence interaction methods
        - Insert at index: Insert a new flight at the specified index
        - FindFlight: Returns the flight with the specified callsign, otherwise null
        - Move to index: Move the flight at index `i` to index `j`
        - Swap: Swap the items at indexes `i` and `j`
        - Remove: Remove the item at the specified index
        - FirstIndexOf: Find the index of the first item matching the specified predicate
    - [ ] Extract logic from Sequence methods into the MediatR handlers that use them
- [ ] Review tests cases
