# Maestro.Avalonia.DesignHost

This project exists solely to enable the Avalonia XAML previewer in IDEs such as Rider.

`Maestro.Avalonia` is a class library loaded by vatSys at runtime.
It has no standalone entry point, so the IDE previewer has nothing to attach to.
This project provides that entry point by referencing `Maestro.Avalonia` and bootstrapping Avalonia via `Program.BuildAvaloniaApp()`.

The IDE discovers `BuildAvaloniaApp()` by convention and uses it as the host process when rendering XAML previews.
This project is not part of the plugin and is never deployed.
