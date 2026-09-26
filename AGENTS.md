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

## Subagents

When delegating work to a subagent:

- Do not repeatedly poll or query the subagent for status while it is working.
- After delegation, allow the subagent to run independently and report completion through the normal subagent completion mechanism.
- Only check on a running subagent if:
  - its result is required before other work can proceed and no completion notification has arrived after a reasonable amount of time;
  - you need to provide new information or change its task; or
  - there is evidence that the subagent is stuck or has failed.
- Do not use status checks merely to determine whether a subagent has finished.
- Prefer doing other independent work while subagents are running. If there is no other work, wait for their completion rather than polling them.