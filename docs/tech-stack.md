# Technology stack

| Area | Decision | Current status |
| --- | --- | --- |
| Language/runtime | C# on .NET 10 | Configured centrally |
| SDK | Exact stable version in `global.json`; no roll-forward or previews | Configured for local development and CI |
| Desktop | Avalonia on Windows, macOS, and Linux | Selected; shell and packaging not scaffolded |
| Presentation | MVVM with `CommunityToolkit.Mvvm` | Selected; no package reference yet |
| Tests | xUnit v3; .NET 10's Microsoft.Testing.Platform runner | Domain and Management test projects configured with coverage |
| Dependency wiring | Constructor injection, manually composed at Desktop startup | Policy for future implementation |
| Persistence | Separate Infrastructure project; versioned local saves | Storage technology deferred |

## Configuration ownership

- `global.json` pins the SDK used by both developers and GitHub Actions.
- `Directory.Build.props` holds shared target framework, nullable reference types,
  implicit usings, warning policy, and analyzer settings. Keep exceptions explicit
  in the project that needs them.
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
| Save format and storage technology | First persistence feature, with realistic save size, history, and query needs |
| Stable-release save compatibility | Before the first stable release |
| Concrete Avalonia package versions | Desktop scaffolding; verify compatible stable versions together |
| OS minimum versions and native packaging | Desktop scaffolding/release work, before claiming distributable support |

The three-OS build matrix checks portability of the current code. It does not
prove native UI behavior or packaging support. See [testing](testing.md).

## Tool references

- [.NET SDK selection](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json)
- [Central package management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)
- [Avalonia MVVM guidance](https://docs.avaloniaui.net/docs/how-to/mvvm-how-to)
- [xUnit v3 with Microsoft.Testing.Platform](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)
- [.NET test runner selection](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test)
