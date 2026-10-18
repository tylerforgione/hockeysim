---
name: repo-audit
description: Audits HockeySim's code and tests for chosen categories (dry, redundant-tests, performance, layering, determinism, dead-code) by fanning out one area-reviewer per area, verifying the findings, and reporting a ranked list for the maintainer to pick from. Drafts GitHub issues only for the findings the maintainer selects.
argument-hint: "[categories...] [areas...]"
disable-model-invocation: true
---

# Repo audit

Arguments: $ARGUMENTS

## 1. Scope

Category keys and their definitions are in [categories.md](categories.md). Area
keys are the area documents in `docs/areas/`: `match-engine`, `season`,
`players-and-lineups`, `saves`, `desktop`. Take any category and area keys from
the arguments; when none of a kind are given, use all of that kind, except
`dead-code`, which runs only when named: the game is incomplete, so code
without callers may still be wanted by planned features. If an argument
matches neither, ask what it means before starting.

## 2. Fan out

In a single message, launch one `area-reviewer` agent per selected area so they
run in parallel. Brief each with:

- The area document path, for example `docs/areas/season.md`.
- The selected category keys.
- Any focus the maintainer gave in the arguments beyond keys.

Do not read the area code yourself while they run.

## 3. Verify

Reviewer findings are leads, not conclusions. For each one:

- Open the cited lines and confirm the quoted code and the problem are real.
  Drop findings that do not hold up, and findings that conflict with an ADR or
  a [future features](../../../docs/future-features.md) entry.
- Follow cross-area leads with `grep`, and merge findings that describe the same
  problem from two areas into one.
- Check `dead-code` findings for XAML binding and serialization use.

## 4. Report

Reply with a table ranked by severity, then by fix size:

| # | Category | Severity | Size | Location | Finding |
| --- | --- | --- | --- | --- | --- |

Keep each finding to one line. Under the table, give the evidence and suggested
fix for high-severity findings only, then one line per area: findings dropped in
verification and categories with nothing found. Do not change any code.

## 5. Issues for the findings the maintainer picks

When the maintainer selects findings, group the ones that belong in the same
change, then draft one issue per group following
[issue tracker](../../../docs/agents/issue-tracker.md) and
[Git workflow](../../../docs/git-workflow.md#issues-before-implementation):
title in the repository's style, then the problem with its locations, the
intended behaviour or structure, scope, and acceptance criteria. Show the drafts
and create them with `gh issue create --body-file` only after the maintainer
approves them. Add no labels unless asked.
