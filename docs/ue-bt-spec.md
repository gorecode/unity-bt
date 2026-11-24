# UE5 Behavior Tree — Formal Specification (v1.0.1)

**Status:** v1.0 revised 2026-11-23 after the R2 second-agent review (findings
B1–B11, N1–N10) and verification against UE 5.8 engine source (tag
`5.8.0-release`, commit `7deeb413`); v1.0.1 (2026-11-24) applies the erratum
found by the P3 harness review (see the changelog). **Pending:** human
maintainer sign-off (R2 gate).

Engine source was read as *behavioral spec only* (AGENTS.md legal rule):
statements below describe UE semantics in this project's own words. Citations
`[src: file:line]` refer to that pinned checkout (paths relative to
`Engine/Source`, modules `Runtime/AIModule` and `Editor/BehaviorTreeEditor`
unless stated). Epic doc pages cited were fetched 2026-11-23.

## Statement format

Every statement: `ID | statement | confidence tag | stimulus & expected trace sketch`.

- A statement is *decidable* iff a golden test can encode the stimulus and assert
  the expected trace (node activation/deactivation/result sequence + blackboard
  mutations; timestamps ignored — "equality modulo timing").
- A statement may cross-reference an earlier sketch (`*(covered by S02's
  stimulus)*)` instead of repeating it.
- Statements marked *structural* are enforced by authoring-validation tests;
  statements marked *bench-gated* are asserted by performance/allocation tests,
  not trace tests.

Confidence tags: **[D]** = documented in Epic's public docs; **[K]** = established
community/engine knowledge, not contradicted by docs (v1.0: several are
engine-source-verified; citation given); **[V]** = still requires sandbox (R7)
confirmation before it may gate a test; **[ours]** = this project's own invariant
(no UE counterpart).

## Naming map (working names — final public naming is the Phase-0 decision, RISK-12)

| UE5 concept | Working name (this project) |
|---|---|
| Behavior Tree asset | `TreeAsset` |
| Behavior Tree Component / Brain (per agent) | `TreeRunner` |
| Root node / asset-level root decorators | `RootNode` / root decorators |
| Composite — Selector / Sequence | `Selector` / `Sequence` |
| Composite — Simple Parallel | `SimpleParallel` |
| Decorator (node-attached guard) | `Decorator` (subtypes: `BlackboardDecorator`, `CooldownDecorator`, `LoopDecorator`, `TimeLimitDecorator`, `ForceResultDecorator`) |
| Observer Aborts (None / Self / Lower Priority / Both) | `AbortMode.None / Self / LowerPriority / Both` |
| Ignore Restart Self (per-task flag) | `RestartPolicy` (`Restart` / `KeepRunning`) |
| Notify Observer (blackboard decorator re-evaluation mode) | `NotifyMode` (`OnResultChange` / `OnValueChange`) |
| Key Query (Is Set / Is Not Set) | `KeyQuery` (`IsSet` / `IsNotSet`) |
| Apply Decorator Scope (composite option) | `UnregisterBranchOnExit` |
| Dynamic subtree injection tag | `InjectionTag` |
| Task (instant / latent) | `InstantTask` / `LatentTask` |
| Service | `Service` |
| Blackboard / blackboard keys | `Blackboard` / typed `BlackboardKey<T>` |
| Node memory (per-runner state block) | `NodeMemory` |
| Run Behavior / Run Behavior Dynamic | `SubtreeTask` / `DynamicSubtreeTask` |
| Behavior tree execution history | `ExecutionTrace` |

## Typed blackboard key catalog (exact Unity types — product set)

`bool`, `int`, `float`, `double`*, `string`, `UnityEngine.Vector2`*,
`UnityEngine.Vector3`, `UnityEngine.Quaternion`, enum (int-backed),
`UnityEngine.Object` (assignable with type filter).

Decisions (v1.0):

- UE `Name` maps to `string` — one string kind; UE's Name/String distinction has
  no behavior we mirror.
- UE `Rotator` maps to `Quaternion`; the editor documents the degrees-Euler
  bridge (authoring parity).
- `Vector2` has no UE blackboard counterpart → **[ours]** extension, flagged
  like ForceFailure. `Quaternion` is the mapped counterpart of `Rotator`.
- UE `Class` (class-filtered type reference): deferred post-v1; `Object` with a
  type filter covers the common cases.
- UE `Struct` (arbitrary instanced structs): deferred post-v1 alongside
  `Class`; the catalog's enumerated types cover v1 needs.
- `double` provisional **[V]**: UE 5.8 ships no double key type as far as we
  can establish (Int, Float, Bool, String, Name, Vector, Rotator, Enum, Object,
  Class, Struct) — confirm in sandbox whether parity needs it; drop if not.
- String design rule (**[ours]**): string *values* are immutable shared
  references; no allocation, hashing or comparison in the tick path (AGENTS.md
  invariant 1). Key *identifiers* are compile-time symbols — a separate concept
  from the string value type.

---

## A. Scheduler & execution model

- **S01 [K]** — Updates are event-driven; there is no per-frame traversal. A
  search runs only on: tree start/restart; latent-task completion; abort
  completion; blackboard observer firing; timed re-requests (interval ticks of
  active services/timed decorators that re-request execution); explicit
  request. When nothing is pending, the component does not tick at
  all. *(src-verified: the search flag is set only by execution requests
  [src: BehaviorTreeComponent.cpp:1804-1812]; idle tick is disabled
  [src: BehaviorTreeComponent.cpp:38-39].)*
  *Stimulus:* latent task InProgress, several frames pass → *Trace:* no node
  transitions.
- **S02 [D]** — Search evaluates root-down; child order at each composite
  defines priority (leftmost = highest).
  *Stimulus:* selector with [A(fail), B(success)] → *Trace:* A activated→failed;
  B activated→succeeded; selector succeeded.
- **S03 [D]** — Selector: fails only when all children fail; succeeds on first
  child success. *(covered by S02's stimulus)*
- **S04 [D]** — Re-entry: a new search that selects a branch which is currently
  executing re-enters it; a running latent task is NOT re-executed **iff** the
  task sets `RestartPolicy.KeepRunning` — with the default (`Restart`) the task
  is aborted and re-executed. See S49. *(covered by S49's stimulus)*
- **S05 [V]** — Residual restart cases beyond S04/S49: decorator re-check
  failures between loop iterations, and any node classes overriding the restart
  default. Sandbox: LP abort → re-enter same branch with changed conditions.
  *Stimulus:* loop re-iteration whose guard flips false between iterations →
  *Trace:* branch exits; parent observes the child's result per S23.
- **S06 [D]** — Sequence: children in order; any child failure fails the
  sequence immediately; all succeed → success; a running latent child parks the
  sequence at its position.
  *Stimulus:* [A(2s latent, success), B(instant success)] → *Trace:* A
  activated→running; (2s) A succeeded; next update: B activated→succeeded;
  following update: sequence succeeded.
- **S07 [K]** — A search finds at most one task, and exactly one task executes
  per update. An instant task returns its result within that update; the
  continuation at its parent is a request like any other and is processed by
  the next update — a chain of instant tasks takes one update per task, plus
  one for the composite's completion. *(src-verified: the search loop only
  locates the next task [src: BehaviorTreeComponent.cpp:2094-2154]; abort and
  execute happen once per update [src: BehaviorTreeComponent.cpp:2200-2222]; a
  task result only schedules an update [src: BehaviorTreeComponent.cpp:557-561,
  1144-1150].)*
  *Stimulus:* sequence of three instant tasks → *Trace:* update 1: root search,
  sequence activated, A activated→succeeded; update 2: B activated→succeeded;
  update 3: C activated→succeeded; update 4: sequence completed.
- **S08 [K]** — Deferred execution: every request (task result, observer,
  timer, explicit) is queued and processed on the component's next update,
  never inline with the triggering event. When several requests are pending
  for one update, the one whose search start has the highest priority is
  processed and the others are skipped. *(src-verified [src:
  BehaviorTreeComponent.cpp:1144-1150, 1804-1812; priority rule
  1256-1281].)*
  *Stimulus:* an instant task fails inside a selector → *Trace:* its finish is
  in update N; the selector's next child activates in update N+1.
  *Residual [V]:* interleaving of an observer request with the completion of
  the task whose update fired it (S36).
- **S46 [D]** — The Root is unique, has exactly one child, and only a Composite
  may be attached to it; every search begins at the Root.
  *Stimulus:* tree start → *Trace:* the root's composite is the first node
  activated.
- **S48 [K]** — Continuation vs search: on task completion (Success/Failure),
  execution continues locally at the parent composite (next child /
  fail-over); decorators on ancestors above that parent are not re-evaluated —
  ancestor conditions are enforced reactively by observer aborts, not by
  continuation. Only start/restart triggers a root-down search; observer
  aborts trigger a search limited to the affected branch (S19). *(src-verified
  [src: BehaviorTreeComponent.cpp:517-583, 1067-1080, 1389-1394].)*
  *Stimulus:* sequence [A(latent success), B] with a passing-at-entry guard on
  the sequence that later flips false (`AbortMode.None`); A completes →
  *Trace:* next update: B activated (guard not re-checked).

## B. Composites

- **S09 [D]** — `SimpleParallel` has exactly two children: main task +
  background subtree; both activate together. The main slot accepts task nodes
  only (editor schema).
  *Stimulus:* parallel start → *Trace:* main task activated and background
  branch root activated in one update.
- **S10 [D]** — Finish mode **Immediate**: main task finishing aborts the
  background branch; parallel result = main result. *(result mapping [K],
  src-verified [src: BTComposite_SimpleParallel.cpp:110-126].)*
  *Stimulus:* main succeeds at t=1s, background running → *Trace:* background
  aborted; parallel succeeded.
- **S11 [D]** — Finish mode **Delayed**: main task finishing lets the background
  finish naturally; parallel result = main result, delivered after background
  completes. *(result mapping [K], src-verified [src:
  BTComposite_SimpleParallel.cpp:74-97].)* Special case **[V]**: an
  instantly-succeeding main task with Delayed forces one background run before
  finishing [src: BTComposite_SimpleParallel.cpp:98-106] — sandbox-confirm the
  observable trace.
  *Stimulus:* main succeeds at t=1s, background finishes at t=3s → *Trace:*
  parallel succeeds at t=3s with main's result.
- **S12 [K]** — The engine imposes no structural rule against nesting a
  `SimpleParallel` inside a background branch (no editor or runtime check
  exists; the main pin is task-only [src:
  EdGraphSchema_BehaviorTree.cpp:265-268]). Behavior of such trees is
  unspecified in UE. **[ours]**: our editor warns on nested parallels; runtime
  behavior undefined. *Structural* — enforced by authoring-validation tests.
- **S13 [K]** — Aborting a `SimpleParallel` aborts both children (main via the
  parallel-task removal path, background via branch deactivation). *(src-verified
  [src: BTComposite_SimpleParallel.cpp:110-126;
  BehaviorTreeComponent.cpp:1542-1571].)*
  *Stimulus:* observer abort on the parallel's parent while both sides run →
  *Trace:* both children deliver Finish(Aborted) (order between them: S50's
  [V]); parallel Finish(Aborted); new search.
- **S14 [V]** — Background branch finishing while the main task still runs:
  presumed no effect on the node (the main task keeps running), but the source
  shows the parallel re-selecting its background child on the next search
  [src: BTComposite_SimpleParallel.cpp:29-35] behind a same-search retry guard
  [src: BTComposite_SimpleParallel.cpp:43-47] — whether the background restarts
  before the main task ends is unresolved. Sandbox: background (1s) shorter
  than main (3s).
  *Stimulus:* background (1s) shorter than main (3s) → *Trace (presumed):*
  background finished at t=1; no further transitions until main finishes at
  t=3.
- **S50 [K]** — Latent-abort completion ordering: a branch switch waits until
  all in-flight latent aborts deliver Finish(Aborted) — including both children
  of a `SimpleParallel` and nested subtree tasks — before any new node
  activates. Ordering *between* simultaneous aborts: **[V]**.
  *Stimulus:* observer abort on the parent of a parallel with two slow-aborting
  children → *Trace:* both children Finish(Aborted); only then the new branch
  activates.
- **S58 [D]** — Composite option (UE "Apply Decorator Scope"; `SimpleParallel`
  defaults it on): when execution flow leaves the composite, all decorators and
  services in the branch below are unregistered (their observers cease);
  decorators on the composite itself are unaffected. *(src-verified [src:
  BTCompositeNode.h:196-198, BTCompositeNode.cpp:190-217].)*
  *Stimulus:* scoped selector; flow moves to a sibling branch → *Trace:*
  descendants' observers cease (no later aborts from them); sibling execution
  unaffected.

## C. Decorators (guards)

- **S15 [D]** — Decorators attach to composite and task nodes; multiple
  allowed; **all** must pass (AND) for branch entry. The Root carries
  asset-level decorators with subtree-only semantics (S47).
  *Stimulus:* selector [A(two passing guards), B] → *Trace:* A runs (both
  guards checked in one update).
- **S16 [D]** — A decorator failing at entry = branch not entered; the parent
  reacts as if the child failed (selector: next child; sequence: fail).
  *Stimulus:* selector [A(guard fail), B] → *Trace:* A never activates; B runs.
- **S17 [D]** — `AbortMode.None`: condition checked only at branch entry.
  *Stimulus:* guard passes at entry, flips false later, None mode → *Trace:*
  no abort; branch runs to completion.
- **S18 [D]** — `AbortMode.Self`: while the branch executes, condition→false
  aborts the branch; the parent then continues as if the child had failed (the
  request carries Failed: a sequence fails, a selector moves on). *(src-verified
  [src: BehaviorTreeComponent.cpp:972].)*
  *Stimulus:* running task under a Self-guard inside a sequence; observed key
  flips → *Trace:* next update: task abort + Finish(Aborted); sequence
  completed with Failure.
- **S19 [D]** — `AbortMode.LowerPriority`: while the decorated branch is *not*
  executing, a change that flips the decorator's observed *result* (per its
  `NotifyMode`, S54) triggers a search **limited to the decorated branch**
  (starting at its parent composite, bounded to that child): lower-priority
  execution is aborted and, if the branch passes its entry checks, it executes.
  The rest of the tree is not re-evaluated. *(src-verified: bounded range
  [src: BehaviorTreeComponent.cpp:1246-1252, 2156-2160].)* Arming: an LP
  observer is registered only once a search has **reached** its branch and
  found it failing (or after leaving it); branches a search never reached are
  not armed *(src: BTCompositeNode.cpp:227-250, 287-292, 297-315].)*
  *Stimulus:* branch1(guard on key K, LP) failed at entry, branch2 running; K
  flips so branch1's guard now passes → *Trace:* next update: search at the
  parent; guard re-checked; branch2's task abort + Finish(Aborted); branch1's
  task executes.
