# Installation

## Prerequisites

Before installing vMaestro, ensure you have the following:

- [vatSys](https://virtualairtrafficsystem.com/) (version 1.4.20 or later)
- .NET Framework 4.7.2 or later

## Installing from GitHub

1. Download the [latest release from GitHub](https://github.com/YuKitsune/vMaestro/releases).
2. Extract the `Maestro.zip` file into your vatSys plugins directory (`Documents\vatSys Files\Profiles\<Profile Name>\Plugins\MaestroPlugin`).
3. Run the `unblock-dlls.bat` helper script (included in the `Maestro.zip` file) to unblock all the `.dll` files.

## Verifying Installation

1. Open vatSys.
2. Look for the `TFMS` menu item in the vatSys menu bar.
3. Click `TFMS`, then select an airport.
4. If the Maestro window appears, the installation was successful.

:::tip
If you do not see the `TFMS` menu item after restarting vatSys, refer to the [Troubleshooting](#troubleshooting) section below.
:::

## Troubleshooting

### TFMS menu item not appearing

If the TFMS menu item does not appear, it's likely that the `.dll` files for the plugin have been blocked by Windows.
This is a security feature in Windows that blocks files downloaded from the internet to protect your computer from potentially harmful software.

1. Locate the `unblock-dlls.bat` file (included in the `Maestro.zip` file).
2. Ensure the file is located in the same folder as the `.dll` files, or in one of the folders above it.
3. Run the script by double-clicking it. You will be shown a list of all the `.dll` files the script will unblock. Press `Y` to continue, or `N` to exit.
4. Restart vatSys once the script has completed.

This script will search for any `.dll` files in the current folder or sub-folders and ensure they are unblocked.

### DPI Awareness

If you are using a high-resolution display (4K monitor, high-DPI laptop screen, etc.) and experience graphical issues after launching vatSys, you may need to run the `dpiawarefix.bat` script.

1. Locate the `dpiawarefix.bat` file (included in the `Maestro.zip` file).
2. Run the script by double-clicking it.
3. Restart vatSys.

This script adjusts Windows DPI settings for vatSys, making it compatible with high-resolution displays.

### `Unable to locate Maestro.json` error

If you see this error, then the Maestro plugin cannot find the `Maestro.json` configuration file.
This file is necessary for the plugin to work.

Make sure the `Maestro.json` file is located alongside the `Maestro.Plugin.dll` file and in the same folder.

## Next Steps

Once installation is complete, proceed to the [System Overview](./02-system-overview.md) to learn about vMaestro's features, or jump directly to [System Operation](./03-system-operation.md) to start using Maestro.
