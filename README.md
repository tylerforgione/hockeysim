# HockeySim

[![Validate](https://github.com/tylerforgione/hockeysim/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/tylerforgione/hockeysim/actions/workflows/build-and-test.yml)
[![CodeQL](https://github.com/tylerforgione/hockeysim/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/tylerforgione/hockeysim/actions/workflows/codeql-analysis.yml)


[![C#](https://img.shields.io/badge/C%23-14-239120)](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-14)
[![.NET](https://img.shields.io/badge/.NET-10.0.401-512BD4)](global.json)
[![Avalonia](https://img.shields.io/badge/Avalonia-12.1.4-8B44AC)](Directory.Packages.props)

HockeySim is an offline single-player hockey management sim for Windows, macOS,
and Linux. Be the GM of a team in a completely fictional league; control of your
favourite NHL team is planned.

## Status

- **Current release:** [v0.1.0](https://github.com/tylerforgione/hockeysim/releases/tag/v0.1.0)
  (pre-release). Play a full fictional-league season with saves.
- **Next release:** [v0.2.0](https://github.com/tylerforgione/hockeysim/milestone/1)
  adds a deeper match engine with special teams and advanced statistics,
  in-match injuries, preseason and playoffs, and easier navigation.
- **After that:** v0.3.0 adds season rollover.

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
