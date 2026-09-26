# HockeySim

[![Validate](https://github.com/tylerforgione/hockeysim/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/tylerforgione/hockeysim/actions/workflows/build-and-test.yml)
[![CodeQL](https://github.com/tylerforgione/hockeysim/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/tylerforgione/hockeysim/actions/workflows/codeql-analysis.yml)

HockeySim is a single-player hockey management game, designed to run fully
offline. The planned desktop application uses Avalonia on Windows, macOS, and
Linux. Matches are simulated independently of their presentation.

## Status

The repository contains a headless new-game workflow that creates a reproducible
32-team fictional league, protects Domain invariants, and exposes read-only
Management snapshots. Domain and Management behavior tests run with coverage in
CI. Desktop remains a console placeholder; Simulation, Infrastructure, and the
Avalonia shell are planned, not scaffolded.

## Development

Install the exact stable .NET SDK specified in [global.json](global.json).
Local development and CI use the same SDK; update the pin through a PR.

From the repository root:

```sh
dotnet restore HockeySim.slnx
dotnet build HockeySim.slnx --configuration Release --no-restore
dotnet format HockeySim.slnx --verify-no-changes --no-restore --severity warn
```

The current console placeholder can be run with
`dotnet run --project src/HockeySim.Desktop`; it is not yet the Avalonia UI.
See [testing](docs/testing.md) for the test setup and future execution commands.

## Engineering guide

- [Architecture and repository structure](docs/architecture.md)
- [Technology stack and deferred choices](docs/tech-stack.md)
- [Coding conventions](docs/conventions.md)
- [Testing and validation](docs/testing.md)
- [Git, issue, and PR workflow](docs/git-workflow.md)
- [Deferred game-design decisions](docs/deferred.md)
- [Domain vocabulary](CONTEXT.md) and [architecture decisions](docs/adr/)
- [Agent instructions](AGENTS.md)
