# UE5 Behavior Tree — Formal Specification (draft v0.9)

**Status: draft.** Authored Nov 2026 from UE 5.8 public documentation and established
community knowledge, as the behavioral target for this project's runtime.
**Review pending:** human maintainer + second frontier agent with clean context
(acceptance rules in [implementation-plan R2](implementation-plan.md)).
Items tagged **[V]** require resolution against the UE 5.8 sandbox (R7 traces) before
they gate any test.

## Statement format

Every statement: `ID | statement | confidence | stimulus → expected trace (sketch)`.
A statement is *decidable* iff a golden test can encode the stimulus and assert the
expected trace (node activation/deactivation/result sequence + blackboard mutations;
timestamps ignored — "equality modulo timing").

Confidence tags: **[D]** = documented in Epic's public docs; **[K]** = well established
community knowledge, not contradicted by docs; **[V]** = verify in sandbox;
**[ours]** = this project's own invariant (no UE counterpart).

## Naming map (working names — final public naming is the Phase-0 decision, RISK-12)

| UE5 concept | Working name (this project) |
|---|---|
| Behavior Tree asset | `TreeAsset` |
| Behavior Tree Component / Brain (per agent) | `TreeRunner` |
| Composite — Selector / Sequence | `Selector` / `Sequence` |
| Composite — Simple Parallel | `SimpleParallel` |
| Decorator (node-attached guard) | `Decorator` (subtypes: `BlackboardDecorator`, `CooldownDecorator`, `LoopDecorator`, `TimeLimitDecorator`, `ForceResultDecorator`) |
| Observer Aborts (None / Self / Lower Priority / Both) | `AbortMode.None / Self / LowerPriority / Both` |
| Task (instant / latent) | `InstantTask` / `LatentTask` |
| Service | `Service` |
| Blackboard / blackboard keys | `Blackboard` / typed `BlackboardKey<T>` |
| Run Behavior / Run Behavior Dynamic | `SubtreeTask` / `DynamicSubtreeTask` |
| Node memory (per-agent instances) | node instancing model |
| Behavior tree execution history | `ExecutionTrace` |

## Typed blackboard key catalog (exact Unity types — product set)

`bool`, `int`, `float`, `double`, `string`, `UnityEngine.Vector2`,
`UnityEngine.Vector3`, `UnityEngine.Quaternion`, enum (int-backed),
`UnityEngine.Object` (assignable with type filter). Extensions beyond this set are
post-v1. `double` inclusion is provisional **[V]** (check UE numeric practice — no UE
double keys; may drop).

---

## A. Scheduler & execution model

- **S01 [D]** — Updates are event-driven; a search runs only on: tree start/restart;
  task completion; abort completion; blackboard observer firing; explicit
  branch-execution request. There is no per-frame traversal.
  *Stimulus:* idle tree, one frame passes → *Trace:* empty (no node transitions).
- **S02 [D]** — Search evaluates root-down; child order at each composite defines
  priority (leftmost = highest).
  *Stimulus:* selector with [A(fail), B(success)] → *Trace:* A activated→failed;
  B activated→succeeded; selector succeeded.
- **S03 [D]** — Selector: fails only when all children fail; succeeds on first child
  success. *(covered by S02 stimulus)*
- **S04 [D]** — Re-entry: a new search that selects a branch which is currently
  executing re-enters it — running latent tasks are NOT re-executed or restarted.
  *Stimulus:* LP-abort re-search landing on the running branch → *Trace:* no task
  re-activation.
- **S05 [V]** — Enumerate the exact cases where a re-selected running task IS
  restarted (suspected: decorator re-check failures between iterations, loop
  restarts). Sandbox: LP abort → re-enter same branch with changed conditions.
- **S06 [D]** — Sequence: children in order; any child failure fails the sequence
  immediately; all succeed → success; a running latent child parks the sequence at
  its position.
  *Stimulus:* [A(2s latent, success), B(instant success)] → *Trace:* A
  activated→running; (2s) A succeeded; B activated→succeeded; sequence succeeded.
