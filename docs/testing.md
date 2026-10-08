# Testing and validation

## Current status

Every production project has a matching xUnit v3 test project on
Microsoft.Testing.Platform. Each [area document](architecture.md#current-implementation)
has a test map listing its test files and what each covers; read that rather
than scanning the test projects. The test names record the individual checks.

## Test organization

Mirror the five production responsibilities under `tests/`, using
`HockeySim.<Responsibility>.Tests` for project names. Group tests by the feature
or behavior they exercise. Initially keep focused and workflow tests in the
same owning project; split slow or environment-dependent suites only when
execution needs justify it.

| Area | What to verify |
| --- | --- |
| Domain | Invariants, valid operations, rejected invalid states, and edge cases |
| Simulation | Match calculations and results, controlled randomness, and reproducibility |
| Management | Workflows with real Domain and Simulation implementations, resulting state, and read-only snapshot behavior |
| Infrastructure | Real save/load round trips in isolated temporary storage, preserved random state, and clear handling of incompatible versions |
| Desktop | View-model presentation behavior and selected headless binding/interaction tests |

Use real core implementations by default in Management workflow tests. Small,
controlled scenarios keep them fast. Substitute the match engine only when a
specific outcome or failure is necessary to exercise the behavior under test.
Tests should use the same meaningful interfaces as callers, rather than depend
on private implementation details.

For releases, also smoke-test the native application on Windows, macOS, and
Linux, including packaging and launching. Headless UI tests do not replace
those checks. Full desktop end-to-end automation is deferred until concrete
failures or repetitive checks justify its cost.

## Simulation testing

A single seeded example shows little about whether a stochastic match engine is
correct. Simulation tests combine three kinds of check, all using fixed seeds so
failures indicate changed behavior rather than unlucky randomness:

- **Invariants across many seeds.** Run the engine over a fixed set of seeds and
  assert every result is valid: the score matches goal events, shots are at
  least goals, only dressed players appear in events, and a result has a winner
  when the rules require one.
- **Determinism.** The same inputs and random state produce an identical result,
  different seeds produce differing results, and inputs are not mutated. This
  protects the [reproducible saves](adr/0002-reproducible-saves.md) promise.
- **Statistical bands.** Over many seeded matches, aggregate outcomes stay within
  wide, plausible bands, such as average goals per match, and relative
  expectations hold, such as a much stronger team winning most matches and
  evenly matched teams splitting results. Prefer relative assertions over exact
  targets so deliberate rebalancing does not break them; bands catch broken
  tuning, not small balance changes.

Avoid exact golden-master comparisons of seeded output while balance is still
changing; every deliberate tuning change would invalidate them. Revisit them
once balance stabilizes. Long-run Management tests simulate a full season
headlessly and assert the world remains valid afterward.

## Local validation

Use the exact SDK from `global.json`. From the repository root:

```sh
dotnet restore HockeySim.slnx
dotnet build HockeySim.slnx --configuration Release --no-restore
dotnet format HockeySim.slnx --verify-no-changes --no-restore --severity warn
```

Run each test project against the Release build:

```sh
dotnet test --project tests/HockeySim.Domain.Tests/HockeySim.Domain.Tests.csproj --configuration Release --no-build --no-restore --minimum-expected-tests 1
dotnet test --project tests/HockeySim.Simulation.Tests/HockeySim.Simulation.Tests.csproj --configuration Release --no-build --no-restore --minimum-expected-tests 1
dotnet test --project tests/HockeySim.Desktop.Tests/HockeySim.Desktop.Tests.csproj --configuration Release --no-build --no-restore --minimum-expected-tests 1
dotnet test --project tests/HockeySim.Management.Tests/HockeySim.Management.Tests.csproj --configuration Release --no-build --no-restore --minimum-expected-tests 1
dotnet test --project tests/HockeySim.Infrastructure.Tests/HockeySim.Infrastructure.Tests.csproj --configuration Release --no-build --no-restore --minimum-expected-tests 1
```

This is the native .NET 10 Microsoft.Testing.Platform command shape selected in
`global.json`; use compatible xUnit v3 MTP packages when scaffolding. The minimum
expected count prevents a test project with zero discovered tests from passing.
Do not suppress discovery or runner failures.

To apply C# formatting locally, run
`dotnet format HockeySim.slnx --no-restore --severity warn`, then review its diff.
Re-run validation when fixes change the relevant code or configuration.

## CI and coverage

The [validation workflow](../.github/workflows/build-and-test.yml) runs on every
branch push and every PR to `main`, including configuration and documentation
changes. It checks formatting once and builds/tests on all three operating
systems. Test commands run each discovered `tests/**/*.Tests.csproj` against the
Release build; a project omitted from the solution cannot silently count as a
successful solution test run. The projects run in parallel, so the test step
takes about as long as the slowest project; each project's output is printed as
a collapsible log group, and any failing project fails the step. CodeQL analyzes the normal PR checkout using an
explicit build with the pinned SDK.

Formatting failures, build warnings/errors, and test failures must block merge.
[Git workflow](git-workflow.md#repository-settings) lists the remote enforcement
that still needs configuring. Informational style suggestions are not promoted
to errors indiscriminately.

CI collects a Cobertura report from each test project with the
Microsoft.Testing.Platform coverage extension and publishes the reports as a
per-platform artifact. There is no percentage gate: review meaningful behavior
and edge cases, including critical invariants, persistence, and reproducibility.
Revisit a threshold only after a useful baseline exists.

## Release builds

The [release workflow](../.github/workflows/release.yml) runs the validation
workflow above on the tagged commit, then publishes each runtime identifier on
a runner of its own OS and checks that the executable is in the output. That
proves the build publishes and packages; it does not launch the app. A manual
run (`workflow_dispatch`) is a dry run that uploads the archives as workflow
artifacts without creating a release.

Before tagging a release, and when publish settings change, smoke-test the
published build natively on each supported platform, preferably on a machine
with no .NET installed: launch it, confirm the startup menu shows the expected
version, start a new game, advance at least one day, then save, and load that
save. Record which OS and architecture combinations were checked and which were
not. The Desktop tests check that the startup menu shows the running version
and that a source build reports `0.0.0-dev`.

To publish locally, see the [README](../README.md#publish-a-build).

## Evidence in PRs

Record commands run, their outcomes, and any untested platforms or blocked
checks. For documentation/configuration changes, verify links and configuration
syntax as relevant. Distinguish current CI results from intended future checks;
report missing tests or coverage explicitly.

References: [xUnit v3 MTP setup](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform),
[.NET test runner selection](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test),
[Avalonia headless testing](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform).
