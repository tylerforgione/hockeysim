# Publish self-contained, single-file desktop builds versioned by Git tag

Players should run HockeySim without installing .NET. Each release therefore
publishes the desktop app as a **self-contained** build that carries its own
.NET runtime. A framework-dependent build would be smaller, but it needs a
separately installed runtime, and a machine-wide .NET update could then change
behaviour within one engine version
([reproducible saves](0002-reproducible-saves.md)). Bundling the runtime makes
the release version identify the exact code that runs.

Each build is also **single-file**: one `HockeySim` executable, with the native
graphics libraries (SkiaSharp, HarfBuzzSharp, and Avalonia's macOS or ANGLE
library) beside it. The natives are loaded from that folder rather than
extracted to a cache on first run, which avoids leaving files behind and the
antivirus warnings that self-extraction can trigger. The plain publish output
was a folder of about 230 files in which players would have to find the
executable.

`dotnet publish -c Release -r <identifier>` produces this build. The settings
live in `HockeySim.Desktop.csproj` and apply only when a runtime identifier is
given, so ordinary builds and tests stay framework-dependent.

## Rejected and deferred options

- **Trimming** is deferred. It reduced an Apple silicon build from 41 MB to
  16 MB zipped, and macOS and Linux published without trim warnings, but
  Windows failed: `BuiltInComInteropSupport` is not trim-compatible (IL2026).
  Avalonia 12 probably no longer needs that setting, but removing it needs a
  native Windows check. Trimming also fails at run time in paths that a short
  smoke test may not reach, and 25 MB is not a problem for a desktop game.
  Revisit it if download size matters, removing `BuiltInComInteropSupport`
  only after a Windows check.
- **ReadyToRun** is deferred. It precompiles code to shorten startup at the
  cost of a larger download. Startup has not been measured as a problem.
- **Native AOT** is rejected for now. It needs a fully trim- and
  AOT-compatible app and dependencies, which is more than trimming already
  deferred above.

## Supported platforms

| Identifier | Platform | Minimum version |
| --- | --- | --- |
| `win-x64` | Windows on x64 | Windows 10 (64-bit), as supported by .NET 10 |
| `osx-arm64` | macOS on Apple silicon | macOS 15 |
| `osx-x64` | macOS on Intel | macOS 15 |
| `linux-x64` | Linux on x64 with glibc and X11 or XWayland | glibc 2.27, with ICU and fontconfig installed |

These follow [.NET 10's supported OS list](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md):
macOS 14 left .NET support in September 2026, and the bundled native
libraries require older versions than these minimums. Linux builds need the
desktop libraries that standard desktop distributions already include, such
as ICU, because dates and numbers are formatted for the user's culture. Arm
Windows and Linux, and musl-based Linux such as Alpine, are not published.

Each identifier is published on a runner of its own operating system, so the
macOS build gets the ad-hoc code signature that Apple silicon requires and the
Linux archive keeps the executable bit. Builds are not code-signed for
distribution or notarized, and there is no macOS `.app` bundle, installer, or
auto-update; see [future features](../future-features.md#native-packaging-and-updates).

## Versioning

A release's version is its Git tag, `v<version>`, starting at `v0.1.0`. The
release workflow passes the version to MSBuild with `-p:Version=`, and every
assembly carries it. A build without a version, from source or CI, is
`0.0.0-dev`, so it never claims to be a release. The informational version
leaves out the commit hash, so a release shows exactly its tag's version. The
startup menu shows the running version so bug reports can cite it.

The release version is also the **engine version**: the reproducibility
promise holds for one release, and a development build promises nothing
beyond its own commit. The engine version is independent of the save
`FormatVersion` ([ADR 0004](0004-local-save-format.md)). A release may leave
the save format unchanged, and the format version changes only when the saved
shape does.

Releases numbered `0.x` are published as GitHub pre-releases. Save
compatibility between stable releases is still to be decided before `1.0`.
