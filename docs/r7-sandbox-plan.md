# R7 sandbox plan — 15 canonical scenario classes

Human-owned input to R2's `[V]` resolution and Phase 3's differential validation.
Each class lists the UE 5.8 setup, the stimulus, what to capture, the golden
scenario that encodes our expectation, and the statements it resolves. Captures go
to `docs/ue-sandbox-traces/` with the engine build recorded next to them.

**Capture procedure (re-runnable):** install UE **5.8.x** (pin the minor), create a
minimal C++ project, build each tree below in the Behavior Tree editor, run with
the Behavior Tree debugger's execution history enabled, apply the stimulus, and
export the history (or the `ai.debug` log). Store one file per class named
`<class-id>.<build>.txt`. The comparison target is *equality modulo timing*: the
same node activation/deactivation/result sequence, timestamps ignored.

| # | Class | Tree / stimulus | Resolves | Golden scenario |
|---|---|---|---|---|
| 1 | Selector fallback & priority | Selector [A(instant fail), B(instant success)]; start | S02, S03, S16 | `S02-S03-selector-fallback` |
| 2 | Sequence with latent park | Sequence [A(latent 2s), B(instant)]; start, let A finish | S06, S48 | `S06-sequence-latent-park`, `S48-continuation-no-ancestor-recheck` |
| 3 | Instant chain & deferral | Sequence of instant tasks; plus a task that writes a key observed by an LP guard | S07, S08, S36 | `S07-S46-instant-chain-one-update`, `S08-S36-observer-deferred-search` |
| 4 | Re-entry & Ignore Restart Self | Running latent task re-selected by an LP re-search; flag on and off | S04, S49, S05 | `S04-S49-keep-running`, `S04-S49-restart-default`, `S05-loop-guard-flip` |
| 5 | Self abort | Sequence [guard(Self) → latent, B]; flip the key while running | S18 | `S18-self-abort` |
| 6 | LowerPriority abort & idle-but-armed | Selector [guard(LP, idle) → task, latent]; flip the key; also the all-guards-false start | S19, S62 | `S19-lower-priority-abort`, `S62-idle-but-armed` |
| 7 | Both abort & multi-observer | Selector [guard(Both) → latent, latent]; flip the key both ways; a second class with two LP guards on one key | S20, S21 | `S20-both-abort`, `S21-multi-observer-coalesced` |
| 8 | Latent abort cleanup | Task with a delayed abort under a Self guard; observer abort on a parallel's parent | S29, S13, S50 | `S29-slow-abort-blocks`, `S13-parallel-abort-both-children`, `S50-latent-aborts-ordered` |
| 9 | Simple Parallel finish modes | Parallel with main/background of different durations; Immediate and Delayed; background finishing first | S09–S11, S14 | `S09-parallel-activates-both` … `S14-parallel-background-finishes-early` |
| 10 | Cooldown | Cooldown-decorated branch; exit, re-enter inside and after the window | S22 | `S22-cooldown-window` |
| 11 | Loop | Finite loop over a failing task; infinite loop with timeout | S23 | `S23-loop-finite-failure-passthrough`, `S23-loop-infinite-timeout` |
| 12 | Time limit | Time-limited latent branch; let the limit expire; re-enter before expiry | S24 | `S24-time-limit-fails` |
| 13 | Services | Service on a composite and on a task; interval ticks; restart-timer on/off; re-search through an active service | S32, S33, S53 | `S32-service-on-task`, `S33-service-restart-timer-on/off`, `S53-service-continuous-on-research` |
| 14 | Blackboard notifications | IsSet / comparison guards; set, clear, no-op write; Notify Observer both modes; authored defaults | S34–S37, S54 | `S34-authored-default-read`, `S35-S37-set-and-clear-notify`, `S54-notify-*` |
| 15 | Subtrees & isolation | Static subtree with root decorators; dynamic subtree injected and swapped while running; subtree writing the parent's key; two agents on one tree | S39, S40, S47, S59, S60 | `S39-subtree-runs-inline`, `S40-dynamic-subtree-*`, `S47-root-decorators-of-subtree`, `S59-subtree-shares-blackboard`, `S60-cross-agent-isolation` |

## Statements still gated on these captures

`verify:S05`, `verify:S26`, `verify:S50`, `verify:S53` keep their golden scenarios
skipped until the corresponding trace confirms or refutes the presumption; the
scenario is then edited to the observed trace and the tag removed. Items the engine
source already settled (see the spec's "Resolved from engine source" list) need a
capture only as a differential cross-check in Phase 3.

## Decorator evaluation order (class 7 addendum, S26)

Two guards on one node, both failing, each with an observable side effect (a
`Print String` service or a blackboard write in a custom decorator). The trace
shows whether the second guard evaluates at all.