- **S07 [K]** — Instant tasks execute synchronously within one search; a search ends
  when a latent task returns InProgress.
  *Stimulus:* chain of instant tasks → *Trace:* all transitions within one update
  (single trace segment, one global sequence-number block).
- **S08 [V]** — Deferred execution: enumerate which task activations / abort
  completions are deferred to a later update (same-frame vs next-frame rules).
  Sandbox: abort completing during a blackboard-notification storm.

## B. Composites

- **S09 [D]** — `SimpleParallel` has exactly two children: main task + background
  subtree; both activate together.
- **S10 [D]** — Finish mode **Immediate**: main task finishing aborts the background
  branch; parallel result = main result.
  *Stimulus:* main succeeds at t=1s, background running → *Trace:* background aborted;
  parallel succeeded.
- **S11 [D]** — Finish mode **Delayed**: main task finishing lets the background
  finish naturally; parallel result = main result, delivered after background
  completes.
  *Stimulus:* main succeeds at t=1s, background finishes at t=3s → *Trace:* parallel
  succeeds at t=3s with main's result.
- **S12 [V]** — May the background branch contain a nested `SimpleParallel`?
  (Presumed: no.) Sandbox: author nested case; record editor behavior.
- **S13 [D]** — Aborting a `SimpleParallel` aborts both children.
- **S14 [V]** — Background branch finishing while main still runs: effect on the
  node (presumed: none; node keeps running on main). Sandbox: background short,
  main long.

## C. Decorators (guards)

- **S15 [D]** — Decorators attach to composite/task nodes; multiple allowed; **all**
  must pass (AND) for branch entry.
- **S16 [D]** — A decorator failing at entry = branch not entered; the parent reacts
  as if the child failed (selector: next child; sequence: fail).
  *Stimulus:* selector [A(guard fail), B] → *Trace:* A never activates; B runs.
- **S17 [D]** — `AbortMode.None`: condition checked only at branch entry.
- **S18 [D]** — `AbortMode.Self`: while the branch executes, condition→false aborts
  the branch; a new search follows.
  *Stimulus:* running task under Self-guard; observed key flips → *Trace:* task
  Finish(Aborted); new search from parent.
- **S19 [D]** — `AbortMode.LowerPriority`: while the decorated branch is *not*
  executing, a change of the observed condition aborts any lower-priority execution
  and triggers a new search (which re-checks this decorator first).
  *Stimulus:* branch1(guard on key K, LP) idle, branch2 running; K flips → *Trace:*
  branch2 aborted; search re-checks guard; if pass → branch1 executes.
- **S20 [D]** — `AbortMode.Both` = Self + LowerPriority simultaneously.
- **S21 [V]** — Ordering when several observers fire from one blackboard write:
  document the rule (suspected: priority/registration order) and its observable
  effect on abort sequencing.
- **S22 [V]** — Cooldown: scope and trigger point (starts on branch finish vs on
  activation; per-agent instance). Sandbox: two rapid entries after a finish.
- **S23 [D]** — Loop: repeats the branch N times or infinitely; variant breaks on
  child failure. **[V]** exact result mapping for infinite-loop-with-failure.
- **S24 [D]** — TimeLimit: branch executing past the limit is aborted.
  **[V]** final result recorded (aborted vs failed).
- **S25 [D]** — ForceSuccess maps branch failure→success at completion. *(ours)*
  `ForceResultDecorator` additionally offers ForceFailure — flagged as our extension,
  not a UE claim.
- **S26 [V]** — Evaluation order of multiple decorators on one node (attachment
  order vs reverse; short-circuiting). Sandbox: two failing guards with side effects.

## D. Tasks

- **S27 [D]** — Tasks are the only leaves; instant tasks return a terminal result
  synchronously; latent tasks return InProgress and complete via an explicit
  completion call (possibly many updates later).
