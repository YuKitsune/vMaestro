# Contributing to vMaestro

Thank you for your interest in contributing to vMaestro.

## Development Setup

### Prerequisites

- .NET SDK 10.0
- .NET Framework 4.7.2 Developer Pack (for plugin compilation)
- vatSys (required for plugin compilation - the `Maestro.Plugin` project automatically locates vatSys binaries)
- An IDE (Visual Studio, Rider, or VS Code with C# extension)

### Building

The project uses NUKE for build orchestration:

```bash
# Linux/macOS
./build.sh

# Windows
./build.cmd
# or
./build.ps1
```

Or build directly with the .NET CLI:

```bash
dotnet build source/Maestro.sln
```

### Installing the Plugin

To compile and install the plugin into a vatSys profile:

```bash
nuke install --profile-name <vatSys Profile Name>
```

### Running the Server

```bash
dotnet run --project source/Maestro.Server/Maestro.Server.csproj
```

Or with Docker:

```bash
docker-compose up --build
```

## Projects

| Project | Target | Purpose |
|---------|--------|---------|
| `Maestro.Core` | net472, net10.0 | Domain logic, scheduling algorithms, flight management |
| `Maestro.Wpf` | net472 | WPF UI components |
| `Maestro.Plugin` | net472 | vatSys plugin integration |
| `Maestro.Server` | net10.0 | SignalR server for multi-user operation |
| `Maestro.Core.Tests` | net10.0 | Unit tests for core logic |
| `Maestro.Server.Tests` | net10.0 | Unit tests for server |

`Maestro.Core` is independent of vatSys and can be developed and tested separately.

## Pull Requests

1. Fork the repository
2. Create a feature branch from `main`
3. Make your changes
4. Ensure tests pass
5. Submit a pull request

### Guidelines

- Keep changes focused and minimal
- Add tests for new functionality
- Follow existing code style
- Update documentation if behaviour changes

## Code Style

- Follow existing patterns in the codebase
- Use descriptive names
- Prefer composition over inheritance
- Keep methods focused and small
- Add comments only when the "why" isn't obvious

## Reporting Issues

Report issues at https://github.com/YuKitsune/vMaestro/issues

Include:

- Steps to reproduce
- Expected behaviour
- Actual behaviour
- vatSys and Maestro Plugin version)
- maestro_log.txt files

## License

By contributing, you agree that your contributions will be licensed under the same license as the project.
