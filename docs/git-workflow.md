# Git, issues, and pull requests

## Issues before implementation

Features, bug fixes, and significant refactors require a GitHub issue before
implementation. The issue states the intended behavior, scope, and acceptance
criteria. Read its comments and labels as well as its body. Ask about unresolved
significant design decisions rather than silently choosing an architecture.

Small documentation and maintenance changes may use a standalone PR whose body
explains the work. Specs live in GitHub Issues; PRs are delivery and review
artifacts, not the issue-triage request surface. See
[tracker instructions](agents/issue-tracker.md) and [triage labels](agents/triage-labels.md).

## Branches

Use `<issue-number>-<issue-title>`, converting the title to a lowercase,
hyphen-separated Git-safe name. For example, issue 42, "Add player development",
uses `42-add-player-development`. Preserve the recognizable title where
possible. For work without an associated issue, use a short descriptive name,
such as `update-editorconfig`.

All changes reach `main` through a PR, including maintainer-authored changes and
small documentation fixes. Work on a branch rather than pushing directly to
`main`.

## Commits and PR titles

Use imperative language with an existing change-type prefix:

| Prefix | Use |
| --- | --- |
| `feat:` | A new feature |
| `fix:` | A bug fix or performance correction |
| `chore:` | Maintenance, documentation, configuration, or small improvements |
| `tests:` | Addition or revision of tests |

Examples: `feat: Add player development`, `fix: Validate contract terms`,
`chore: Align the SDK configuration`, `tests: Cover roster invariants`.
Avoid vague subjects such as "Fixed" or "Changes".

Use squash merge only. The PR title becomes the squash commit subject, so revise
it to describe the final change rather than intermediate work.

## Review and agent authority

The maintainer's review is sufficient; a second reviewer is not required.
**Every agent-created PR must receive the maintainer's review before merging.**
Passing checks alone does not satisfy this requirement.

For assigned implementation work, agents may edit, validate, commit, push the
working branch, and open a review-ready PR without another instruction. Resolve
failures in scope before handoff. If required validation is blocked, report the
reason and keep any PR in draft. Agents stop at the review handoff; merging
requires an explicit maintainer instruction after review. Do not enable
unattended auto-merge.

Follow the [stack's approval policy](tech-stack.md) before adding dependencies.

## PR content and validation

Follow the [PR template](../.github/pull_request_template.md): link the issue (or
explain why a maintenance PR has none), describe the final changes, and provide
reproducible validation evidence. Keep the title and body aligned with the final
scope. List material limitations, including unavailable native-platform checks.

Run the checks in [testing](testing.md). Formatting violations, build
warnings/errors, and failing tests block merge. Coverage is reported without a
percentage threshold once the test projects exist.

## Repository settings

The checked-in workflows provide checks; GitHub repository settings must enforce
the merge policy separately. At the foundation inspection, `main` was reported
as unprotected, no repository rulesets were listed, and all three merge methods
were enabled. This documentation/configuration pass does not modify those
remote settings.

After the new workflows have run, configure:

- A rule for `main` requiring PRs and successful `Formatting`,
  `Build and test (ubuntu-latest)`, `Build and test (windows-latest)`, and
  `Build and test (macos-latest)` checks. Verify their names from an actual run.
- Squash merge as the only merge method, with the PR title as commit subject.

Maintainer review is an explicit human step. If an agent creates a PR under the
maintainer's GitHub identity, [GitHub does not allow the author to approve their
own PR](https://docs.github.com/en/pull-requests/how-tos/review-pull-requests/approving-a-pull-request-with-required-reviews).
Do not require a second person's approval merely to work around
that constraint. A separate agent identity would be needed if that approval
must later be enforced through GitHub review counts. The detailed protection
endpoint was not accessible through the current integration during inspection.
