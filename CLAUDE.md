# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

vMaestro is a vatSys plugin that emulates the Maestro Traffic Flow Management System for air traffic control. It's a C# .NET solution targeting both .NET Framework 4.7.2 and .NET 9.0, designed to manage aircraft sequencing and runway assignments for air traffic controllers.

## Build System

This project uses NUKE as its build orchestration tool:

- **Build**: `./build.sh` (Linux/macOS) or `./build.cmd` (Windows) or `./build.ps1` (PowerShell)
- **Test Core**: `dotnet test source/Maestro.Core.Tests/Maestro.Core.Tests.csproj`
- **Test Server**: `dotnet test source/Maestro.Server.Tests/Maestro.Server.Tests.csproj`
- **Test All**: `dotnet test source/Maestro.sln`
- **Build Solution**: `dotnet build source/Maestro.sln`
- **Run Server**: `dotnet run --project source/Maestro.Server/Maestro.Server.csproj`
- **Docker Server**: `docker-compose up` (includes Seq logging at http://localhost:5341)

The build system automatically handles .NET SDK installation if needed and uses GitVersion for versioning.

## Architecture

### Core Components

The solution consists of six main projects:

1. **Maestro.Core** - Domain logic and business rules
   - Multi-target: `net472` and `net9.0`
   - Contains scheduling algorithms, flight management, and configuration
   - Uses MediatR for message handling pattern
   - Key models: `Flight`, `Sequence`, `Scheduler`

2. **Maestro.Wpf** - WPF user interface components
   - Windows presentation layer with custom controls
   - MVVM pattern with ViewModels in `ViewModels/` directory
   - Custom styling to match vatSys theme

3. **Maestro.Plugin** - vatSys plugin integration
   - Targets `net472` for vatSys compatibility
   - Handles communication between vatSys and Maestro core
   - Plugin entry point and vatSys-specific adapters

4. **Maestro.Server** - SignalR server for online mode
   - ASP.NET Core server enabling multi-user collaboration
   - Hub-based real-time communication
   - Docker support via `docker-compose.yaml`

5. **Maestro.Core.Tests** - Unit tests for core domain
   - Uses xUnit v3, NSubstitute for mocking, and Shouldly for assertions
   - Comprehensive test coverage for scheduling algorithms and flight management

6. **Maestro.Server.Tests** - Unit tests for server components
   - Tests for SignalR hub functionality and server-side logic

### Key Architecture Patterns

- **CQRS/MediatR**: Request/response and notification patterns via MediatR
- **Domain-Driven Design**: Rich domain models with behavior (e.g., `Flight.cs`)
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection throughout
- **Scheduling Algorithm**: Core traffic flow management in `Sequence.Schedule()` method (`Sequence.cs`)
- **Configuration-Driven**: Airport configurations define runway modes, landing rates, and assignment rules

## Domain Model

### Flight Management
- **Flight States**: Unstable → Stable → SuperStable → Frozen → Landed
   - **Pseudo States**: New, Desequenced, Removed
- **Time Management**: ETA (Estimated Time of Arrival), STA (Scheduled Time of Arrival), both for feeder fixes and landings
- **Runway Assignment**: Automatic assignment with manual override capability
- **Flow Controls**: Speed controls applied during sequencing

### Sequencing System
- Flights are scheduled using configurable landing rates per runway
- Runway mode changes can be scheduled for future times
- Zero-delay flights and flights with manual intervention receive special handling
- Conflicts resolved by runway reassignment or applying delays
- **Core Algorithm** (`Sequence.Schedule()` in `Sequence.cs`):
  - Respects flight states (Frozen/Landed flights untouched, Stable flights only rescheduled when forced)
  - Automatic runway assignment based on feeder fix preferences
  - Enforces separation using runway acceptance rates
  - Handles conflicts via flight reordering or delay application
  - Integrates time slots and runway mode changes into scheduling decisions

## Testing

- Test framework: xUnit v3
- Mocking: NSubstitute
- Assertions: Shouldly
- Test builders available for `Flight` and `Sequence` objects in `Builders/` directory
- Fixed clock implementation for deterministic time-based testing

## Configuration

- `Maestro.json` contains airport configuration (runways, landing rates, assignment rules)
- Configuration loaded from vatSys profile directory
- DPI awareness fix available via `dpiawarefix.bat` for high-resolution displays

## Development Notes

- The project is under active development - check README roadmap for current status
- Logs written to vatSys files directory
- Server can be run via Docker or directly
- DPI awareness fix: run `dpiawarefix.bat` for high-resolution displays