- **S20 [D]** — `AbortMode.Both` = Self + LowerPriority simultaneously: the
  observer is registered while its branch executes (armed on activation), when
  the branch fails at entry, and after the branch is left. *(src:
  BTCompositeNode.cpp:235-248, 281-292, 307-311].)*
  *(covered by S18 + S19 stimuli combined)*
- **S21 [K]** — Observers notified by one write each queue a request; the
  request whose search start has the highest priority is processed and the
  others are skipped — only the first branch is attempted, never a merged
  search. UE imposes **no defined ordering** among the notified observers
  themselves (multimap iteration; [src: BlackboardComponent.cpp:426-484]);
  **[ours]**: our runtime notifies in registration order so golden tests can
  assert it (not differentially comparable). *(src-verified priority rule
  [src: BehaviorTreeComponent.cpp:1256-1281].)*
  *Stimulus:* one write hitting two LP guards on different branches of one
  selector while a lower-priority task runs → *Trace:* both guards
  re-evaluated; next update: one search at the selector; running task aborted;
  the first guard's branch executes.
  *Residual [V]:* UE's own notification order is unspecified; sandbox captures
  can only sample it.
- **S22 [K]** — Cooldown: the cooldown window starts when the branch
  **deactivates** (last use = branch exit), not on activation; during the
  window the decorator's condition is false (branch blocked, parent treats it
  as failed); with an abort mode set the decorator stays relevant and
  re-requests execution when the window elapses; remaining-time state is
  per-runner `NodeMemory`. *(src-verified [src:
  BTDecorator_Cooldown.cpp:12-88].)*
  *Stimulus:* branch runs and exits; re-entry attempted within the window →
  *Trace:* decorator false, branch not entered; (window elapsed) decorator
  true, branch entered.
