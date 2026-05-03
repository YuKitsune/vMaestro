# Maestro Server

The Maestro Server enables multi-user operation by synchronizing sequence state between connected clients.

## Architecture

The server is a message relay. No sequence computations are performed on the server. All scheduling and sequencing logic runs on the master client.

### Master/Slave Model

- **Master**: The first client to connect for a given airport. Owns the sequence and performs all computations.
- **Slaves**: Subsequent clients. Receive a read-only view of the sequence.

### Message Flow

1. Slaves send sequence modification requests to the server
2. The server relays these requests to the master
3. The master performs the modification and computes the updated sequence
4. The master broadcasts the updated state to all slaves via the server

### State

The server stores the most recently synchronized sequence in-memory.
Once all clients disconnect, the sequence is deleted.
