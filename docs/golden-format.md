# Golden scenario format — `unity-bt.golden/1`

Neutral JSON: trees, stimulus and expected traces are **data** (AGENTS.md: expected
traces are never hardcoded in C#). No C# type names, no UE identifiers — node types
use the working names of the spec's naming map (`docs/ue-bt-spec.md`).

Scenarios live in `Packages/com.gorecode.unity-bt/Tests/Golden/Scenarios/<section>.json`.
A file declares `"schema": "unity-bt.golden/1"` and holds one scenario object or
`{ "scenarios": [ ... ] }`. The runner (`Tests/Editor/Golden/`) feeds each scenario
to an `IGoldenRuntime`, records the trace, and compares it with the expectation.

## Scenario

```json
{
  "schema": "unity-bt.golden/1",
  "scenarios": [{
    "id": "S19-lower-priority-abort",
    "title": "Human-readable summary",
    "statements": ["S19"],
    "requires": ["selector", "latentTask", "blackboardDecorator", "observerAbort.lowerPriority"],
    "compare": "segmented",
    "loop": false,
    "pendingAtEnd": false,
    "runners": 1,
    "tree": { "root": "sel", "nodes": { "...": {} } },
    "trees": { "extraTreeName": { "root": "...", "nodes": {} } },
    "blackboard": { "K": 0 },
    "stimulus": ["start", "tick", "write:K=1", "tick", "settle"],
    "expected": { "updates": [ ["..."], ["..."] ] }
  }]
}
```

| Field | Meaning |
|---|---|
| `statements` | Spec statement IDs the scenario exercises. A test fails if a traceable statement has none. |
| `requires` | Runtime capabilities needed, from the closed list in `GoldenCapabilities` (a typo fails the suite instead of skipping forever). A runtime lacking any of them makes the scenario **skipped**, not failed — the suite doubles as a coverage dashboard while the runtime grows. `verify:Sxx` gates a scenario on the R7 sandbox resolution of that `[V]` statement. |
| `compare` | `segmented` (default): update boundaries matter (S07/S08). `flat`: event order only. |
| `loop` | `true` = loop-execution mode: an exhausted tree re-requests a root search every update (S62). Default `false` (single-run). `settle` is not allowed with `loop: true`. |
| `pendingAtEnd` | `true` allows the run to end with a request still pending. Otherwise the runner fails the scenario (a trace that silently stops mid-flight is a mistake). |
| `runners` | Number of tree instances (default 1). With more, stimulus steps may carry an `@k:` prefix and `expected` lists `runner1`, `runner2`, … |
| `tree` / `trees` | The main tree and named extra trees (for dynamic subtrees). |
| `blackboard` | Authored initial key values (S34). |
| `stimulus` | Ordered steps driving the run. |
| `expected.updates` | The expected trace: one array per update. |

## Tree nodes (`type`)

| Type | Fields |
|---|---|
| `Selector`, `Sequence` | `children: [ids]`; optional `unregisterBranchOnExit: true` (S58) |
| `SimpleParallel` | `main: id`, `background: id`, `finishMode: "Immediate" \| "Delayed"` |
| `BlackboardDecorator` | `child`, `key`, `query`, `notify`, `abort`. `query` is `"IsSet"`, `"IsNotSet"` or `{ "op": "eq\|ne\|gt\|ge\|lt\|le", "value": … }`. `notify`: `OnResultChange` (default) or `OnValueChange`. `abort`: `None` (default), `Self`, `LowerPriority`, `Both`. |
| `CooldownDecorator` | `child`, `seconds` |
| `LoopDecorator` | `child`, `mode: "Finite" \| "Infinite"`, `count` (default 3), `timeout` (seconds; negative = forever) |
| `TimeLimitDecorator` | `child`, `seconds` |
| `ForceResultDecorator` | `child`, `result: "Success" \| "Failure"` |
| `InstantTask` | `result` (default `Success`), `writes: { key: value }`, `countActivations: true` (emits `id.count:N`, S30) |
| `LatentTask` | `writes`, `duration` (auto-completes after N seconds), `restartPolicy: "Restart" \| "KeepRunning"` (S49), `abortDelay` (asynchronous abort, S29) |
| `Service` | `interval`, `restartTimerOnEachActivation`, `tickOnSearchStart`, `writes`; attached through `services: [ids]` on composites and tasks |
| `SubtreeTask` | `tree: { root, nodes }`, optional `rootDecorators: { id: {…} }` (S47) |
| `DynamicSubtreeTask` | `injectionTag`, optional `defaultTree` (a `trees` name) |

Decorators wrap their `child`; a chain of decorators on one node is a chain of
`child` references, outermost first — the order they are evaluated in (S26).

## Stimulus steps

| Step | Effect |
|---|---|
| `start` | Restart: queue a root search (processed by the next update). Node memory persists (S30). |
| `stop` | Stop the runner: abort everything now (S41). |
| `tick` | Run exactly one update. Always produces a segment, even an empty one (S01). |
| `settle` | Run updates until no request is pending. |
| `write:K=value` | Write a key (value is JSON: `1`, `1.5`, `true`, `"text"`). Observers fire synchronously (S36). |
| `clear:K` | Clear a key; notifies like a write when the key was set (S35, S37). |
| `complete:id:Result` | A latent task finishes; its continuation is deferred to the next update (S08). |
| `advance:seconds` | Move the logical clock; due timers fire within this step. Randomness is never used (deviation pinned to 0, S33). |
| `inject:tag:treeName` | Inject a subtree into a dynamic subtree task (S40). |
| `breakpoint:id` | Debugger: the next activation of node `id` pauses the run right before it; the update in which it would have activated ends there. |
| `step` | Debugger: run exactly one node's transition (the paused activation and its result), then pause again. |
| `resume` | Debugger: leave pause mode; the following `tick`/`settle` steps run normal updates. |

**Segment rule:** `tick` (and each tick of `settle`) always opens a segment; every
other step opens a segment only if it emitted events.

## Trace events

| Event | Meaning |
|---|---|
| `id.activate` | A search entered the composite, or a task started executing |
| `id.finish:Success\|Failure\|Aborted` | Task finish (S28) |
| `id.complete:Success\|Failure` | Composite finished (composites emit nothing when aborted) |
| `id.abort` | Abort delivered to a task; followed by `id.finish:Aborted` (S29) |
| `id.pass` / `id.fail` | Decorator condition evaluation (entry checks and observer re-evaluations) |
| `id.observe` | An observer fired and re-evaluated its decorator |
| `id.tick` | Service tick |
| `id.count:N` | Activation counter of a `countActivations` task |
| `bb.write:K=value` / `bb.clear:K` | Blackboard mutation (S42) |
| `search:root` | A root search (start, restart, or the re-request after exhaustion in looped mode) |
| `search:id` | A search at composite `id`: the continuation after a child's result, or an observer-triggered search limited to one of its branches |
| `search:discard` | A search was rolled back because it selected a running `KeepRunning` task (S49) |

Nested-tree nodes are addressed `subtreeId/nodeId`; injected root decorators as
`subtreeId#decoratorId`.

## Update model (UE 5.8, source-verified — `BehaviorTreeComponent.cpp` unless noted)

Expected traces are derived from these rules; the naive reference runtime
implements exactly them.

1. **At most one task per update.** A search only locates the next task
   (2094-2154); the current task is aborted if the search moved away from it;
   then at most one task executes (2200-2222). A task's result — instant or
   latent — is a request for the *next* update (557-561, 1144-1150) — S07, S08.
2. **Event order inside an update:** search events first (`search:…`, composite
   activations as the search enters them, decorator `pass`/`fail`), then the
   abort of the abandoned task, then composite completions, then the new task's
   execution. Composite completion carries the result that ended it (a child's
   success for a selector, a child's failure for a sequence) or its default
   when it ran out of children.
   *Mapping rule for differential comparison:* UE decides a composite's result
   during its search (2106-2116) and records it before the abort; this trace
   emits the completion after the abort. Compare node-level results, not the
   position of completion markers relative to abort markers.
3. **Requests are not merged.** Several pending requests → the one with the
   highest-priority search start is processed; the others are skipped, but a
   skipped request widens the processed search's range (1256-1281) — S08, S21.
4. **Searches are range-limited.** A continuation and a root search may select
   anything below their start; an observer-triggered search may select only
   inside the decorated branch. A task found beyond the range invalidates the
   search, which is rolled back with nothing aborted (2156-2160, 2181-2186);
   no task at all is a valid, exhausting search — S19.
5. **Observer arming** (`BTCompositeNode.cpp` 227-250, 281-292, 297-315):
   Self/Both observers register when a search enters their branch, LP/Both
   observers when a search finds the branch failing at entry; Self observers
   unregister when the branch is left, LP observers re-arm. A branch no search
   reached is not armed.
6. **Observer changes are applied when the search's data is applied** — after
   the abort of the task the search replaces (2245-2254), and never for a
   search that found no task (1498-1508). Observers positioned below the new
   task are removed at every task execution (2245-2248, 2390-2406), including
   loop re-iterations and continuations.
7. **Observer requests** (`BTDecorator.cpp` 88-120; 990-1023): every
   notification evaluates the condition. A failing result asks the parent to
   continue with a failed child; a passing result asks for a search limited to
   the decorated branch — and with `OnValueChange` also restarts an executing
   branch. No previous result is stored.
8. **Discard:** a search that selects a running `KeepRunning` task is rolled
   back (2162-2186); the search events it already produced stay in the trace.
9. **Finish** (585-613): when the root composite ends with any result, every
   auxiliary node is unregistered; looped mode re-requests a root search,
   single-run mode stops — S62.
10. **Slow aborts** (1432-1454, 1776-1800): nothing is searched while a task
    is aborting; the tree keeps its state. When the abort completes, the
    outcome computed before it is applied without a new search; a request that
    arrived during the abort discards that outcome and is searched instead,
    with its range limit removed — S29, S50.

Not modeled by the naive runtime (unexercised; extend before a scenario needs
them): a restart passing through a running nested composite deactivates and
re-activates it (2336-2348); composite services, which activate with the
applied search data after the abort (2254).

## Comparison

Timestamps never appear. `segmented` requires the same number of segments and the
same event sequence in each; `flat` compares the concatenated order. A mismatch
prints a per-update diff with missing/extra events.

## Not yet expressible (known gaps, extend before the statements gate tests)

- Unordered event groups (S13, S50 abort ordering) — currently handled with
  `verify:` gates.
- Typed blackboard keys, log assertions (S40), and a per-runner event stream for
  `runners > 1`.
- Tracing a replayed run as a second stream (S42's replay assertion itself; the
  scenario only pins the original trace).
- Runtime lifecycle beyond `Load`: no unload, and the step/event protocol is
  plain strings rather than typed values.
