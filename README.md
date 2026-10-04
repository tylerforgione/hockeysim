# HockeySim

[![Validate](https://github.com/tylerforgione/hockeysim/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/tylerforgione/hockeysim/actions/workflows/build-and-test.yml)
[![CodeQL](https://github.com/tylerforgione/hockeysim/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/tylerforgione/hockeysim/actions/workflows/codeql-analysis.yml)

HockeySim is a single-player hockey management game, designed to run fully
offline. The planned desktop application uses Avalonia on Windows, macOS, and
Linux. Matches are simulated independently of their presentation.

## Status

The repository contains a headless new-game workflow that creates a reproducible
32-team fictional league, protects Domain invariants, and exposes read-only
Management snapshots. The Avalonia desktop application provides new-game setup
and an in-game shell with a home dashboard, an inbox, the managed team's roster
and lineup editor, and read-only browsing of every team's roster. Advancing
days, schedules, and standings are not implemented yet. A headless Simulation
project calculates statistical match results from two teams' lineups, but the
game does not play matches yet. Infrastructure has not yet been scaffolded.

## Development

Install the exact stable .NET SDK specified in [global.json](global.json).
Local development and CI use the same SDK; update the pin through a PR.

From the repository root:

```sh
dotnet restore HockeySim.slnx
dotnet build HockeySim.slnx --configuration Release --no-restore
dotnet format HockeySim.slnx --verify-no-changes --no-restore --severity warn
```

Run the Avalonia desktop application from source with:

```sh
dotnet run --project src/HockeySim.Desktop/HockeySim.Desktop.csproj
```

See [testing](docs/testing.md) for the complete validation commands.

## Engineering guide

- [Architecture and repository structure](docs/architecture.md)
- [Technology stack and deferred choices](docs/tech-stack.md)
- [Coding conventions](docs/conventions.md)
- [Testing and validation](docs/testing.md)
- [Git, issue, and PR workflow](docs/git-workflow.md)
- [Future features](docs/future-features.md)
- [Domain vocabulary](CONTEXT.md) and [architecture decisions](docs/adr/)
- [Agent instructions](AGENTS.md)
