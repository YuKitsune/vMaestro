---
sidebar_position: 3
---

# Online Mode

vMaestro supports multi-user operation through an optional server component. This allows multiple controllers to share a single sequence in real-time.

## Connection States

The online status indicator in the Configuration Zone shows the current connection state:

| Status | Meaning |
| ------ | ------- |
| `OFFLINE` | Not connected to a server. All processing is local and all functions are available. |
| `READY` | Connected to the server but not synchronised. Appears when connected before joining the VATSIM network. |
| `FMP` | Connected with the Flow role. Processing is done locally and the sequence is shared to all other clients. |
| `APP` | Connected with the Approach role. Some functions may be restricted. |
| `ENR` | Connected with the Enroute role. Some functions may be restricted. |
| `ENR/FMP` or `APP/FMP` | Connected with Enroute or Approach role, but no dedicated FMP is online. All functions are available. |
| `OBS` | Connected with the Observer role. The sequence is read-only. |

## Roles

### Flow (FMP)

The Flow role is intended for the Flow Management Position. When a controller with the Flow role is online:
- They control the master sequence
- Their client performs all scheduling calculations
- Changes are broadcast to all other connected clients

### Approach (APP)

The Approach role is intended for Approach controllers working traffic within the TMA. Depending on configuration, some functions may be restricted to require Flow approval.

### Enroute (ENR)

The Enroute role is intended for Enroute controllers managing traffic before the TMA boundary. Depending on configuration, some functions may be restricted.

### Observer (OBS)

The Observer role provides read-only access to the sequence. Observers cannot make any modifications.

## Pseudo-Master Mode

When no controller with the Flow role is online, the connected Approach or Enroute controllers operate in pseudo-master mode (shown as `ENR/FMP` or `APP/FMP`). In this mode:
- All functions are available
- The first connected client becomes the master
- Sequence changes are still synchronised across all clients

## Partitions

Partitions allow multiple independent sequences for the same airport to run simultaneously. Common uses include:
- Separating live VATSIM operations from training sessions
- Running multiple training scenarios concurrently

Each partition maintains its own sequence state and does not affect other partitions.

## Permissions

Administrators can configure which roles are permitted to perform specific actions. See the [Admin Guide](../../admin-guide/02-plugin-configuration.md#server-configuration) for permission configuration details.
