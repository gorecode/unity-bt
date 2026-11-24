# R7 sandbox plan — canonical scenario classes

The plan called for ~15 classes; the P3 harness reviews added three (16–18) for
the semantics they corrected. Human-owned input to R2's `[V]` resolution and
Phase 3's differential validation.
Each class lists the UE 5.8 setup, the stimulus, what to capture, the golden
scenario that encodes our expectation, and the statements it resolves. Captures go
to `docs/ue-sandbox-traces/` with the engine build recorded next to them.

**Capture procedure (re-runnable):** install UE **5.8.x** (pin the minor), create a
minimal C++ project, build each tree below in the Behavior Tree editor, run with
the Behavior Tree debugger's execution history enabled, apply the stimulus, and
export the history (or the `ai.debug` log). Store one file per class named
`<class-id>.<build>.txt`. The comparison target is *equality modulo timing*: the
same node activation/deactivation/result sequence, timestamps ignored.

**Update boundaries are part of the trace.** The history must keep the per-tick
grouping (at most one executed task per tick): our golden traces are segmented
per update (S07/S08), and a capture that flattens ticks cannot detect a
segmentation error. Record, per tick: decorator evaluations, composite
activations and deactivations, aborts, and the task executed.

| # | Class | Tree / stimulus | Resolves | Golden scenario |
|---|---|---|---|---|
| 1 | Selector fallback & priority | Selector [A(instant fail), B(instant success)]; start | S02, S03, S16 | `S02-S03-selector-fallback` |
| 2 | Sequence with latent park | Sequence [A(latent 2s), B(instant)]; start, let A finish | S06, S48 | `S06-sequence-latent-park`, `S48-continuation-no-ancestor-recheck` |
| 3 | Instant chain & deferral | Sequence of instant tasks; plus a task that writes a key observed by an LP guard | S07, S08, S36 | `S07-S46-one-task-per-update`, `S08-deferred-continuation`, `S36-observer-inside-task-update` |
| 4 | Re-entry & Ignore Restart Self | Running latent task re-selected by a tree restart; flag on and off | S04, S49, S05 | `S04-S49-keep-running`, `S04-S49-restart-default`, `S05-loop-guard-flip` |
| 5 | Self abort | Sequence [guard(Self) → latent, B]; flip the key while running | S18 | `S18-self-abort` |
| 6 | LowerPriority abort & restart on finish | Selector [guard(LP, idle) → task, latent]; flip the key; flip it back before the next tick; also the all-guards-false start | S19, S62 | `S19-lower-priority-abort`, `S19-bounded-search-discarded`, `S62-restart-on-exhaustion` |
| 7 | Both abort & multi-observer | Selector [guard(Both) → latent, latent]; flip the key both ways; a second tree with two LP guards on one key | S20, S21 | `S20-both-abort`, `S21-multi-observer-priority` |
| 8 | Latent abort cleanup | Task with a delayed abort under a Self guard; observer abort on a parallel whose children abort slowly | S29, S13, S50 | `S29-slow-abort-blocks`, `S13-parallel-abort-both-children`, `S50-latent-aborts-ordered` |
| 9 | Simple Parallel finish modes | Parallel with main/background of different durations; Immediate and Delayed; background finishing first | S09–S11, S14 | `S09-parallel-activates-both` … `S14-parallel-background-finishes-early` |
| 10 | Cooldown | Cooldown-decorated branch; exit, re-enter inside and after the window | S22 | `S22-cooldown-window` |
| 11 | Loop | Finite loop over a failing task; infinite loop with timeout | S23 | `S23-loop-finite-failure-passthrough`, `S23-loop-infinite-timeout` |
| 12 | Time limit | Time-limited latent branch; let the limit expire; leave the branch (a LowerPriority guard on the other branch fires) and re-enter it; then a tree restart that re-selects the same running task — does the limit re-arm? | S24 | `S24-time-limit-fails`, `S24-time-limit-resets-on-reentry`, `S24-time-limit-on-restart` |
| 13 | Services | Service on a composite and on a task; interval ticks; restart-timer on/off; restart through an active service | S32, S33, S53 | `S32-service-on-task`, `S33-service-restart-timer-on/off`, `S53-service-continuous-on-research` |
| 14 | Blackboard notifications | IsSet / comparison guards; set, clear, no-op write; both notify modes; a write that leaves the condition passing under each mode; a re-armed LP guard written again while its condition still passes; authored defaults | S34–S37, S54 | `S34-authored-default-read`, `S35-S37-set-and-clear-notify`, `S54-notify-*` |
| 15 | Subtrees & isolation | Static subtree with root decorators; dynamic subtree injected and swapped while running; subtree writing the parent's key; two agents on one tree | S39, S40, S47, S59, S60 | `S39-subtree-runs-inline`, `S40-dynamic-subtree-*`, `S47-root-decorators-of-subtree`, `S59-subtree-shares-blackboard`, `S60-cross-agent-isolation` |
| 16 | ForceSuccess × Self abort | Selector [ForceSuccess → guard(Self) → latent task, next task]; flip the key; check whether the parent records success and whether the next task runs | S25 | `S25-force-success-converts-self-abort` |
| 17 | Restart & OnValueChange | Running latent task with and without Ignore Restart Self; tree restart while running; an OnValueChange guard written with a value that leaves the condition passing | S04, S49, S54 | `S04-S49-*`, `S54-notify-value-change-*` |
| 18 | Finish & restart cadence | All branches blocked at start (looped and single-run); count the searches per tick while idle; then set the key and record which update picks it up | S62 | `S62-restart-on-exhaustion` |

## Statements still gated on these captures

`verify:S05`, `verify:S08`, `verify:S14`, `verify:S24`, `verify:S33`, `verify:S50`,
`verify:S53` keep their golden scenarios skipped until the corresponding trace
confirms or refutes the presumption; the scenario is then edited to the observed
trace and the tag removed.

Items the engine source settled (see the spec's "Resolved from engine source"
list) need a capture only as a differential cross-check in Phase 3. The ones
resting on a single reading of the code deserve that cross-check most:
one task per update (S07), bounded observer searches and the discarded
out-of-range search (S19), the outcome applied after a slow abort (S29),
OnValueChange restart versus OnResultChange (S54), ForceSuccess on a Self abort
(S25), restart on finish (S62), and a subtree's root-decorator Self abort
finishing the subtree task with Failure (S47).

## Decorator evaluation order (S26)

Resolved from source (`BTCompositeNode.cpp:454-478`): attachment order with
short-circuit. A capture here is a cross-check only: two guards on one node,
both failing, each with an observable side effect (a `Print String` service or
a blackboard write in a custom decorator); the trace shows whether the second
guard evaluates at all.