- **S23 [K]** — Loop: `NumLoops` (finite, default 3, clamp 1–255) re-activates
  the branch regardless of the child's result — child failure does **not**
  break the loop; infinite mode repeats until `InfiniteLoopTimeoutTime`
  (negative = forever) elapses; when the loop stops, the child's last result
  passes through unchanged (the loop never rewrites results). A single search
  can never loop: same-search retries are refused. Loop decorators cannot
  carry observer abort modes. *(src-verified [src: BTDecorator_Loop.h:26-36;
  BTDecorator_Loop.cpp:22-66; BTDecorator_Loop.cpp:17-19 for the abort
  restriction].)*
  *Stimulus:* finite loop N=2 over a failing instant task → *Trace:* task
  activated→failed; (next update) task activated→failed; parent observes
  Failure. Each re-iteration is continuation-triggered (S48) and lands in its
  own update segment (S07/S08).
- **S24 [K]** — TimeLimit: when the limit expires while the branch executes,
  the branch is stopped and **fails** (Failure delivered to the parent); the
  timer resets to the full window every time the node regains focus (each new
  relevance period). *(src-verified [src: BTDecorator_TimeLimit.cpp:13-87].)*
  *Stimulus:* limit 2s, branch re-entered after an abort at t=1.8s → *Trace:*
  full new 2s window before Failure.
