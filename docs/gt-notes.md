# Graph Toolkit spike notes (R1 / P1)

Spike: `Assets/Spikes/GtSpike/` (throwaway, Editor+Runtime asmdefs, never referenced
from product assemblies). Verified on **Unity 6000.6.10f1** on 2026-11-23; round-trip
test `Gorecode.Bt.Spikes.GtSpike.Tests.SpikeGraphTests` is green (11 node types,
subgraph node, 10 wires, save → load → structure equality).

**Outcome (2026-11-24): closed, not adopted.** Graph Toolkit cannot attach a
decorator to a branch the way UE does (see "Decorators" below), so the maintainer
dropped the visual editor from v1; the reworked plan is proposal v0.3
(implementation-plan.md, decision record). The spike code stays as the evidence.

## What GTK actually is

- **A built-in editor module, not a package.** Namespace `Unity.GraphToolkit.Editor`,
  assembly `UnityEditor` (types live in `UnityEditor.GraphToolkitModule`). The
  `com.unity.graphtoolkit` UPM id is a **deprecated shim — never add it to a manifest**.
  Editor assemblies need no asmdef reference to use it (like `UnityEditor` itself).
- Version matrix: 6000.4–6000.5 module without port capacity / type casting / wire+node
  visualization / USS node styling / element IDs; **6000.6 = the Graph API** (what
  the spike targeted); 6000.7 adds the State Machine API, `NodeView<T>`/`StateView<T>`,
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
  placement (title = class name), or `[Node("Folder/Path", iconPath, title,
  stylesheet)]` — `title` is also the node's title once instantiated and
  `stylesheet` is a project path to a `.uss` file applied to the node's visual
  tree (`iconPath` accepts `null`), `OnDefinePorts(IPortDefinitionContext)`:
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
  (insertions renumber subsequent rids). Had an editor been adopted, this would
  have fed the serialization torture tests (concurrent edits to different nodes of
  one `.gtspike` file, then a git merge).
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
`SpikeRuntimeGraph` (verified: `LoadMainAssetAtPath` type flip). Under an editor
plan, a "graph → runtime compile step" would have lived here.

## Opening the graph window

**The supported path works.** Double-clicking the asset — equivalently
`AssetDatabase.OpenAsset` on its main object, which Graph Toolkit handles — opens
a graph window with all nodes and wires. Verified 2026-11-24 on 6000.6.10f1.

**Correction of the first pass (2026-11-23).** The earlier finding blamed a
reflection-based open (`GraphObject.LoadGraphObjectAtPath` +
`GraphViewEditorWindowImp.ShowGraph`) for an empty canvas. That was wrong: the
cause is the spike test re-creating the graph asset **at the same path while the
editor runs** (`GraphDatabase.CreateGraph` over an existing file). Graph windows
opened afterwards stay at "Graph Processing Pending" with no node views until the
editor restarts. Reproduced with the reflection path, with `OpenAsset`, with
horizontal and vertical ports, with and without node `OnEnable` overrides; gone
after a restart. In the stuck state the view's idle timer runs but processing
never completes. So:

- a headless loop of "run the test → open the graph → capture" needs an editor
  restart between the test and the capture;
- product tests must not re-create graph assets under an open editor if anyone
  is going to look at them (use fresh paths, or accept the restart).

Screenshot command (Pipeline package, no foreground window needed):

```
unity command capture editor_window --window "<window title>" --output-path Artifacts/screenshots/<branch>/<name>.png --project-path <project>
```

It captures at the window's size, brings the tab forward while reading and
restores the previous tab. `capture game_view` and `capture scene_view` exist
too.

In a fresh session the window can open zoomed far out. Frame-all is the `A` key in
the window; there is no public method for it, but the canvas (`GraphViewImp`, found
by type name under an `IGraphWindow`) takes the key directly: a `KeyDownEvent`
built from a `UnityEngine.Event` (the pooled event's `keyCode`/`character` setters
are not public) sent to that element. `SpikeGraphFrame` (menu `spikes/Fit graph to
window`) does this for every open graph window that shows node views, and logs the
node-view count and whether the pending label is shown.

The "Graph Processing Pending" label stays in the visual tree once processing is
done, only hidden — test its `resolvedStyle.display`, not its presence.

## Decorators: what Graph Toolkit can and cannot represent (2026-11-24)

Checked against the 6000.6 and 6000.7 manuals and API reference.

- **No edge-attached objects, in either version.** Nothing can hang on a wire the
  way a UE decorator hangs on a branch. The node kinds are standard, context
  (a container of block nodes), block, plus variable, constant and portal nodes.
