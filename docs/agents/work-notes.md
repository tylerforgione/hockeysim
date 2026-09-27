# Work notes

Work notes carry the state of a branch across agent sessions: what was done,
what remains, and decisions waiting on the maintainer. They are a local,
gitignored cache; GitHub issues and PRs stay the source of truth for scope
and review.

## Location

One Markdown file per branch at `.agents/branches/<branch-name>.md`, using the
exact branch name (for example `.agents/branches/8-build-the-avalonia-team-management-application.md`).
Branch names already start with the issue number (see
[Git workflow](../git-workflow.md#branches)), so the file is findable by issue
too. `.agents/branches/` is gitignored.

## When to read and write

- **Start of a session on a branch**: read the branch's work notes if present
  and treat the open items as the starting backlog. Verify stale-looking claims
  against the code before acting on them.
- **Handoff** (end of a session, before reporting completion, or before opening
  a PR): update the notes so a fresh agent could continue with no other
  context. The notes are complete when every open item from this session is
  either marked done or listed under Remaining with its reason.

## Template

```markdown
# <branch-name>

Issue: #<number> (<title>) · Last updated: <YYYY-MM-DD>

## Status
One or two sentences: where the branch stands (e.g. committed locally, pushed,
PR open, awaiting review).

## Done
- Delivered behaviour, with commit hashes where useful.

## Remaining
- [ ] Concrete next step, with why it is still open (blocked, deferred, needs approval).

## Awaiting maintainer
- Decisions made provisionally, or questions only the maintainer can answer.

## Notes
- Gotchas, validation commands actually run, and pointers a future session needs.
```

Keep entries short and current: rewrite items as they change rather than
appending a history log; commits and PRs already hold the history.