- **S25 [K]** — ForceSuccess converts Failed→Succeeded at completion, **and
  also the Failed result a Self (observer) abort delivers to the parent**: a
  Self abort is processed as a regular deactivation carrying Failed, so the
  parent sees Succeeded and a force-success branch never falls through to the
  next one. Only a literal `Aborted` result (a branch switched away by a
  search) passes through unconverted. *(src-verified: "we only modify result
  if search originates from our parent node (abort self) or on our associated
  node (failure)" [src: BTDecorator_ForceSuccess.cpp:21-43; abort path
  BTCompositeNode.cpp:252-265].)*
  **[ours]** `ForceResultDecorator` additionally offers ForceFailure — our
  extension, no UE counterpart (the engine has no ForceFailure node).
  *Stimulus:* task under ForceSuccess + Self guard inside a selector; guard
  flips → *Trace:* next update: task abort + Finish(Aborted); selector
  completed with Success; the next branch does not run.
- **S26 [K]** — Decorators on one node are evaluated in attachment (array)
  order with short-circuit: the first failing decorator stops the check and
  the branch is not entered. *(src-verified [src:
  BTCompositeNode.cpp:454-478].)*
  *Stimulus:* node with [guard1(fail), guard2(pass)] → *Trace:* guard1
  evaluated → fail; guard2 not evaluated; branch not entered.

