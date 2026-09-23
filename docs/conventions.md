# Code conventions

## Naming and layout

Use [.editorconfig](../.editorconfig) for naming and formatting preferences.
Namespaces follow the owning project and folder. Follow the feature-oriented
layout in [architecture](architecture.md).

## Types and methods

- Prefer explicit domain types when primitives would obscure meaning.
- Use `var` when the type is obvious from the right-hand side, such as
  `var player = new Player(...)`; use an explicit type otherwise.
- Nullable reference types are enabled. Model optional data deliberately.
- Prefer immutable types where appropriate. Protect Domain invariants through
  controlled operations and avoid exposing mutable state that bypasses them.
- Give methods one clear responsibility, use guard clauses, and keep public
  interfaces small. Extract a named operation when it clarifies responsibility.
- Use informative method names. An unwieldy name is a reason to reconsider the
  responsibility, not to hide essential intent in a comment.

## Comments and documentation

Explain non-obvious intent, business rules, constraints, and tradeoffs where
readers need that context, even when the code is short. Use enough lines to
explain clearly; avoid narrating obvious code. Keep larger architecture
tradeoffs in ADRs and link them where useful.

Remove obsolete code rather than commenting it out; Git preserves its history.
Use TODOs only for a clearly defined next step, linking its issue when one exists.

Use XML documentation for public interfaces whose intent is not obvious,
complex domain behavior, and non-obvious parameters or return values.

## Validation and exceptions

Validate inputs at system entry points and enforce invariants in Domain.
Use domain-specific exceptions where appropriate. Catch exceptions when there
is meaningful handling; do not catch them only to ignore them.

## Enforcement

Compiler warnings, including nullable warnings, are errors. The pinned SDK's
.NET 10 default analyzer rules and explicitly configured warning-level style
rules run during builds. `dotnet format` checks formatting and warning-level
style/analyzer diagnostics; advisory suggestions remain informational.

Change enforced rules deliberately in a PR. Avoid broad suppressions to make a
check pass; explain any narrowly justified exception near its configuration.
See [testing](testing.md) for the exact validation commands.

## Tests

Test observable behavior rather than implementation details. Names should
clearly describe the behavior under test. New features include tests, and bug
fixes include regression tests. The project layout, test scope, and coverage
policy are in [testing](testing.md).
