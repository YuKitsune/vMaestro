---
sidebar_position: 1
---

# vMaestro

:::warning Flight Simulation Only
This software is intended for **flight simulation use only** on networks such as VATSIM. It must not be used for real-world aviation or air traffic control operations. No warranty is provided; all content is offered "as is" without liability.
:::

vMaestro is a [vatSys](https://virtualairtrafficsystem.com) plugin that emulates the Maestro Traffic Flow Management System (TFMS), designed to assist air traffic controllers in sequencing arrival traffic.

## Key Features

Using real-time data from vatSys, vMaestro provides:

- **Automatic Runway Assignment** - Allocates incoming flights to runways based on feeder fixes and aircraft performance
- **Optimised Scheduling** - Calculates landing times to minimise delay while maintaining safe separation
- **Visual Timeline** - Displays the sequence in a vertically scrolling timeline
- **Speed Control Suggestions** - Provides suggested speed adjustments to absorb delays
- **Manual Control** - Allows controllers to modify the sequence to reflect operational requirements
- **Multi-User Support** - Supports online collaboration through the optional server component

## Documentation

### For Controllers

The **[User Guide](./user-guide)** covers vMaestro concepts and operation:

- [System Overview](./user-guide/system-overview) - How vMaestro works, flight states, and sequencing behaviour
- [System Operation](./user-guide/system-operation) - Interface navigation and day-to-day operation

### For Administrators

The **[Admin Guide](./admin-guide)** covers installation, configuration, and deployment:

- [Plugin Installation](./admin-guide/01-plugin-installation.md) - Installing vMaestro into vatSys
- [Plugin Configuration](./admin-guide/02-plugin-configuration.md) - Configuring airports and runway modes
- [Server Deployment](./admin-guide/03-server-deployment.md) - Running the server for multi-user operation
- [API Access](./admin-guide/04-api-access.md) - Accessing the API documentation

### For Contributors

See [CONTRIBUTING.md](https://github.com/YuKitsune/vMaestro/blob/main/CONTRIBUTING.md) in the repository for development setup, architecture, and contribution guidelines.
