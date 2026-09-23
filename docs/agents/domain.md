# Domain docs

This repo uses a single-context layout:

- `CONTEXT.md` at the repo root holds domain terminology.
- `docs/adr/` holds architecture decision records.

## Before exploring

Read `CONTEXT.md` and the ADRs relevant to the area being explored.

If these files or directories do not exist, proceed silently.
The domain-modeling skill creates them when terminology or decisions
are resolved.

## Use the glossary's vocabulary

Use terms defined in `CONTEXT.md` when naming domain concepts in issues,
proposals, hypotheses, and tests.

When a concept is missing, reconsider whether it belongs in the domain
or record the gap for domain-modeling.

## Surface ADR conflicts

If a proposal contradicts an existing ADR, identify the ADR and explain
why the decision should be reconsidered.
