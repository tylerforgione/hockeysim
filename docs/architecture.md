# Architecture

HockeySim is a single-player, offline management game. Game operations run
without a desktop UI; the user manages a team rather than directly controlling
athletes during matches. A future match viewer may present simulation output.

## Project responsibilities

| Project | Responsibility |
| --- | --- |
| `HockeySim.Domain` | Domain objects, value types, and invariants that must always hold. Controlled operations protect validity; callers cannot bypass them through mutable collections. |
| `HockeySim.Simulation` | Match calculations. Accepts match inputs and controlled randomness, maintains internal match state, and returns a result. |
| `HockeySim.Management` | Game workflows, world generation, season orchestration, and applying match results through Domain operations. Exposes game-state commands and read-only snapshots. |
| `HockeySim.Infrastructure` | Save/load, serialization, file access, and other external data access. Implements persistence contracts consumed by Management. |
| `HockeySim.Desktop` | Avalonia views, presentation state, view models, and startup wiring. Sends game-state changes to Management and displays its snapshots. |

Domain enforces validity; Management coordinates operations; Simulation
calculates match outcomes. Neither UI behavior nor storage details belong in
those core responsibilities.

## Dependencies and interaction

The intended project dependency direction is:

```mermaid
flowchart TD
    Desktop --> Management
    Desktop --> Infrastructure
    Desktop --> Simulation
    Infrastructure --> Management
    Infrastructure --> Domain
    Management --> Simulation
    Management --> Domain
    Simulation --> Domain
```

Management owns the persistence contracts it needs; Infrastructure implements
them. Domain has no dependency on the other application projects. Management
and Simulation have no dependency on Desktop or Infrastructure. Keep framework
and storage-specific types out of core interfaces.

Desktop's references to Infrastructure and Simulation serve startup composition.
Use ordinary constructors and manual wiring there. Views and view models use
Management's interface; they do not invoke the match engine, storage, or live
Domain mutation methods directly. Snapshots expose only read-only data, without
mutable references back into the game world. Shape snapshots for callers rather
than copying the entire model indiscriminately.

Simulation returns a match result instead of changing the persistent game world.
Management applies that result through Domain operations. See
[the ownership decision](adr/0001-headless-game-ownership.md).

## Randomness and saves

Within the same engine version, the same starting state, random state, and
sequence of management actions produce the same result. Preserve hidden random
state in saves so reloading and repeating actions does not reroll an outcome.
Keep outcome-affecting randomness and time inputs under explicit control.
Simulation owns the single deterministic generator (`ControlledRandom`) and its
persistable `RandomState`; Management uses the same generator for world
generation and passes the state to and from match simulation.
Reproducibility is not a promise across engine versions or protection against
editing local saves. See [the rationale](adr/0002-reproducible-saves.md).

Pre-release saves may become incompatible. Version the save format and reject
unsupported versions clearly. Choose the format and storage technology during
the first persistence feature, using representative data and access patterns.
Establish a longer-term compatibility policy before the first stable release.

## Repository structure

Production projects live in `src/`; matching test projects live in `tests/`:

```text
src/HockeySim.<Responsibility>/
tests/HockeySim.<Responsibility>.Tests/
docs/
  adr/
  agents/
CONTEXT.md
```

Use the five responsibilities in the table above. Add projects to
`HockeySim.slnx` when they are scaffolded. Within a project, group related files
by feature or domain responsibility, such as a view and view model together
under `Roster/`. Keep small projects flat until grouping is useful. Name folders
for their responsibility instead of creating generic `Helpers` or `Managers`
collections. Namespaces follow the owning project and folder.

## Current implementation

Domain, Management, and Desktop exist today. Domain protects generated-world
invariants through validated construction and read-only collections. Management
owns a headless new-game workflow, controlled random state, managed-team
selection, and read-only snapshots. Fictional names are kept separate from the
league and roster rules that use them.

Management also delivers inbox messages to the user. New-game messages are
derived from the generated managed team, so they never describe state the game
does not hold; selecting a different managed team replaces them with messages
for that team. Marking a message read is a Management command.

Desktop wires a Management game manager at startup. After the new-game screen
starts a game, a `GameSession` forwards commands (lineup changes, reading
messages) to Management and publishes each resulting snapshot to the in-game
shell's feature pages: home, inbox, roster, lines, and league teams. Only the
managed team's lineup is editable, and Management validates every change.
Colours and control styles live in `HockeySim.Desktop/Theme/`; team identity
colours are dynamic resources so a chosen team's colours can replace the
league defaults later.
Simulation calculates a match statistically from both teams' lineups as they
stand at match start. Player ratings, fixed forward-line and defence-pair usage
weights, and starting-goalie quality drive shots and goals. Tied matches go to
sudden-death overtime and then a shootout. It takes an explicit random state and returns
the state after the match with the result, without changing the teams.
Management does not invoke it yet; season orchestration will apply results.
Infrastructure has not been scaffolded; until it exists, the related parts of
the diagram remain target architecture.
