# P1 — Graph Toolkit viability spike

Status: **closed 2026-11-24, not adopted.** Graph Toolkit cannot attach a decorator
to a branch the way UE does (`docs/gt-notes.md`, "Decorators"), so no editor is
built. The 6.7-beta rebuild was not run. Findings and API notes: `docs/gt-notes.md`.

## Where things live

Unity-compiled spike code (this repo's dev project): `Assets/Spikes/GtSpike/`

- `Editor/SpikeGraph.cs` — graph class, extension `gtspike`, subgraphs enabled
- `Editor/SpikeComposites.cs`, `SpikeTasks.cs`, `SpikeGuards.cs` — 11 node types
  with options (float/int/string/enum, `Delayed()`), vertical ports
- `Editor/SpikeNodeLook.cs`, `Editor/SpikeNode.uss` — shared colors and the
  per-type stylesheet that widens the node card
- `Editor/SpikeGraphImporter.cs` — ScriptedImporter compiling to `SpikeRuntimeGraph`
- `Editor/SpikeWindowProbe.cs` — reflection helpers for GTK's window and importer
  API
- `Editor/SpikeGraphFrame.cs` — fits every open graph window to its viewport
- `Runtime/SpikeRuntimeGraph.cs` — minimal compiled data model
- `Tests/Editor/SpikeGraphTests.cs` — headless round-trip proof (green)

Menu items: `Assets/Create/spikes/GT Spike Graph`, `spikes/Open GT Spike Graph
(probe)`, `spikes/Probe GTK importers`, `spikes/Fit graph to window`.

The supported way to see the graph is to double-click
`Assets/Spikes/GtSpike/TestArtifacts/MainGraph.gtspike` (git-ignored; created by the
round-trip test). A graph asset re-created by the test while the editor runs opens
empty until the editor restarts — `docs/gt-notes.md`, "Opening the graph window".

## What the spike showed

1. Graph tool authoring on the 6000.6 Graph API works end to end: graph asset,
   11 node types, custom inspector fields, subgraph node, programmatic wiring.
2. Serialization round-trips (save → load → structure and layout equality), plain
   Unity YAML.
3. The importer pattern (tool-owned ScriptedImporter → runtime asset) works and is
   mandatory — GTK ships none.
4. The supported open path (double-click, `AssetDatabase.OpenAsset`) renders nodes
   and wires. A `[Node(.., stylesheet)]` stylesheet reaches the node's visual tree;
   `Node` itself exposes only `Title`, `Subtitle` and `DefaultColor`.
5. The whole loop (authoring, import, screenshot) is drivable through the Unity
   CLI — screenshots go to `Artifacts/screenshots/` (not committed).
