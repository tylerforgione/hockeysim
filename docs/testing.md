# Testing and validation

## Current status

Domain and Management test projects use xUnit v3 with
Microsoft.Testing.Platform. They cover generated-world invariants, managed-team
selection, reproducibility, snapshot isolation, and focused Domain validation.
Simulation, Infrastructure, and Desktop tests remain pending with their projects
or concrete behavior.

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
dotnet test --project tests/HockeySim.Management.Tests/HockeySim.Management.Tests.csproj --configuration Release --no-build --no-restore --minimum-expected-tests 1
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
successful solution test run. CodeQL analyzes the normal PR checkout using an
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

## Evidence in PRs

Record commands run, their outcomes, and any untested platforms or blocked
checks. For documentation/configuration changes, verify links and configuration
syntax as relevant. Distinguish current CI results from intended future checks;
report missing tests or coverage explicitly.

References: [xUnit v3 MTP setup](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform),
[.NET test runner selection](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test),
[Avalonia headless testing](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform).
