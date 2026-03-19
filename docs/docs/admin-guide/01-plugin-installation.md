---
sidebar_position: 1
---

# Plugin Installation

This page covers how to install the vMaestro plugin into vatSys.

## Prerequisites

- [vatSys](https://virtualairtrafficsystem.com/) version 1.4.20 or later
- .NET Framework 4.7.2 or later

## Installation

1. Download the [latest release from GitHub](https://github.com/YuKitsune/vMaestro/releases)
2. Extract `Maestro.zip` into your vatSys plugins directory:
   ```
   Documents\vatSys Files\Profiles\<Profile Name>\Plugins\MaestroPlugin
   ```
3. Run `unblock-dlls.bat` (included in the zip) to unblock the DLL files

## Verification

1. Open vatSys
2. Look for the `TFMS` menu item in the menu bar
3. Click `TFMS` and select an airport
4. If the vMaestro window appears, installation was successful

## Troubleshooting

### TFMS menu item not appearing

The DLL files may be blocked by Windows security.

1. Locate `unblock-dlls.bat` in the plugin folder (or a parent folder)
2. Run the script by double-clicking it
3. Press `Y` when prompted to confirm
4. Restart vatSys

### DPI Awareness Issues

On high-resolution displays, graphical issues may occur.

1. Locate `dpiawarefix.bat` in the plugin folder
2. Run the script
3. Restart vatSys

### Unable to locate Maestro.yaml

The plugin cannot find its configuration file.

Ensure `Maestro.yaml` is placed in one of the following locations (searched in order):

1. `{ProfileDirectory}/Plugins/Configs/Maestro/Maestro.yaml`
2. `{ProfileDirectory}/Plugins/Configs/Maestro.yaml`
3. `{ProfileDirectory}/Plugins/Maestro.yaml`
4. `{ProfileDirectory}/Maestro.yaml`
5. `{PluginDirectory}/Maestro.yaml`

See [Plugin Configuration](./02-plugin-configuration.md) for configuration details.

## Next Steps

After installation, proceed to [Plugin Configuration](./02-plugin-configuration.md) to set up airports and runway modes.