## D. Tasks

- **S27 [D]** — Tasks are the only leaves; instant tasks return a terminal
  result synchronously; latent tasks return InProgress and complete via an
  explicit completion call (possibly many updates later).
  *Stimulus:* latent task started → *Trace:* activated; InProgress; (later
  update) terminal result.
- **S28 [K]** — Lifecycle: activation → Execute → optional per-update Tick while
  running → Finish(∈ {Success, Failure, Aborted}) exactly once per activation.
  *Stimulus:* latent task completing normally → *Trace:* activated; running;
  succeeded.
- **S29 [K]** — Abort path: an abort request calls the task's abort hook; the
  task may finish the abort immediately or asynchronously; the tree does not
  proceed past the aborting task until its abort completes; Finish(Aborted) is
  delivered.
  *Stimulus:* abort requested on a latent task with a slow abort → *Trace:*
  abort hook invoked; (delay) Finish(Aborted); tree proceeds only after.
- **S30 [K]** — `NodeMemory` persists across abort and re-entry for as long as
  the owning tree instance lives; it is not wiped by restarts. Tasks
  re-initialize the fields they care about at activation. *(src-verified [src:
  BehaviorTreeComponent.cpp:2755-2765, 2257-2265;
  BehaviorTreeTypes.cpp:114-122].)*
  *Stimulus:* task with counter memory runs, exits, is re-entered → *Trace:*
  second run observes the first run's stored value (task-defined behavior).
- **S31 [K]** — UE's instancing model (corrected): node objects are shared
  templates — one duplicate per tree asset per manager, shared by all runners;
  per-runner state lives in `NodeMemory` blocks; per-runner node *objects*
  exist only for nodes opting into instancing (UE's Blueprint-based nodes,
  PlayAnimation, RunBehaviorDynamic; composites can never be instanced). The
  UE4-era `EBTNodeMemoryType` no longer exists in 5.8. *(src-verified [src:
  BTNode.h:257-259; BehaviorTreeManager.cpp:263-317; BTNode.cpp:90-118].)*
  **[ours]**: our runtime uses per-runner node instances as the implementation
  mechanism instead; the observable contract is S60 (cross-agent isolation).
  *Structural* — no golden trace of its own.
- **S49 [D]** — `RestartPolicy` (UE "Ignore Restart Self", default `Restart`):
  when a search selects a task that is already running, with `KeepRunning` the
  whole search update is discarded (rolled back) and the task continues; with
  `Restart` the task is aborted and re-executed. No UE built-in class changes
  the default. *(src-verified [src: BTTaskNode.h:94-96;
  BehaviorTreeComponent.cpp:2162-2186].)*
  *Stimulus:* a restart (tree restart, or the re-request of an OnValueChange
  guard) lands on the running task; `KeepRunning` → *Trace:* no re-activation
  (search discarded); `Restart` → *Trace:* Finish(Aborted), then Execute
  again.
- **S60 [K]** — Cross-agent isolation: two runners on one `TreeAsset` have
  independent blackboards and independent per-runner state (cooldown timers,
  loop counters); nothing leaks between agents. (UE achieves this with shared
  templates + per-component memory (S31); we achieve it with per-runner
  instances — the observable contract is identical.)
  *Stimulus:* two runners; A finishes a Cooldown-decorated branch; both trees
  restarted immediately → *Trace:* A blocked by cooldown, B executes.