- **S28 [D]** — Lifecycle: activation → Execute → optional per-update Tick while
  running → Finish(∈ {Success, Failure, Aborted}) exactly once per activation.
- **S29 [D]** — Abort path: abort request calls the task's abort hook; the task may
  finish the abort immediately or asynchronously; the tree does not proceed past the
  aborting task until its abort completes; Finish(Aborted) is delivered.
- **S30 [K]** — A task whose branch is re-entered after abort restarts (new Execute),
  it never resumes mid-state. **[V]** enumerate exceptions.
- **S31 [K]** — Node instancing: each runner instantiates its own node instances;
  nodes explicitly marked shared are per-asset and must not hold per-runner state.
  **[V]** confirm UE defaults per node class (which built-ins are shared).

## E. Services

- **S32 [D]** — Services attach to composites; they activate when the branch becomes
  relevant and deactivate when it ceases.
- **S33 [D]** — While active, a service ticks at its configured interval (UE default
  0.5 s), optionally randomized (± deviation); it also receives search-start/finish
  and activation callbacks. **[V]** exact callback set and ordering vs child execution.

## F. Blackboard

- **S34 [D]** — A blackboard **asset** defines typed keys (catalog above); each runner
  gets a runtime **instance** with values initialized from asset defaults.
- **S35 [D]** — Two observer kinds: value-change and presence (set/unset).
- **S36 [K]** — Observers fire in the same update as the write that caused them
  (before returning control to idle). **[V]** exact interleaving vs task completion
  within one update.
- **S37 [D]** — Clearing a key ≠ writing a value; clear fires presence observers,
  not value observers.
- **S38 [ours]** — Value types never box; keys are struct-typed per the catalog;
  string keys are compile-time identifiers (no runtime string lookups in the tick
  path).

## G. Tree lifecycle

- **S39 [D]** — One tree per runner; `SubtreeTask` runs a nested tree as part of the
  parent's execution; nested root result maps to the task's result.
- **S40 [K]** — `DynamicSubtreeTask` re-chooses its subtree when the observed key
  changes (aborting the current subtree if it differs). **[V]** exact re-evaluation
  timing.
- **S41 [D]** — Stopping the runner aborts everything; start options reset or
  preserve the blackboard instance.

## H. Debugger & performance invariants [ours]

- **S42 [ours]** — Every node transition (activation, deactivation, result, abort,
  observer fire, service tick) is recorded into `ExecutionTrace` with a global
  sequence number; the trace alone suffices to replay a run.
- **S43 [ours]** — Breakpoints pause execution before the chosen node activates;
  step commands resume one node at a time.
- **S44 [ours]** — Instrumentation detached: 0 B/tick, no measurable tick overhead
  (CI-enforced); attached: ≤ 10% tick-time in the standard stress scene.
- **S45 [ours]** — Observer/event fan-out is bounded by registration count; queues
  are pooled and bounded; no unbounded growth across a run.

---

## Coverage: research §7 in-scope bullets → statements

| §7 item | Statements |
|---|---|
| 1 event-driven scheduler, instancing | S01, S02, S04, S05, S07, S08, S31 |
| 2 composites incl. Simple Parallel | S02–S06, S09–S14 |
| 3 decorators, observer aborts | S15–S26 |
| 4 services | S32, S33 |
| 5 tasks (instant/latent, instanced, cleanup) | S27–S31 |
| 6 blackboard (keys, observers, instance/asset) | S34–S38 |
| 7 debugger | S42–S44 (+ S45) |
| 8 integrations (MoveTo etc.) | post-v1 of this spec (Phase 6 extends) |

## Open sandbox items (R7 worklist)

S05, S08, S12, S14, S21, S22, S23(partial), S24(partial), S26, S30(partial),
S31(partial), S33(partial), S36, S40 — 14 items; each gets a captured UE 5.8 trace
and a resolution note appended here before it may gate a golden test.
