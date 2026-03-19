# Contributing to vMaestro

Thank you for your interest in contributing to vMaestro.

## Development Setup

### Prerequisites

- .NET SDK 10.0
- .NET Framework 4.7.2 Developer Pack (for plugin compilation)
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

### Running Tests

```bash
# All tests
dotnet test source/Maestro.sln

# Core tests only
dotnet test source/Maestro.Core.Tests/Maestro.Core.Tests.csproj

# Server tests only
dotnet test source/Maestro.Server.Tests/Maestro.Server.Tests.csproj
```

### Running the Server

```bash
dotnet run --project source/Maestro.Server/Maestro.Server.csproj
```

Or with Docker:

```bash
docker-compose up
```

## Architecture

### Projects

| Project | Target | Purpose |
|---------|--------|---------|
| `Maestro.Core` | net472, net10.0 | Domain logic, scheduling algorithms, flight management |
| `Maestro.Wpf` | net472 | WPF UI components |
| `Maestro.Plugin` | net472 | vatSys plugin integration |
| `Maestro.Server` | net10.0 | SignalR server for multi-user operation |
| `Maestro.Core.Tests` | net10.0 | Unit tests for core logic |
| `Maestro.Server.Tests` | net10.0 | Unit tests for server |

### Patterns

- **MediatR** - Request/response and notification patterns
- **MVVM** - WPF ViewModels in `Maestro.Wpf/ViewModels/`
- **Dependency Injection** - Microsoft.Extensions.DependencyInjection

### Key Components

- **Flight** (`Maestro.Core/Models/Flight.cs`) - Domain model for tracked flights
- **Sequence** (`Maestro.Core/Models/Sequence.cs`) - Contains the `Schedule()` method implementing the core scheduling algorithm
- **Scheduler** - Orchestrates sequence updates

### Flight States

Flights progress through states: Unstable → Stable → SuperStable → Frozen → Landed

The scheduling algorithm in `Sequence.Schedule()`:
- Respects flight states (Frozen/Landed flights are not modified)
- Assigns runways based on feeder fix preferences
- Enforces separation using acceptance rates
- Handles conflicts via reordering or delay application

## Testing

- **Framework**: xUnit v3
- **Mocking**: NSubstitute
- **Assertions**: Shouldly
- **Test Builders**: Available in `Builders/` directories for `Flight` and `Sequence`
- **Fixed Clock**: Use for deterministic time-based testing

Example:

```csharp
[Fact]
public void Should_assign_runway_based_on_feeder_fix()
{
    // Arrange
    var sequence = new SequenceBuilder()
        .WithRunwayMode("34IVA")
        .Build();

    var flight = new FlightBuilder()
        .ViaFeederFix("RIVET")
        .Build();

    // Act
    sequence.Add(flight);
    sequence.Schedule();

    // Assert
    flight.AssignedRunway.ShouldBe("34L");
}
```

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
- vMaestro version
- vatSys version (if plugin-related)

## License

By contributing, you agree that your contributions will be licensed under the same license as the project.
