# Recompute Function

The “Recompute” option recalculates all the flight's position in the sequence based on the latest estimates provided by vatSys.

This may become necessary when the flight is no longer in the `Unstable` state (so its position in the sequence is fixed) and can no longer meet its scheduled time, starting to delay all the flights behind it in the sequence.

When activated, the flight is placed in the sequence according to the last ETA-ff received from vatSys.
The state of the flight can change depending on its new position in the sequence. In particular, if the flight was already `Frozen` due to previous controller action it can become `Unstable`, `Stable` or
`Superstable`.

Invoking the recompute function will dismiss any manually assigned landing time, runway assignment, and unsets the zero-delay flag if it has been set. 

This function is not accessible for a dummy flight manually inserted in the sequence