# Graph Toolkit spike notes (R1 / P1)

Spike: `Assets/Spikes/GtSpike/` (throwaway, Editor+Runtime asmdefs, never referenced
from product assemblies). Verified on **Unity 6000.6.10f1** on 2026-11-23; round-trip
test `Gorecode.Bt.Spikes.GtSpike.Tests.SpikeGraphTests` is green (11 node types,
subgraph node, 10 wires, save → load → structure equality).

## What GTK actually is

- **A built-in editor module, not a package.** Namespace `Unity.GraphToolkit.Editor`,
  assembly `UnityEditor` (types live in `UnityEditor.GraphToolkitModule`). The
  `com.unity.graphtoolkit` UPM id is a **deprecated shim — never add it to a manifest**.
  Editor assemblies need no asmdef reference to use it (like `UnityEditor` itself).
- Version matrix: 6000.4–6000.5 module without port capacity / type casting / wire+node
  visualization / USS node styling / element IDs; **6000.6 = the Graph API** (what we
  target); 6000.7 adds the State Machine API, `NodeView<T>`/`StateView<T>`,
  `[GraphMenu]`/`[BlackboardMenu]`, `GraphLogger.GraphChanges`, polymorphic ports.
- `Graph` derives from `System.Object` — it is **not** a `UnityEngine.Object`. Asset
  files wrap a main object of GTK's own type (see serialization below).

## API surface used by the spike (all compile- and run-verified on 6000.6)

- Graph: `[Graph(ext)]`, `[Graph(ext, GraphOptions.SupportsSubgraphs)]`, `[Serializable]`
  class : `Graph`; `GraphDatabase.CreateGraph<T>(path)` / `LoadGraph<T>(path)` /
  `LoadGraphForImporter<T>(path)` / `SaveGraph(graph)` /
  `PromptInProjectBrowserToCreateNewAsset<T>()`.
- Authoring graph API: `graph.AddNode(node)`, `graph.Connect(IPort out, IPort in)` →
  bool, `graph.AddSubgraphNode(Graph, Vector2)` → `ISubgraphNode`,
  `graph.GetNodes()`, `graph.NodeCount`, `graph.GetWire`, `graph.IsConnectionAllowed`,
  `graph.CreateVariable<T>` / `AddVariableNode` (not yet exercised),
  `graph.UndoBeginRecordGraph(string actionName)` / `UndoEndRecordGraph()`.
  **Every programmatic edit must sit inside the Undo pair** (also refreshes the view).
- Nodes: `[Serializable]` class : `Node`, `[Node("Folder/Path")]` for Add-menu
  placement (title = class name), `OnDefinePorts(IPortDefinitionContext)`:
  `AddInputPort[T](name)` / `AddOutputPort[T](name)` with `.WithCapacity(PortCapacity.Single)`
  (typed inputs default Single; **untyped ports and outputs default Multi**),
  `.AsVertical()`, `.WithDisplayName`, `.WithTooltip`, `.WithConnectorUI(PortConnectorUI.Arrowhead)`.
- Options (custom inspector fields): `OnDefineOptions(IOptionDefinitionContext)`:
  `AddOption<T>(name).WithDisplayName(..).WithDefaultValue(..).Delayed()`; read back
  with `GetNodeOptionByName(name).TryGetValue<T>(out ..)`. `Delayed()` avoids
  port-rebuild churn per keystroke. Enums and strings work as type args.

## Serialization model (on-disk evidence: `TestArtifacts/MainGraph.gtspike`)

- Plain **Unity YAML**. Main object: `GraphObjectImp`
  (`m_EditorClassIdentifier: UnityEditor.dll::Unity.GraphToolkit.Editor.Implementation.GraphObjectImp`).
  Graph content (models, nodes, wires) lives in **`[SerializeReference]` rid tables**
  (`GraphModelImp`, node/wire models, `Hash128` GUIDs).
- **RISK-06 input:** managed-reference rid tables are notoriously merge-hostile
  (insertions renumber subsequent rids). P6 torture tests must include concurrent
  edits to different nodes of one `.gtspike` file + git merge, before the Phase-2
  asset model freezes.