- **Unity Behavior does not do it either**: its guards ("Conditional Guard
  (Modifier)") are inline nodes in the execution chain — the pattern this spike
  uses (`Cooldown → Move To`), which is what makes the tree look bloated next to
  UE.
- **6000.6 (project floor):** a node is a title, ports and fixed typed option
  fields. Decorators can only be separate chain nodes, or baked into a node type
  as option fields (not composable, not stackable in order).
- **6000.7** adds `NodeView<T>` — custom UI Toolkit controls inside a node, backed
  by serialized fields of the node class, with undo, zoom level-of-detail and
  culling hooks — plus custom right-click menu entries and per-data-type styles.
  With it, decorators and services can be a serialized list on the node they
  guard, drawn as a stack of rows with an add button. That is a **supported
  approximation** of UE's attached decorators, drawn inside the node card rather
  than on the wire. It is not available on the project floor.
- 6000.7 also adds a State Machine API whose transitions carry conditions; it does
  not apply to graphs.

Styling facts on 6000.6: `Node.Title` and `Node.Subtitle` are settable;
`Node.DefaultColor` colors the strip on the node's top border; `AsVertical()` puts
inputs on top and outputs at the bottom; `PortConnectorUI` is `Arrowhead` or
`Circle`. `Title`, `Subtitle` and `DefaultColor` are the only appearance-related
members `Node` declares (checked by reflection: no width, style or icon member).
A node card is a fixed 124 px wide, which leaves 80 px for the title and truncates
most of them; the per-type stylesheet is the fix — `SpikeNode.uss` sets
`width: auto; min-width: 220px` on `.ge-node` (other GTK class names seen:
`ge-node__title`, `ge-node__color-line`, `ge-node__vertical-port-container`). It
does not reach GTK's own subgraph node, whose title stays truncated.

## Gotchas hit (all real, this session)

1. `UndoBeginRecordGraph` **requires an actionName** argument (CS7036 otherwise).
2. The agent skill's doc URLs had drifted: real pages live under
   `manual/extending-the-editor/gtk-index/implementing-a-graph-tool/…` — fetch
   `gtk-index.md` first and follow its TOC.
3. Stale `.meta`: after the importer lands, existing graph files keep `DefaultImporter`
   until an explicit `ImportAsset(…, ForceUpdate)`.
4. `unity command script eval` strips plain double quotes in `--code`; write
   `\"` inside a single-quoted PowerShell string to pass a C# string literal
   (or write a real script file + menu item).
5. CLI races after `Assets/Refresh`: menu/test commands issued during the recompile
   + domain reload fail (`menu not found`, `EDITOR_NOT_RUNNING`). Always
   `status --until-ready`, settle ~3 s, retry once.
6. GTK logs "A new asset is created at the same path…" when tests re-create graph
   assets — benign (see `docs/console-allowlist.txt`).
7. `LoadGraphObjectAtPath(string)` generic-vs-typed overload trap (above).
8. `Node.OnEnable` is `public`; an override must be `public` too
   (`protected override` fails with CS0507).
9. A console check taken right after `Assets/Refresh` can miss compile errors
   that arrive seconds later. Confirm with `unity recompile` and by checking a
   fact that only the new code produces (a new menu item exists, new data in an
   asset).
10. `VisualElement.Query<T>()` is an extension method: `script eval` code has no
    `using` directives, so walk the tree there manually.
11. A single-quoted PowerShell string has no `\'` escape: a C# char literal in
    eval code fails to parse before Unity sees anything.
12. A graph window can come back after an editor restart as a blank page titled
    `UnnamedTool`; close it before capturing.

## 6.6 → 6.7 stability assessment (historical — the rebuild was not run)

R1 closed before the 6.7-beta rebuild, so this is the list as the spike left it,
not a verified diff.

The spike deliberately avoids every 6.7-only API (`NodeView<T>`, `[GraphMenu]`,
`GraphChanges`, state machines, polymorphic ports). The spike's entire GTK surface:
our own `[Serializable]` graph/node classes, `GraphDatabase`, `IPortDefinitionContext`,
`IOptionDefinitionContext`, one `ScriptedImporter`. The soft spots as the spike left
them: `GraphObject`/`GraphViewEditorWindowImp` (implementation namespace, reflection
use), and the rid-table serialization format.

## Unity Behavior model comparison

Not inspected in-project (package not installed), and no longer planned: plan v0.3
has no editor, so there is no graph model to compare against. Had it been read, it
would have been under the Unity Companion License and for **patterns only**
(RISK-02/RISK-11; see `docs/licenses.md`).