## E. Services

- **S32 [D]** — Services attach to Composite **and** Task nodes. A composite
  service is active while execution is inside the composite; a task service
  becomes active when the task starts executing and ceases when the task's
  branch deactivates. *(src-verified [src: BTTaskNode.h:88-90;
  BehaviorTreeComponent.cpp:2468-2487; BTCompositeNode.cpp:135-188].)*
  *Stimulus:* service (0.5s interval) on a 2s latent task inside a sequence →
  *Trace:* service activates with the task, ticks ~3 times, deactivates when
  flow leaves the task's branch (branch deactivation, not the Finish call
  itself).
- **S33 [D]** — While active, a service ticks at its configured interval (UE
  default 0.5s — [K], an editor/API default), optionally randomized; it also
  receives search-start/finish and activation callbacks. Options: "Call Tick on
  Search Start" runs one immediate tick (dt = 0) when a search enters the node;
  "Restart Timer on Each Activation" resets the countdown to a full interval at
  every (re)activation. *(src-verified [src: BTService.h:58-64;
  BTService.cpp:44-63].)* *Norm (ours):* golden scenarios pin random deviation
  to 0.
  *Stimulus:* two searches through the node within one interval, restart option
  on vs off → *Trace:* tick counts differ accordingly.
  *Residual [V]:* exact callback set and ordering vs child execution.
- **S53 [V]** — Service continuity: a re-search that re-selects the currently
  active path does not deactivate/reactivate services on that path nor reset
  their interval timers (S33 options excepted). Presumed: continuous.
  *Stimulus:* LP abort re-search landing back on the running branch →
  *Trace:* no service deactivate/activate pair.

## F. Blackboard

- **S34 [K]** — A blackboard **asset** defines typed keys; each key type may
  carry an authored default value; each runner gets a runtime **instance**
  whose values initialize to the authored defaults (or the type's
  invalid/zero value when none is authored). *(src-verified [src:
  BlackboardData.h:12-42; BlackboardKeyType_Int.h:27;
  BlackboardComponent.cpp:176-187; BlackboardKeyType.cpp:21-40].)*
  *Stimulus:* key with authored default 5 read before any write → *Trace:*
  observed value 5.
- **S35 [K]** — Observer model (corrected): there is a **single** notification
  kind — a per-key value-change notification. Any write that changes the
  value, and any clear of a set key, invokes all registered observers of that
  key identically. "Presence" is not a separate mechanism; it is a `KeyQuery`
  (IsSet / IsNotSet) evaluated by decorators. *(src-verified [src:
  BehaviorTreeTypes.h:61-72; BlackboardComponent.cpp:426-484, 781-797].)*
  *Stimulus:* observer on key K; K set → notification; K cleared →
  notification.
- **S36 [K]** — Observers fire synchronously within the write that caused them
  (same call stack, before control returns to idle); any search they request
  is deferred to the next update (S08). *(src-verified [src:
  BlackboardComponent.cpp:781-797].)*
  *Stimulus:* write K inside a task's Execute → *Trace:* observer callback
  runs before Execute returns; search transitions in the next update.
  *Residual [V]:* interleaving with task completion inside one update.
- **S37 [K]** — Clearing a key notifies observers exactly as a value write
  does (single notification kind); a no-op write (same value) does not notify
  at all. *(src-verified [src: BlackboardComponent.cpp:781-797;
  BlackboardComponent.h:358-361].)*
  *Stimulus:* LP guard (IsSet on K) idle; branch2 running; K cleared →
  *Trace:* branch2 aborted; re-search skips the guard.
- **S38 [ours]** *(structural — enforced by the banned-API analyzer and
  allocation tests)* — Value types never box; keys are struct-typed per the
  catalog; key identifiers are compile-time symbols (no runtime string lookups
  in the tick path). String values follow the catalog's immutability rule.
- **S54 [K]** — `BlackboardDecorator` model: `KeyQuery` ∈ {IsSet, IsNotSet};
  `NotifyMode` ∈ {OnResultChange (requests only when the condition's result
  flips), OnValueChange (requests on every notifying change while the
  condition passes)}; UE default OnResultChange. With OnValueChange, a write
  that leaves the condition passing while the branch executes **restarts the
  branch** (abort + re-execute) unless the task's `RestartPolicy` is
  KeepRunning — the main use of that flag. *(src-verified: "always request
  when passing" [src: BTDecorator.cpp:88-120; ActivateBranch,
  BehaviorTreeComponent.cpp:990-1023].)*
  *Stimulus:* guard "K > 3", OnValueChange, branch running; K 5→6 →
  re-evaluation passes; next update the running task is aborted and
  re-executed. With OnResultChange: K 5→6 → no request; K 5→5 → no
  notification at all.

