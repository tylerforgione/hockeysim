# Testing and validation

## Current status

Domain, Simulation, Management, and Desktop test projects use xUnit v3 with
Microsoft.Testing.Platform. They cover generated-world invariants, managed-team
selection, reproducibility, snapshot isolation, focused Domain validation,
inbox messages, new-game and in-game view-model behavior (navigation, lineup
editing, team browsing, player detail), and a headless Avalonia walkthrough
from the startup menu through every available in-game page, playing a day, and
opening a match's box score. Desktop season tests advance real games: the
current-date and continue states, page refresh after a day, an empty league day,
opening a result's box score from the home page, schedule browsing, a failing
match engine leaving the day unplayed with an error shown, overlapping
advancement rejected while a gated engine holds a day, and a full season ending
in the completed, still-browsable state with final standings. Desktop standings
and statistics tests check each standings scope against Management's ranked
tables, the level no-games state, refresh after a day with the chosen scope kept,
skater and goalie totals for another team's roster and player detail, zero
totals and undefined percentages before an appearance, the roster column choice
kept across teams and days, and pages redrawn when the session's snapshot is
replaced. The headless walkthrough also renders the standings scopes and the
roster's season columns, saves from the title bar, loads the save from the
startup menu through the discard confirmation, and checks that closing the
window with unsaved progress asks instead of closing. Desktop save and load
tests use real save files in a temporary folder: saving under a typed or chosen
name, what counts as unsaved progress (playing a day, changing the lineup,
reading mail, but not a failed day), overwrite confirmation with cancellation
leaving the file untouched, invalid names, write and listing failures, saving a
completed season, saving and leaving for the menu blocked while a day plays,
and loading into every page. Discard confirmations for loading, starting a new
game, and exiting are checked with cancellation keeping the game and with no
question asked when nothing is unsaved. Damaged, other-version, and unreadable
saves are reported with the game in progress kept, and play continued after a
load matches uninterrupted play. Simulation tests
check result invariants across many seeds, each decision path (regulation,
overtime, shootout), determinism, unchanged input teams, and statistical bands
for lineup strength, line and pair usage, and goalie quality. Individual match
statistics are checked for reconciliation with the score and shots, appearance
and eligibility rules, assist validity, shootout exclusion, and zero-shot and
zero-production cases. Schedule tests check, across several seeds, the full
opponent-count matrix, league match count, home/away totals, venue balance
within each pair of opponents, valid identities, no self-matches or same-day
conflicts, the calendar dates, and reproducibility. Domain season tests check
day-level atomicity (partial days, results for other dates, duplicates, invalid
players), standings points for each decision, shootout handling in team and
individual totals, completed-match invariants, and the terminal state. Domain
standings scenarios isolate each ranking criterion, two-club and multi-club
head-to-head (unbalanced meetings, cycles, a partly broken tie, clubs that have
not met), odd-game exclusion, shootout goals, games-played differences, the
no-games state, and teams level on every criterion. Management standings tests
check that league, conference, and division tables hold the right teams in a
consistent order before any match, midseason, and after the full season, and
that they are read-only and isolated between snapshots.
Management season tests advance real games: whole days, empty days, lineup
changes used by the next match, a failing match engine leaving the day
unplayed and the random state unchanged, rejected nested and serialized
cross-thread advancement, snapshot isolation, and reproducibility. A shared
fixture plays one full 1,344-match season and checks schedule completion, 84
games per team, records and individual totals reconciled with the results, every
decision type, and the completed-season state.
Management save tests round-trip a new game, a midseason game with a changed
lineup and a read message, and a completed season. They check that the loaded
game matches what was saved, from rosters and lineups through every result,
team records, season totals, standings, the inbox, and the random state, and
that a lineup change and further league days after loading give the same
results as uninterrupted play. They also check that loading replaces a
different active game, that a save is a copy later play does not change, and
that a loaded game accepts commands. Each kind of invalid save is rejected
(missing values, missing, extra, or duplicate results, current dates outside the
season, scores that do not reconcile, unrostered or ineligible players, invalid
ratings, an unknown managed team or scheduled team, misnumbered inbox messages),
and the active game then continues exactly as if the load was never attempted.
Infrastructure tests save and load real files in a temporary directory: new,
midseason, and completed games with continued play compared against
uninterrupted play, the file header, replacing an earlier save, a failed save
leaving the earlier save intact with no temporary file, the random state
preserved exactly at its extremes, other format versions rejected as
unsupported, non-save, damaged, truncated, and malformed files rejected, a
well-formed save that breaks game rules rejected with the active game
unchanged, and missing or unwritable saves reported as storage failures. Save
directory tests keep separate named saves, list them newest first with their
save time, find a save whose name differs only in case, skip files that are
not named saves, and report a folder that cannot be created. Management tests
check which save names are accepted, their case-insensitive equality, and
composed accents.
Rating tests check that a player needs every defined rating, and that the
overall rating's weights total 100 for each position and never include
durability. They also check the overall at the edges: uniform 0, 67, and 100
ratings, one maximum rating contributing its documented weight, unused ratings
having no effect, and half points rounding up. Management generation tests,
over several seeds, check that centres lead faceoffs, that defence lead
defending while forwards lead scoring, and that only goalies are rated for
goaltending. They also check that overall ratings spread at every position and
that traits, including durability read from a save, vary. Snapshots omit
durability, saves carry it through a load, and a save without it is rejected.
Desktop tests check the OVR column, the overall in the player profile, and
profiles that list only the ratings a position uses, never durability.

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
