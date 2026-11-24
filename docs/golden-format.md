# Golden scenario format — `unity-bt.golden/1`

Neutral JSON: trees, stimulus and expected traces are **data** (AGENTS.md: expected
traces are never hardcoded in C#). No C# type names, no UE identifiers — node types
use the working names of the spec's naming map (`docs/ue-bt-spec.md`).

Scenarios live in `Packages/com.gorecode.unity-bt/Tests/Golden/Scenarios/<section>.json`.
A file holds one scenario object or `{ "scenarios": [ ... ] }`. The runner
(`Tests/Editor/Golden/`) feeds each scenario to an `IGoldenRuntime`, records the
trace, and compares it with the expectation.

## Scenario

```json
{
  "schema": "unity-bt.golden/1",
  "id": "S19-lower-priority-abort",
  "title": "Human-readable summary",
  "statements": ["S19"],
  "requires": ["selector", "latentTask", "blackboardDecorator", "observerAbort.lowerPriority"],
  "compare": "segmented",
  "runners": 1,
  "tree": { "root": "sel", "nodes": { "...": {} } },
  "trees": { "extraTreeName": { "root": "...", "nodes": {} } },
  "blackboard": { "K": 0 },
  "stimulus": ["start", "tick", "write:K=1", "tick"],
  "expected": { "updates": [ ["..."], ["..."] ] }
}
```

| Field | Meaning |
|---|---|
| `statements` | Spec statement IDs the scenario exercises. A CI test fails if a traceable statement has none. |
| `requires` | Runtime capabilities needed. A runtime lacking any of them makes the scenario **skipped**, not failed — the suite doubles as a coverage dashboard while the runtime grows. `verify:Sxx` gates a scenario on the R7 sandbox resolution of that `[V]` statement. |
| `compare` | `segmented` (default): update boundaries matter (S07/S08). `flat`: event order only (timing-insensitive statements). |
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
`child` references (outermost first — the order S26 presumes for entry checks).

## Stimulus steps

| Step | Effect |
|---|---|
| `start` | Queue a root search (processed by the next update). Node memory persists (S30). |
| `stop` | Stop the runner: abort everything now (S41). |
| `tick` | Run exactly one update. Always produces a segment, even an empty one (S01). |
| `settle` | Run updates until no request is pending. |
| `write:K=value` | Write a key (value is JSON: `1`, `1.5`, `true`, `"text"`). Observers fire synchronously (S36). |
| `clear:K` | Clear a key; notifies like a write when the key was set (S35, S37). |
| `complete:id:Result` | A latent task finishes; its continuation is deferred to the next update (S08). |
| `advance:seconds` | Move the logical clock; due timers fire within this step. Randomness is never used (deviation pinned to 0, S33). |
| `inject:tag:treeName` | Inject a subtree into a dynamic subtree task (S40). |
| `breakpoint:id`, `step`, `resume` | Debugger controls (S43). |

**Segment rule:** `tick` (and each tick of `settle`) always opens a segment; every
other step opens a segment only if it emitted events.

## Trace events

| Event | Meaning |
|---|---|
| `id.activate` | Node activation: a task starts executing, a composite is entered |
| `id.finish:Success\|Failure\|Aborted` | Task finish (S28) |
| `id.complete:Success\|Failure` | Composite finished (composites emit nothing when aborted) |
| `id.abort` | Abort delivered to a task; followed by `id.finish:Aborted` (S29) |
| `id.pass` / `id.fail` | Decorator condition evaluation (entry checks and re-evaluations) |
| `id.observe` | An observer fired and re-evaluated its decorator |
| `id.tick` | Service tick |
| `id.count:N` | Activation counter of a `countActivations` task |
| `bb.write:K=value` / `bb.clear:K` | Blackboard mutation (S42) |
| `search:root` / `search:id` | A search started from the root / continued at composite `id` (S19, S48) |
| `search:discard` | A search was rolled back because it selected a running `KeepRunning` task (S49) |

Nested-tree nodes are addressed `subtreeId/nodeId`; injected root decorators as
`subtreeId#decoratorId`.

## Comparison

Timestamps never appear. `segmented` requires the same number of segments and the
same event sequence in each; `flat` compares the concatenated order. A mismatch
prints a per-update diff with missing/extra events.

## Harness conventions (ours, documented where the spec leaves a choice)

- Requests are processed in order; a root search in a batch supersedes every
  continuation in the same batch (S21's "single search after both").
- A Self abort continues at the parent as a failed child; a time limit continues as
  `Failure` (S24); observer aborts pass decorators as `Aborted` so ForceResult never
  converts them (S25).
- Aborted composites emit no event; aborted tasks emit `abort` then `finish:Aborted`.
- Observers of a composite's children stay registered after the tree runs out of
  nodes (S62, loop-execution mode).
