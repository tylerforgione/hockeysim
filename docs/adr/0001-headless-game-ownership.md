# Keep game ownership independent of presentation

HockeySim's management workflows and match simulation must run without the UI.
Domain protects invariants; Management owns workflows and exposes commands and
read-only snapshots; a separate Simulation project returns match results for
Management to apply. This costs explicit input, result, and snapshot models,
but keeps UI changes from becoming alternate paths for changing the game world
and makes the same operations available to automated tests.

Infrastructure implements persistence contracts consumed by Management, and
Desktop wires dependencies with ordinary constructors. The dependency diagram
and current scaffolding gaps are in [architecture](../architecture.md).
