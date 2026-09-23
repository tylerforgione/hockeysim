# Working in HockeySim

## Explore and change code

- Before exploring, read [domain guidance](docs/agents/domain.md), then
  [architecture](docs/architecture.md). The existing application is provisional
  scaffolding; architecture distinguishes current and planned projects.
- Before changing code or dependencies, read [conventions](docs/conventions.md)
  and [technology stack](docs/tech-stack.md), including dependency approval policy.
- Before adding tests or validating changes, read [testing](docs/testing.md).
  Report the commands actually run and distinguish missing tests from passing tests.

## Issues and delivery

- Before starting substantive implementation or performing branch, commit, PR,
  or merge operations, read [Git workflow](docs/git-workflow.md). It defines issue
  requirements, agent autonomy, and mandatory maintainer review of agent PRs.
- Before tracker operations, read [issue tracker](docs/agents/issue-tracker.md).
  Issues and specs live in GitHub Issues.
- Before assigning triage labels, read [triage labels](docs/agents/triage-labels.md).
  Use the five documented labels.

## Keep decisions current

When work changes an established responsibility or dependency direction, discuss
the change before implementation and update the relevant documentation. Record
resolved domain terminology in `CONTEXT.md` and consequential architecture
tradeoffs in `docs/adr/`, following the single-context layout.
