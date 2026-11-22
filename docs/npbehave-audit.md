# NPBehave Audit — R3 deliverable (decision recorded)

**Date:** 2026-11-22 · **Source:** `meniku/NPBehave` @ `a1bc967`, cloned & inspected.

## Verified facts

| Fact | Value |
|---|---|
| Last release-era commit | `a1bc967` — **2022-11-26** ("Add Example Scene for NavMoveTo task") |
| Stars | 1,277 |
| Runtime size | ~3,330 LOC C# (excluding examples/editor) |
| Tests | 14 editor test files (Clock, Blackboard, general) |
| Assembly layout | single flat `Scripts/` folder, **no asmdef**, no package structure |
| License | GitHub API reports **MIT**, but **no LICENSE file at repo HEAD** — provenance must be confirmed with the author before adopting any code |

## Architecture findings

1. **Code-built trees.** Trees are composed via constructors at runtime, per agent.
   No asset model, no editor-authoring path. Construction allocates per agent.
2. **Decorator-as-wrapper.** Decorators *wrap* a decoratee `Node` (parent chain),
   unlike UE's guards attached *to* a node. `LOWER_PRIORITY` stops are implemented by
   stopping the parent composite (`DoParentCompositeStopped`) — semantically related
   to UE observer aborts, structurally different from UE's priority abort search.
3. **`Stops` enum is a UE superset:** `NONE, SELF, LOWER_PRIORITY, BOTH,
   IMMEDIATE_RESTART, LOWER_PRIORITY_IMMEDIATE_RESTART` — the two `IMMEDIATE_RESTART`
   variants have no direct UE counterpart. Strong conceptual reference for abort
   semantics (and a candidate extension list for us).
4. **Blackboard:** `Dictionary<string, object>` storage (boxes value types; string
   keys → runtime string ops), observers as `List<Action<Type,object>>` (closure
   allocations on registration), deferred notification dispatch via a `Clock` timer.
   Notable original feature: **parent/child blackboard scoping** (scoped lookups) —
   worth stealing as a *concept* (candidate post-v1 extension).
5. **Clock-driven scheduling** via a `UnityContext` MonoBehaviour; throttling example
   exists. Clean idea (per-agent update budget), allocation-heavy implementation.
6. **Parallel is a classic N-child parallel** (success/failure policies) — **not** UE
   Simple Parallel (one main task + one background tree). Direct semantics gap vs our
   spec S09–S14.
7. **Services exist** (`Service` wrapper with interval) — maps conceptually to UE
   services.
8. **No zero-alloc discipline:** boxing, closure observers, per-agent tree
   construction; no banned-construct rules. No LINQ (verified: zero `System.Linq`
   imports in runtime code).
9. **No node-instancing model** distinct from per-agent construction (every node is a
   per-agent object).
10. **Basic debugger exists** (`Debugger.cs` + `Editor/DebuggerWindow.cs`) — log-level
    visualization only; nothing close to execution-history replay.

## Mapping to our spec

| Spec area | NPBehave | Verdict |
|---|---|---|
| Event-driven core (S01–S08) | yes, via observers + clock | concept reusable, implementation not |
| Selector/Sequence (S02–S06) | yes | trivial |
| SimpleParallel (S09–S14) | **no** | must be built from spec |
| Guard decorators + abort modes (S15–S21) | analogous via `Stops` + wrappers | semantics reference; structure wrong |
| Cooldown/Loop/TimeLimit (S22–S24) | yes (Cooldown, Repeater, TimeMax/TimeMin) | semantics reference |
| Services (S32–S33) | yes | concept reusable |
| Tasks lifecycle (S27–S31) | callback-based, no abort-completion semantics parity | partial reference |
| Blackboard typed keys (S34–S38) | string→object, boxed | must be rebuilt (struct keys) |
| Subtrees (S39–S40) | example-level reuse patterns only | must be built |
| Debugger (S42–S44) | log window | must be built |
| Zero-alloc (S38/S44/S45) | violated by design | must be rebuilt |

## Decision

**Spec-led reimplementation.** NPBehave is adopted as a *conceptual reference* —
its `Stops` semantics, service pattern, and blackboard-scoping idea inform our
runtime and its README examples feed the golden-test scenario list — but **no code is
adopted**:

1. Architecture misalignment: wrapper decorators and code-built trees cannot carry an
   editor-first, asset-serialized product (our Phase 2 depends on nodes as data).
2. Allocation profile violates the zero-alloc invariant (boxing, closures, per-agent
   construction) — retrofitting would be a rewrite anyway.
3. License provenance (no LICENSE file at HEAD) makes code adoption a legal question
   with no upside given 1–2.

**Consequences for the plan:** R3 exit satisfied (decision recorded). Phase 1 builds
the runtime from `ue-bt-spec.md` directly; NPBehave's example scenarios are folded
into the P3 golden-test corpus alongside the R7 sandbox scenarios.
