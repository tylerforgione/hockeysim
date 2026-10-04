# Technology stack

| Area | Decision | Current status |
| --- | --- | --- |
| Language/runtime | C# on .NET 10 | Configured centrally |
| SDK | Exact stable version in `global.json`; no roll-forward or previews | Configured for local development and CI |
| Desktop | Avalonia on Windows, macOS, and Linux | New-game flow and in-game team-management shell (home, inbox, roster, lines, league teams, standings, schedule and results, season totals, daily advancement, named save and load); self-contained single-file release builds for `win-x64`, `osx-arm64`, `osx-x64`, and `linux-x64` ([ADR 0005](adr/0005-self-contained-desktop-builds.md)); native packaging and signing deferred |
| Presentation | MVVM with `CommunityToolkit.Mvvm` | Configured for feature-oriented view models |
| Tests | xUnit v3; .NET 10's Microsoft.Testing.Platform runner | Domain, Simulation, Management, and Desktop test projects configured with coverage |
| Dependency wiring | Constructor injection, manually composed at Desktop startup | Policy for future implementation |
| Releases | Git tag `v<version>` sets the release (and engine) version; `0.x` releases are GitHub pre-releases | Release workflow publishes and attaches archives; source builds are `0.0.0-dev` |
| Persistence | Separate Infrastructure project; versioned local saves as one Brotli-compressed System.Text.Json document per game ([ADR 0004](adr/0004-local-save-format.md)) | Named manual saves in the Desktop app, kept in the user's application-data folder; no autosave |

## Configuration ownership

- `global.json` pins the SDK used by both developers and GitHub Actions.
- `Directory.Build.props` holds shared target framework, nullable reference types,
  implicit usings, warning policy, and analyzer settings. Keep exceptions explicit
  in the project that needs them. It also sets the development version
  `0.0.0-dev`, which release builds replace with the tag's version.
- `HockeySim.Desktop.csproj` holds the self-contained, single-file publish
  settings, which apply only when a runtime identifier is given.
- `Directory.Packages.props` owns NuGet package versions. Projects declare their
  package references without local versions. It currently pins the approved
  xUnit v3 and Microsoft Testing Platform coverage packages used by tests.
- `.editorconfig` owns C# naming and formatting preferences.

Discuss a new runtime dependency with the maintainer before relying on it,
unless the issue already approves it. Routine updates to approved dependencies
and the SDK go through the normal PR workflow. Dependabot checks NuGet manifests
and GitHub Actions weekly; inspect SDK updates explicitly rather than assuming
Dependabot maintains the exact SDK pin.

## Deferred decisions

| Decision | Resolve when |
| --- | --- |
| Stable-release save compatibility | Before the first stable release |
| Native packaging, code signing, and installers | Before builds are offered beyond pre-release testers; see [future features](future-features.md#native-packaging-and-updates) |

The three-OS build matrix checks portability of the current code. It does not
prove native UI behavior. Supported runtime identifiers and minimum OS versions
are recorded in [ADR 0005](adr/0005-self-contained-desktop-builds.md). See
[testing](testing.md).

## Tool references

- [.NET SDK selection](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json)
- [Central package management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)
- [Avalonia MVVM guidance](https://docs.avaloniaui.net/docs/how-to/mvvm-how-to)
- [xUnit v3 with Microsoft.Testing.Platform](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)
- [.NET test runner selection](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test)