- Sub-assets (placemats, sticky notes, variables) serialize in the same file.

## The importer is OURS to write (important)

GTK ships **no ScriptedImporter** for custom extensions (module reflection found zero
ScriptedImporter types). The documented pattern — and the only way `.gtspike` stops
being a `DefaultAsset` — is a tool-provided importer:

```csharp
[ScriptedImporter(1, SpikeGraph.AssetExtension)]
class SpikeGraphImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        var graph = GraphDatabase.LoadGraphForImporter<SpikeGraph>(ctx.assetPath);
        // compile to a runtime ScriptableObject, ctx.AddObjectToAsset + SetMainObject
    }
}
```

After `ImportAsset(ForceUpdate)` the `.gtspike` main object becomes our compiled
`SpikeRuntimeGraph` (verified: `LoadMainAssetAtPath` type flip). **This importer is
the natural home of the Phase-2 "graph → runtime compile step with actionable
validation errors."**

## Opening the graph window programmatically

No public documented "open" API found. Working-but-incomplete path (verified):

1. `Unity.GraphToolkit.Editor.GraphObject.LoadGraphObjectAtPath(path, typeof(TGraph))`
   — public static, returns the wrapper (a `GraphObjectImp`).
   Note: the `(string)` overload is **generic** (`LoadGraphObjectAtPath<T>`); via
   reflection use the `(string, Type)` overload.
2. `Unity.GraphToolkit.Editor.Implementation.GraphViewEditorWindowImp.ShowGraph(wrapper)`
   — public static on an implementation-namespaced window class.

**Finding (three vision-reviewed runs):** `ShowGraph` opens a window titled with
the asset name, the grid canvas renders, focus/maximize work — but the populated
graph's nodes never appear (empty grid). The raw `ShowGraph` call is evidently not
the full open pipeline; the supported flow (double-click / `[CustomEditor]` "Edit"
button on the compiled runtime asset, mirroring Unity Behavior) is unverified and
is a **Phase-2 opening task**. Headless authoring/import/round-trip are unaffected
(test-proven).

## Gotchas hit (all real, this session)

1. `UndoBeginRecordGraph` **requires an actionName** argument (CS7036 otherwise).
2. The agent skill's doc URLs had drifted: real pages live under
   `manual/extending-the-editor/gtk-index/implementing-a-graph-tool/…` — fetch
   `gtk-index.md` first and follow its TOC.
3. Stale `.meta`: after the importer lands, existing graph files keep `DefaultImporter`
   until an explicit `ImportAsset(…, ForceUpdate)`.
4. `unity command script eval` **eats double quotes** in `--code`; avoid string
   literals in eval (or write a real script file + menu item).
5. CLI races after `Assets/Refresh`: menu/test commands issued during the recompile
   + domain reload fail (`menu not found`, `EDITOR_NOT_RUNNING`). Always
   `status --until-ready`, settle ~3 s, retry once.
6. GTK logs "A new asset is created at the same path…" when tests re-create graph
   assets — benign (see `docs/console-allowlist.txt`).
7. `LoadGraphObjectAtPath(string)` generic-vs-typed overload trap (above).

## 6.6 → 6.7 stability assessment (adapter-scope argument for the Phase-0 gate)

The spike deliberately avoids every 6.7-only API (`NodeView<T>`, `[GraphMenu]`,
`GraphChanges`, state machines, polymorphic ports). Our entire GTK surface is:
our own `[Serializable]` graph/node classes, `GraphDatabase`, `IPortDefinitionContext`,
`IOptionDefinitionContext`, one `ScriptedImporter`. Known soft spots carried into
Phase 2: `GraphObject`/`GraphViewEditorWindowImp` (implementation namespace,
reflection use), and the rid-table serialization format. Rebuild-on-6.7-beta is the
remaining P1 exit item (blocked on beta availability).

## Unity Behavior model comparison

Not yet inspected in-project (package not installed). Deferred to Phase-2 design:
install `com.unity.behavior`, read its PackageCache source under the Unity Companion
License for **patterns only** (RISK-02/RISK-11; see `docs/licenses.md`).