## G. Tree lifecycle

- **S39 [D]** — One tree per runner; `SubtreeTask` runs a nested tree as part
  of the parent's execution; the nested root's result maps to the task's
  result.
  *Stimulus:* subtree with instant-success root → *Trace:* subtree activated;
  succeeded; `SubtreeTask` succeeded.
- **S40 [K]** — `DynamicSubtreeTask`: the subtree asset is assigned/replaced
  at runtime by external code (component API keyed by `InjectionTag`); there
  is **no** blackboard-key observation re-choosing it. Replacing the subtree of
  the currently active task aborts the running subtree and starts the new one
  via the normal abort path. Root-level decorators of the dynamic subtree are
  not supported (dropped, error logged). *(src-verified [src:
  BTTask_RunBehaviorDynamic.h:13-18;
  BehaviorTreeComponent.cpp:3085-3126].)*
  *Stimulus:* game code swaps the subtree while the task is active → *Trace:*
  running subtree Finish(Aborted); new subtree activated.
- **S41 [D]** — Stopping the runner aborts everything; start options reset or
  preserve the blackboard instance.
  *Stimulus:* stop during a latent task → *Trace:* task Finish(Aborted); no
  further transitions.
- **S47 [K]** — Root-level decorators: a tree asset may carry decorators on
  its root; when the tree runs as a **subtree** they are injected into the
  parent tree at the subtree-task link (gating, observers, abort modes
  identical to decorators placed on the `SubtreeTask`); when the asset runs as
  the root tree they are ignored; the dynamic variant never supports them
  (S40). *(src-verified [src: BehaviorTree.h:44-50;
  BehaviorTreeManager.cpp:181-214]. Note: Epic's Node Reference page says the
  Root supports no decorators — contradicted by the Run Behavior page and by
  the engine source; source treated as authoritative.)*
  *Stimulus:* subtree with a root guard (Self) on K; K clears while the
  subtree's task runs → *Trace:* task Finish(Aborted); parent re-search.
- **S59 [K]** — A subtree runs on the runner's blackboard instance: writes
  from the nested tree are visible to the parent tree.
  *Stimulus:* subtree writes K; parent guard on K evaluated after subtree
  completion → *Trace:* guard sees the subtree's value.
- **S62 [K]** — Tree restart on exhaustion: when a search finds no executable
  branch (the root is exhausted), **every auxiliary node is unregistered** and
  — in loop-execution mode — a root search is requested for the next update,
  so an idle tree re-evaluates from the root on every update; single-run mode
  stops. Nothing stays "armed" between updates: a key change is noticed by the
  next root search, not by a registered observer. *(src-verified [src:
  BehaviorTreeComponent.cpp:585-613].)*
  *Stimulus (looped):* all branches guarded false at start → *Trace:* root
  composite completed with Failure; the next update repeats the same search;
  K set → the following root search executes branch 1.

## H. Debugger & performance invariants [ours]

- **S42 [ours]** — Every node transition (activation, deactivation, result,
  abort, observer fire, service tick) **and every blackboard write** is
  recorded into `ExecutionTrace` with a global sequence number; the trace alone
  suffices to replay a run.
  *Stimulus:* scripted scenario → *Trace:* replay from the log reproduces the
  original transition sequence exactly.
- **S43 [ours]** — Breakpoints pause execution before the chosen node
  activates; step commands resume one node at a time.
  *Stimulus:* breakpoint on node N; run → *Trace:* execution pauses before N's
  first activation; step → exactly N's next transition recorded.
- **S44 [ours] (bench-gated)** — Instrumentation detached: 0 B/tick, no
  measurable tick overhead (CI-enforced); attached: ≤ 10% tick-time in the
  standard stress scene. Asserted by allocation/performance tests, not traces.
- **S45 [ours] (bench-gated)** — Observer/event fan-out is bounded by
  registration count; queues are pooled and bounded; no unbounded growth
  across a run. Asserted by allocation/soak tests.

---

## Coverage: research §7 in-scope bullets → statements

| §7 item | Statements |
|---|---|
| 1 event-driven scheduler, instancing | S01, S02, S04, S05, S07, S08, S31, S46, S48, S49, S60 |
| 2 composites incl. Simple Parallel | S02–S06, S09–S14, S50, S58 |
| 3 decorators, observer aborts | S15–S26, S54 |
| 4 services | S32, S33, S53 |
| 5 tasks (instant/latent, instanced, cleanup) | S27–S31, S49 |
| 6 blackboard (keys, observers, instance/asset) | S34–S38, S54 |
| 7 debugger | S42–S44 (+ S45) |
| (tree lifecycle & subtrees — supplementary) | S39, S40, S41, S47, S59, S62 |
| 8 integrations (MoveTo etc.) | post-v1 of this spec (Phase 6 extends) — explicit waiver: no statements yet, by design |

