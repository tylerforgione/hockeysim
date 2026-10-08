# HockeySim

[![Validate](https://github.com/tylerforgione/hockeysim/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/tylerforgione/hockeysim/actions/workflows/build-and-test.yml)
[![CodeQL](https://github.com/tylerforgione/hockeysim/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/tylerforgione/hockeysim/actions/workflows/codeql-analysis.yml)

HockeySim is a single-player hockey management game, designed to run fully
offline. The desktop application uses Avalonia on Windows, macOS, and Linux. Matches are simulated independently of their presentation.

## Status

The repository contains a headless new-game workflow that creates a reproducible
32-team fictional league, protects Domain invariants, and exposes read-only
Management snapshots. The Avalonia desktop application provides new-game setup
and an in-game shell with a home dashboard, an inbox, the managed team's roster
and lineup editor, and read-only browsing of every team's roster. A new game
also generates a reproducible, balanced 84-match regular-season schedule. A headless
Simulation project plays matches as play-by-play hockey events (shifts, faceoffs,
shot attempts with expected goals, hits, turnovers, penalties, and fights) from
two teams' lineups, playing power plays, penalty kills, and every other strength
state with the lineup's units, with three-on-three overtime and a shootout, and
derives every skater and goalie statistic from the events. Management advances the
season one league day at a time, playing every scheduled match and accumulating
results, team records, and player season statistics through to a completed
season, with league, conference, and division standings ranked by the NHL
tie-breaking procedure. Desktop advances the season one league day at a time,
shows each team's schedule and results with single-match box scores, summarises
the latest league day and division table on the home page, presents division,
conference, and league standings, and shows every player's current-season totals
on roster tables and player profiles. Games are saved under names the user
chooses and loaded from the startup menu, resuming exactly where they were
saved; the app asks before overwriting a save or discarding unsaved progress.

## Download and run

Builds for Windows, macOS, and Linux are attached to each
[GitHub release](https://github.com/tylerforgione/hockeysim/releases). They
include everything they need, so .NET does not have to be installed. Download
the archive for your system:

| System | Archive |
| --- | --- |
| Windows 10 or 11 (64-bit) | `HockeySim-<version>-win-x64.zip` |
| macOS 15 or later on Apple silicon | `HockeySim-<version>-osx-arm64.zip` |
| macOS 15 or later on Intel | `HockeySim-<version>-osx-x64.zip` |
| Linux (x64, glibc, X11 or XWayland) | `HockeySim-<version>-linux-x64.tar.gz` |

Unpack it and run `HockeySim` (`HockeySim.exe` on Windows) from the unpacked
folder, keeping the other files beside it. The startup menu shows the version.
`0.x` releases are pre-releases: a save from one release may not load in
another.

The builds are not signed, so the first launch shows a warning:

- **Windows**: SmartScreen says "Windows protected your PC". Choose
  **More info**, then **Run anyway**.
- **macOS**: Gatekeeper blocks a downloaded build that is not notarized.
  Remove the download quarantine from the unpacked folder in Terminal, then run
  the game:

  ```sh
  xattr -dr com.apple.quarantine HockeySim-<version>-osx-arm64
  ./HockeySim-<version>-osx-arm64/HockeySim
  ```

- **Linux**: no warning, but the game needs the ICU, fontconfig, and X11
  libraries that desktop distributions normally include. Run
  `./HockeySim` from the unpacked folder.

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

### Publish a build

Publish a self-contained build for one runtime identifier with:

```sh
dotnet publish src/HockeySim.Desktop/HockeySim.Desktop.csproj --configuration Release --runtime win-x64
dotnet publish src/HockeySim.Desktop/HockeySim.Desktop.csproj --configuration Release --runtime osx-arm64
dotnet publish src/HockeySim.Desktop/HockeySim.Desktop.csproj --configuration Release --runtime osx-x64
dotnet publish src/HockeySim.Desktop/HockeySim.Desktop.csproj --configuration Release --runtime linux-x64
```

The output is in `src/HockeySim.Desktop/bin/Release/net10.0/<identifier>/publish/`,
or the folder given with `--output`. Add `-p:Version=<version>` to set the
version; without it the build is the development version `0.0.0-dev`. Publish
macOS builds on macOS so they are signed ad hoc, which Apple silicon requires.

### Releases

Pushing a tag `v<version>`, such as `v0.1.0`, runs the
[release workflow](.github/workflows/release.yml). It validates the tagged
commit, publishes every runtime identifier, and creates a GitHub release with
the archives and generated notes; `0.x` versions are marked as pre-releases.
Run the workflow manually for a dry run that only uploads the archives as
workflow artifacts. See [ADR 0005](docs/adr/0005-self-contained-desktop-builds.md)
for the build and versioning decisions.

## Engineering guide

- [Architecture and repository structure](docs/architecture.md)
- [Technology stack and deferred choices](docs/tech-stack.md)
- [Coding conventions](docs/conventions.md)
- [Testing and validation](docs/testing.md)
- [Git, issue, and PR workflow](docs/git-workflow.md)
- [Future features](docs/future-features.md)
- [Domain vocabulary](CONTEXT.md) and [architecture decisions](docs/adr/)
- [Agent instructions](AGENTS.md)
