# Contributing to vMaestro

Thank you for your interest in contributing to vMaestro.

## Development Setup

### Prerequisites

- .NET SDK 10.0
- .NET Framework 4.7.2 Developer Pack (for plugin compilation)
- vatSys (required for plugin compilation, the `Maestro.Plugin` project automatically locates vatSys binaries)
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
dotnet build source/Maestro.slnx
```

### Installing the Plugin

To compile and install the plugin into a vatSys profile:

```bash
nuke install --profile-name "<vatSys Profile Name>"
```

### Running the Server

#### In your IDE or with the .NET CLI

```bash
dotnet run --project source/Maestro.Server/Maestro.Server.csproj
```

#### With Docker

```bash
docker-compose up --build
```

Both methods start the server on `http://localhost:5272`.

Before running, update `Maestro.yaml` so the plugin connects to your local instance:

```yaml
Server:
  Uri: http://localhost:5272/hub
```

> The version compatibility check between the plugin and server is disabled for local dev builds (version `0.0.0`), so no version alignment is required when running locally.

## Projects

| Project | Target | Purpose |
|---------|--------|---------|
| `Maestro.Core` | net472, net10.0 | Domain logic, scheduling algorithms, flight management |
| `Maestro.Wpf` | net472 | WPF UI components |
| `Maestro.Plugin` | net472 | vatSys plugin integration |
| `Maestro.Server` | net10.0 | SignalR server for multi-user operation |
| `Maestro.Core.Tests` | net10.0 | Unit tests for core logic |
| `Maestro.Server.Tests` | net10.0 | Unit tests for server |

`Maestro.Core` is independent of vatSys and can be developed and tested without it.

## Branching

vMaestro uses a trunk-based development model.
In short, `main` is always the integration point, branches are short-lived, and release branches are cut at release milestones.

### Before v1

`main` is the only long-lived branch.
Branch from it, make your changes, and merge back via a pull request.

### After v1

Once v1 ships, the branching model expands:

| Branch | Purpose |
|--------|---------|
| `main` | vNext for active development, may contain breaking changes |
| `releases/v1` | v1 maintenance, for bug fixes and patches only |

**Breaking changes** go on `main` only. Create a short-lived branch from `main`, open a PR, and merge it back.

**Bug fixes and non-breaking changes for v1** follow a merge-forward process:

1. Branch from `releases/v1`
2. Fix the bug
3. Open a PR into `releases/v1`
4. Once merged, open a second PR to merge `releases/v1` forward into `main`

Merging forward keeps `main` up to date with all fixes shipped in v1. Do not cherry-pick - always merge the branch forward.

Breaking changes must never be introduced on a release branch.
Bug fixes, patches, and non-breaking improvements are all acceptable.

## Pull Requests

1. Fork the repository (external contributors) or create a branch directly (maintainers)
2. Branch from the appropriate base (see above)
3. Keep changes focused, so one concern per PR
4. Ensure all tests pass locally before opening a PR
5. Add tests for new functionality
6. Update documentation if behaviour changes

The CI build runs automatically on pull requests and must pass before merging.

## Reporting Issues

Report issues at https://github.com/YuKitsune/vMaestro/issues

Include:

- Steps to reproduce
- Expected behaviour
- Actual behaviour
- vatSys and Maestro Plugin version
- `maestro_log.txt` files

## License

By contributing, you agree that your contributions will be licensed under the same license as the project.