## Open sandbox items (R7 worklist)

**Resolved from engine source 2026-11-23** (no longer sandbox-gated; citations
inline): S08 core (request deferral + request priority), S12, S22, S23, S24,
S25 (ForceSuccess converts a Self abort), S26 (attachment order with
short-circuit), S30, S31, S32, S34, S35, S37, S40, S47, S49 (defaults), S54
(re-requests; no-op write silent), S58, S62 (restart on exhaustion).

**Remaining [V]** (each still gets a captured UE 5.8 trace and a resolution
note appended before it may gate a golden test; the matching golden scenario
carries a `verify:Sxx` capability so it stays skipped):

- S05 — residual restart cases (decorator re-check between loop iterations)
- S08 — interleaving of an observer request with the completion of the task
  whose update fired it
- S11 — Delayed-mode instant-success special case
- S14 — whether a background that finishes is re-selected while the main task
  still runs
- S21 — UE observer notification order (sampling only)
- S33 — service callback ordering vs child execution
- S36 — write/completion interleaving within one update
- S50 — ordering between simultaneous latent aborts
- S53 — service continuity on re-search
- catalog — `double` key-type decision

## v1.0 changelog (relative to draft v0.9)

- Template compliance: every statement now carries a stimulus → trace sketch,
  an explicit cross-reference, or a structural/bench-gated marker (review B1);
  all [V] research questions restated as affirmative presumptions (B2).
- Corrected against UE 5.8 source: S04 (RestartPolicy conditioning), S23 (loop
  never breaks on child failure; result passthrough; infinite-only timeout),
  S24 (Failure result + reset-on-refocus), S30 (memory persists), S31 (shared
  templates + memory blocks, not per-runner objects), S32 (services on tasks),
  S34 (authored key defaults confirmed — the review's doubt was wrong), S35/S37
  (single notification kind; clear notifies; no-op write silent), S40 (external
  assignment via InjectionTag, no key observation).
- Added: S46–S50, S53–S54, S58–S60, S62. IDs retired by folding: S51→S32,
  S52→S33, S55→S37, S56→S24, S57→S23, S61→S25.
- Erratum queued for research notes (§7): OnBecomeRelevant/OnCeaseRelevant are
  node-relevance callbacks, not blackboard "key observers" — the wording that
  produced the original S35/S37 modeling error.
- Delta pass (same reviewer, post-revision): approved with changes; the seven
  consistency fixes applied — S32 sketch aligned to branch deactivation, S38
  marked structural, S13 sketch no longer asserts an abort ordering S50 leaves
  open, S21 gained an inline residual-[V] line, coverage table gained the
  lifecycle row, catalog gained the Struct deferral, S01's trigger list scoped
  and S23's sketch pinned to per-update iterations.

## v1.0.1 erratum (2026-11-24, from the P3 harness review)

The golden harness and its runtime came from the same author as v1.0, so the
harness could not see v1.0's own errors. A second reviewer re-derived every
trace from this spec and cross-checked UE 5.8 source; five semantics were
wrong and are corrected here (each verified against the cited source again
before the edit):

- **S07** — a chain of instant tasks does **not** run within one update: UE
  finds one task, executes one task per update, and defers every continuation.
  This re-segments every golden trace (one task per update).
- **S19 / S48** — an observer-triggered search is bounded to the decorated
  branch, not root-down; LP observers are armed only once a search has reached
  their branch.
- **S54** — OnValueChange restarts an executing branch whose condition still
  passes; it is not a no-op.
- **S25** — ForceSuccess converts the Failed result of a Self abort; only a
  literal Aborted passes through.
- **S62** — an exhausted tree does not stay "armed": all auxiliary nodes are
  unregistered and (in looped mode) a root search is re-requested.
- Also: S08 gained the request-priority rule; S18 states that the parent sees
  a failed child; S20/S21 state the arming and priority behavior; **S26 is
  resolved** from source; **S14 is re-opened as [V]**.

## Engine pin

UE `5.8.0-release`, commit `7deeb413d3dc1fc034f48d1aacc0861301829d32`
(sparse modules: AIModule, GameplayTasks, BehaviorTreeEditor, GraphEditor).
R7 sandbox captures must record the engine build alongside the traces.
