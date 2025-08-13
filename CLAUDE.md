# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

vMaestro is a vatSys plugin that emulates the Maestro Traffic Flow Management System for air traffic control. It's a C# .NET solution targeting both .NET Framework 4.7.2 and .NET 9.0, designed to manage aircraft sequencing and runway assignments for air traffic controllers.

## Build System

This project uses NUKE as its build orchestration tool:

- **Build**: `./build.sh` (Linux/macOS) or `./build.cmd` (Windows) or `./build.ps1` (PowerShell)
- **Test**: `dotnet test source/Maestro.Core.Tests/Maestro.Core.Tests.csproj`
- **Build Solution**: `dotnet build source/Maestro.sln`

The build system automatically handles .NET SDK installation if needed and uses GitVersion for versioning.

## Architecture

### Core Components

The solution consists of four main projects:

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

4. **Maestro.Core.Tests** - Unit tests
   - Uses xUnit v3, NSubstitute for mocking, and Shouldly for assertions
   - Comprehensive test coverage for scheduling algorithms and flight management

### Key Architecture Patterns

- **CQRS/MediatR**: Request/response and notification patterns via MediatR
- **Domain-Driven Design**: Rich domain models with behavior (e.g., `Flight.cs:99-125`)
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection throughout
- **Scheduling Algorithm**: Core traffic flow management in `Scheduler` implementations
- **Configuration-Driven**: Airport configurations define runway modes, landing rates, and assignment rules

## Domain Model

### Flight Management
- **Flight States**: Unstable → Stable → SuperStable → Frozen → Landed
- **Time Management**: ETA (Estimated Time of Arrival), STA (Scheduled Time of Arrival), both for feeder fixes and landings
- **Runway Assignment**: Automatic assignment with manual override capability
- **Flow Controls**: Speed controls applied during sequencing

### Sequencing System
- Flights are scheduled using configurable landing rates per runway
- Runway mode changes can be scheduled for future times
- Zero-delay flights and flights with manual intervention receive special handling
- Conflicts resolved by runway reassignment or applying delays

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
- Outputs build artifacts directly to Australian vatSys profile for easy debugging
- Logs written to `MaestroLogs` directory in vatSys installation