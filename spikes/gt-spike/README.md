# P1 — Graph Toolkit viability spike

Status: **core pass on 6000.6.10f1** (2026-11-23). Remaining P1 exit items: the
6.7-beta rebuild leg (blocked on beta availability), the vision review of the
screenshot (blocked on vision-model configuration), and the human aesthetic gate.

## Where things live

Unity-compiled spike code (this repo's dev project): `Assets/Spikes/GtSpike/`

- `Editor/SpikeGraph.cs` — graph class, extension `gtspike`, subgraphs enabled
- `Editor/SpikeComposites.cs`, `SpikeTasks.cs`, `SpikeGuards.cs` — 11 node types
  with options (float/int/string/enum, `Delayed()`), port capacities
- `Editor/SpikeGraphImporter.cs` — ScriptedImporter compiling to `SpikeRuntimeGraph`
- `Editor/SpikeWindowProbe.cs` — throwaway reflection helpers (GTK window opening,
  importer probing); delete in Phase 2 when the supported path exists
- `Runtime/SpikeRuntimeGraph.cs` — minimal compiled data model
- `Tests/Editor/SpikeGraphTests.cs` — headless round-trip proof (green)

Menu items: `Assets/Create/spikes/GT Spike Graph`, `spikes/Open GT Spike Graph
(probe)`, `spikes/Probe GTK importers`. Findings and API notes: `docs/gt-notes.md`.

## What the spike proved

1. Graph tool authoring on the 6000.6 Graph API works end to end: graph asset,
   11 node types, custom inspector fields, subgraph node, programmatic wiring.
2. Serialization round-trips (save → load → structure equality), plain Unity YAML.
3. The importer pattern (tool-owned ScriptedImporter → runtime asset) works and is
   mandatory — GTK ships none.
4. The graph window opens programmatically via `GraphObject.LoadGraphObjectAtPath`
   + `ShowGraph`, but **node content does not render through that path** (empty
   grid, three vision-reviewed captures). The supported open flow (double-click /
   CustomEditor Edit button) is a Phase-2 task — see gt-notes.
5. The whole loop (authoring, import) is drivable headlessly via the unity CLI —
   screenshots captured to `Artifacts/screenshots/`.
