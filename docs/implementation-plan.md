# Implementation Plan — draft v0.3 PROPOSAL (Nov 2026, code-first authoring)

> **Status: proposal.** The direction — no visual editor, trees authored in C# —
> is the maintainer's decision of 2026-11-24. What awaits review is this reworked
> plan. It replaces v0.2 (last state: commit `7ef53da`) once accepted; until then
> README.md, AGENTS.md and the spec keep their v0.2 wording. The edits that follow
> acceptance are listed [at the end](#edits-that-follow-acceptance).

"Fully finished product" = **UE5 behavior trees in Unity, end to end**: faithful
event-driven runtime, **trees authored in C#**, UE-grade debugger, native
serialization of per-agent data, docs, samples, CI, released as a UPM package — MIT.

Cadence assumption: **1 human, ~10 h/week** (review/gates) + agent workflow
(implementer + second-agent reviewer + Unity CLI compile loop; roles in
[research §5](research.md), wired in [AGENTS.md](../AGENTS.md)).
Timeline carries a **+25% contingency buffer**; re-plan triggers are stated below.
**Minimum Unity version: 6.6 (6000.6)** — unchanged. It is now a support-window
choice, not a technical prerequisite.

---

## Why v0.3 — decision record (2026-11-24)

**Trigger.** The P1 spike and a check of the 6000.6 and 6000.7 documentation
([gt-notes.md](gt-notes.md), "Decorators") showed that Graph Toolkit cannot attach
a decorator to a branch the way UE does — in either version. Decorators can only
be separate nodes in the execution chain (Unity Behavior's approach) or fixed
option fields baked into a node type. 6000.7's `NodeView<T>` can draw them inside
the node card, but not on the connection, and not on the project floor. The
maintainer's verdict: without UE-style attached decorators a visual editor is not
convenient enough to be worth its cost. **The visual editor is dropped from v1.**

**Unchanged requirements:** UE-faithful semantics (R2 spec); zero allocations in
the tick path and the R5 performance targets; the agent-driven workflow; the legal
rules (spec-led, no carryover); MIT.

| | v0.2 | v0.3 |
|---|---|---|
| Trees are | graph assets edited in a Graph Toolkit window | C#, built once into an immutable **tree definition** |
| Decorators, services | nodes in the execution chain | attached to the node they guard, in attachment order (S15, S26, S32) |
| Per-agent data | graph asset + blackboard asset | serialized fields on the runner component |
| Debug view | drawn from the graph asset | drawn from the tree definition |
| Unity API surface | experimental Graph Toolkit | engine core only |

**Gains.** UE parity for attached decorators. Trees are reviewable code: diffs,
renames, tests. There is no on-disk tree format to version or migrate; the source
API becomes the compatibility surface instead (RISK-20). Two risks leave the
register (RISK-01, RISK-06), one is deferred and one narrows (RISK-05, RISK-11).
Prototype P6 and research item R1 leave the plan; P5 moves to Phase 4.

**Costs, stated plainly.** No visual authoring for non-programmers; the audience
narrows to code-first teams and agent-driven projects (RISK-14). The debugger needs
its own tree view instead of a graph window. NPBehave's model (code-built trees,
decorator wrappers) is now conceptually closer to ours; its code is still not
adopted — the decision stands on license provenance and allocation profile
([npbehave-audit.md](npbehave-audit.md)).

**Door left open, not opened.** The builder's output, the tree definition, is
plain data. A visual editor or a data format could later be added as another front
end producing the same definition. Nothing in v1.0 depends on it.

---

## Phase 0 — Preliminary research (2–3 weeks remaining, partially parallel)

Research items **R1–R9** and prototypes **P1–P9** are mapped in the
[risk register](risks-and-prototypes.md), most of them one-to-one. Each produces a
doc or a throwaway prototype under `spikes/`. Done so far: R1 (closed), R3, P3.

### R1 — Graph Toolkit spike (↔ P1) — CLOSED 2026-11-24, not adopted
Toy graph tool on 6000.6: 11 node types, custom fields, subgraph, round-trip test
green. Findings in [gt-notes.md](gt-notes.md). The 6.7-beta rebuild was not run.
**Outcome:** no editor is built (decision record above).

### R2 — UE5 BT formal specification (↔ P2)
Write `docs/ue-bt-spec.md`: complete execution semantics — scheduler, search/abort
order, observer abort modes, services timing, latent task continuation, Simple
Parallel finish modes (Immediate/Delayed — confirm), blackboard observer firing rules,
node instancing. Sources: UE 5.8 public docs + engine source **as behavior spec only**
(RISK-02) + empirical probes in the R7 sandbox where docs are ambiguous.

**Acceptance definition (binding):**
- Every statement uses the template: `ID | statement | confidence tag ([D]ocumented /
  [K]nown-community / [V]erify-in-sandbox) | stimulus & expected trace sketch`.
- Deliverables include the **naming mapping table** (our node/API names ↔ UE concepts)
  and the **typed blackboard key catalog** (exact Unity types).
- **Coverage rule:** every in-scope bullet of [research §7](research.md#7-ue5-bt-semantics--scope-inventory)
  maps to ≥1 numbered statement (coverage table at spec end).
- **Reviewers:** the human maintainer **and** a second frontier agent with clean
  context, both sign off; disputes resolved against the R7 sandbox.
- A statement is *decidable* iff a golden test can encode stimulus → expected trace.

Current state: **v1.0.3 exists** (2026-11-24): three review rounds, all errata
verified against UE 5.8 engine source (tag 5.8.0-release, commit 7deeb413).
Pending: human maintainer sign-off; remaining [V] items listed in the spec's R7
worklist. **v0.3:** six wording changes are queued for that sign-off (see
"Edits that follow acceptance"); none changes a statement's meaning.
**Exit:** sign-offs recorded; all [V] items resolved or explicitly deferred with
rationale.

### R3 — NPBehave audit (no separate prototype; decision doc is the artifact)
Map NPBehave's model onto the R2 spec; decide **adopt & adapt** vs **spec-led
reimplementation informed by it**.
Current state: **done** — see `docs/npbehave-audit.md` (decision: spec-led
reimplementation; NPBehave dormant since commit 2022-11-26, verified).
**v0.3 note:** the audit rejected code-built trees as incompatible with an
editor-first product. That reason no longer applies; the decision stands on its
other two grounds.
**Exit:** decision recorded and referenced by Phase 1.

### R4 — Agent workflow harness (no prototype in v1)
Set up and validate the dev loop before product code: Unity CLI installed; project
wired for compile + test runs; this repo's AGENTS.md; second-agent reviewer wired
(trigger point: every PR-sized change). The layout-test → screenshot → vision →
human-gate loop (P5) is first run at Phase 4, on the debugger window.
**Exit (measurable): 5 consecutive agent-made changes to the package** (runtime,
editor or tests — not spikes), each passing compile → test → second-agent review
with **zero human intervention**. An intervention is any human edit or instruction
between the start of a change and its green run. Review findings are fixed or
waived: nits may be waived with a written reason, a should-fix finding only with
the maintainer's agreement, never by the implementing agent alone. The number of
waivers is reported with the exit. A review passes only with no open blocking or
should-fix finding.

### R5 — Performance baselines (↔ P4)
Microbenchmarks on Unity 6.6, **Mono for iteration + IL2CPP for enforcement**:
event-driven vs polled tree, 1k/10k agents, allocation profiling. Baselines: NPBehave,
Unity Behavior. Deliverable: `docs/perf-baseline.md`.
**Standard workload** (reused by P8, Phase 1 and Phase 5): 1k and 10k agents, each
running the same tree — a latent task under an observer-abort guard — updated every
frame, with blackboard writes firing the observer at a fixed rate; an event-driven
and a polled variant.
**Canonical target set (used identically in Phase 1, Phase 5, DoD):**
≥ 1.5× the agent count of the **better** baseline (NPBehave, Unity Behavior) within a
16.6 ms frame budget, **and 0 B/tick steady-state allocation on IL2CPP**.
**Exit:** harness checked in; harness proven to detect an injected 24 B/tick leak
(that proof is P4); numbers recorded.

### R6 — CI skeleton (no game-code prototype; stands on P4's harness)
GitHub Actions: Linux headless leg (Unity CLI batch: compile + golden tests + alloc
tests). The **desktop leg for UI tests starts at Phase 4** — v1.0 has no authoring
UI. Resolve and document **Unity license activation on runners**
(Personal vs Pro; activation-count limits; secrets handling; game-ci or first-party
action — decided here, tested with a trivial green run). Artifact storage for
test output; from Phase 1 on, a nightly IL2CPP player smoke test (RISK-22).
**Exit:** headless leg green on a trivial project; activation procedure documented in
`docs/ci.md`; IL2CPP test leg decision recorded (enforced nightly or per-PR — with
rationale).

### R7 — UE 5.8 sandbox & trace-capture procedure (human-owned, ~2 days)
Install UE **5.8 (pinned minor)**, minimal C++ project, author the 18 canonical
scenario classes ([r7-sandbox-plan.md](r7-sandbox-plan.md)), capture execution
traces (BT debugger history / `ai.debug` logging) in a repeatable way. Output:
`docs/ue-sandbox-traces/` + capture procedure. **This is an input to R2 [V]-resolution
and Phase 2 differential tests.**
**Exit:** all 18 classes captured; procedure re-runnable by the maintainer.

### R8 — Zero-alloc feasibility spike (↔ P8)
Implement **one latent task + one observer abort** under AGENTS.md constraints
(no iterator methods, no closures, no boxing; struct state machines; pooled runner
state) — **in both candidate state models** of the authoring model below: pooled
per-runner node instances, and shared definition + per-runner state arrays. Measure
B/tick and µs/update at 1k and 10k agents on the standard workload (P4), event path
included — observer aborts driven by blackboard writes. Choose by numbers (rule in
P8). Codify the permitted-construct list and the analyzer that enforces it.
**Exit:** the chosen model is at 0 B/tick on IL2CPP at both agent counts and at or
below the better baseline's µs/update (R5 numbers); decision recorded with numbers;
banned-construct analyzer config merged.

### R9 — Authoring API (↔ P9)
Design the C# authoring surface against three constraints: it reads like the tree;
it can express every golden scenario; an agent can use it from the documentation
alone — including to write its own tasks and services under the zero-alloc rules.
Deliverables: builder API, parameter slots, structural validation, text dump,
authoring guide.
**Exit:** P9 passes both its legs (the second after P8).

### Phase 0 gate (go/no-go — all measurable)
- [x] R1/P1: closed, not adopted (decision record above)
- [ ] R2: spec sign-offs (human + second agent) recorded; coverage table complete
- [x] R3: donor decision recorded (done)
- [x] P3: harness pass — every traceable R2 statement has a scenario (coverage
  enforced by `Scenarios_CoverEveryTraceableSpecStatement`); 18 scenario classes
  enumerated in `docs/r7-sandbox-plan.md`; format documented in
  `docs/golden-format.md`
- [ ] R4: 5 consecutive agent changes, zero human intervention
- [ ] R5/P4: harness detects injected 24 B/tick leak; baselines recorded
- [ ] R6: headless CI leg green; Unity license activation documented & tested
- [ ] R7: 18 scenario classes captured, procedure re-runnable
- [ ] R8/P8: chosen state model at 0 B/tick and at or below the better baseline's
  µs/update; analyzer merged
- [ ] R9/P9: authoring API passes both legs of the agent-usability experiment
- [ ] Naming/product-name decision (RISK-12) recorded — incl. UPM package id

---

## Authoring model (what v0.3 commits to)

**Two layers.** Authoring produces a **tree definition**: an immutable structure
built once per tree type and shared by every runner that uses it. A **runner**
(one per agent) owns execution state and parameter values. The definition is plain
data — the debugger, the text dump and the tests all read it.

**A definition holds structure, not agent state.** It may contain constants,
blackboard keys, node prototypes and *parameter slots* — never a value that differs
per agent. Each runner binds its own serialized fields to the slots when it is
created.

**Illustrative surface.** The final shape is P9's deliverable; names follow the
spec's naming map.

```csharp
public sealed class PatrolBrain : TreeRunner
{
    // Per agent; bound to the definition's "detectRange" slot when the runner is created.
    [SerializeField] float m_DetectRange = 12f;

    // Structure only. Called once per tree type per Play session; the runtime caches the result.
    protected override TreeDefinition OnDefineTree() => Selector(
        BlackboardDecorator(Keys.HasTarget, abort: AbortMode.Both,
            Sequence(
                Chase(Param<float>("detectRange")),
                Wait(1f))),
        Sequence(
            CooldownDecorator(2f, MoveTo(Keys.PatrolPoint)),
            Wait(1f))
            .Services(SenseEvery(0.2f)));
}
```

- Decorators wrap the node they guard, outermost first — the order they are
  evaluated in (S26), the same model as the golden scenario format. Services hang
  off composites and tasks (S32).
- **Definition lifetime.** The runtime object of a Play session owns the definitions
  and builds each one when its first runner is created; nothing static caches them
  (AGENTS.md invariant 3). The definition is immutable afterwards. A tree that fails
  validation reports the error at that point and works in the next session once
  fixed — a broken tree never takes its type down.
- **"Structure only" is enforced by tests:** the Phase-1 edit-mode test builds every
  definition twice, on two runner instances with different field values, and
  requires identical dumps; the fast-play test requires identical dumps before and
  after a full golden run.
- `m_DetectRange` is a plain serialized field: per-agent parameters are native
  Unity serialization and sit next to the structure. Prefab variants overriding such
  a field **must** work — a prefab-override test is part of the Phase-3 exit, not
  an assumption here.
- A runner's blackboard starts from the values bound at creation (S34's authored
  defaults).
- **Allocation budget:** definition build — once per tree type per session, at
  load; runner creation — pooled; tick path — 0 B (AGENTS.md invariant 1).

**Validation acts like a compiler for trees.** The C# compiler cannot see tree
semantics, so building a definition rejects an invalid tree with a message naming
the node and its path. That runs at load time — in Play mode, or in a player — so
an edit-mode test also builds every definition in the package (and, as they arrive,
the samples) and instantiates every runner type: an invalid tree, or a field bound
to a missing slot, fails CI, not the first prefab load. Every structural statement gets
a rule and a test: the root has one composite child (S46); decorators sit only on
composites and tasks (S15); a loop carries no observer abort (S23); a parallel's
shape and task-only main slot (S09); a warning on nested parallels (S12).

**Text dump.** `TreeDefinition.Dump()` prints a tree as indented text — node
types, nesting and order, decorators, services, parameters — leaving node ids out.
Tests compare dumps, reviews read them, agents check their edits against them.

**State model** — how a runner stores node state — is left to P8: pooled
per-runner node instances, or shared definition + per-runner state arrays, chosen
by measurement against the R5 targets.

**Not in v1.0:** any visual editor; a data (JSON/DSL) tree format; editing tree
structure in the Inspector.

---

## Phase 1 — Runtime core + authoring API (3–4 weeks; depends on R2, R3, R4, R5, R6, R8, R9)

Scope: event-driven scheduler; Selector/Sequence; instant & latent tasks; blackboard
with typed keys (R2 catalog) + observers; condition decorators with Self abort;
per-agent state in the P8 model; **builder API from P9** with parameter slots,
structural validation and text dump; **UPM package skeleton + asmdef split
(runtime/editor/tests/spikes) from day one**; **debugger instrumentation points**
(compile-time switched capture, zero-cost when off — Phase 4 depends on this);
golden tests from R2 statements (all Phase-1-applicable [D]/[K] ones green, **every
tree built through the public builder API**); zero-alloc enforced by R8 analyzer +
alloc tests in CI.
**Exit (numeric):**
- CI green (R6 headless leg) incl. alloc tests on IL2CPP;
- every golden scenario that needs only Phase-1 capabilities passes on the product
  runtime, built through the public builder API;
- each structural rule (S09, S12, S15, S23, S46) has a validation test, and an
  edit-mode test builds every definition in the package twice (two runner instances
  with different field values, identical dumps) and instantiates every runner type;
- perf ≥ NPBehave event-driven baseline (µs/agent·update and agents-per-16.6 ms-frame
  recorded vs R5 numbers); instrumentation detached-overhead test = 0 B/tick and no
  measurable tick-time delta;
- fast-play test: two Play sessions without domain reload produce identical golden
  traces and identical definition dumps (no stale definition, no leaked runner state).

## Phase 2 — UE-parity runtime semantics (3–4 weeks; depends on Phase 1)

Scope: full observer abort semantics (Self/LowerPriority/Both + abort search order),
services (periodic + event), Simple Parallel (both finish modes), cooldown/loop/
time-limit/force-result decorators, subtrees, deferred & event-driven activation paths.
- Golden tests for the full R2 spec.
- **Mandatory differential validation:** every scenario *class* from the R7 set is
  compared against UE traces; **pass rule: trace equality modulo timing** (same node
  activation/deactivation/result sequence; timestamps ignored). Results doc signed off
  by the human maintainer against this written rubric.
**Exit:** full spec statement list green — the 58 non-debugger golden scenarios pass
on the product runtime and the naive reference runtime is deleted (the two debugger
scenarios, S42 and S43, pass in Phase 4); differential doc signed off per rubric.

## Phase 3 — Authoring hardening (1–2 weeks; depends on Phase 2)

Scope: parameter binding between runner fields and definition slots on the real
runtime; the authoring guide, written for agents and for people; both legs of P9
re-run on the final API.
**Exit (scripted):** "PatrolXPatrol" — two agents, each with a clean context and
only the authoring guide, and then one human (the maintainer), each build and run a
two-guard patrol/investigate tree in C#: attached decorators, one service, one
subtree, one custom latent task, two per-agent parameters. No runtime-code change
and no help between the start and the recorded result.
- **Pass rule for "runs":** the tree's dump equals the reference dump, and a
  recorded run reproduces the scenario's expected trace (golden runner).
- **Thresholds:** each agent meets P9's composition and custom-node thresholds; the
  maintainer — who wrote neither the API nor the guide — completes it unaided, the
  time is recorded, and every place the guide failed them is fixed before the exit.
- **Tuning (RISK-16):** an automated test instantiates a prefab variant with an
  overridden parameter and asserts the runner uses the overridden value.

## Phase 4 — Debugger (5–6 weeks; depends on Phases 1–2; can overlap Phase 3)

Scope: Play-mode live visualization in a **read-only tree window drawn from the tree
definition** (no authoring); breakpoints pausing Play mode (per-node,
per-decorator); execution history with replay (R7-style trace capture already
instrumented in Phase 1); abort-path tracing; blackboard time-travel inspection.
The screenshot → vision pipeline and the desktop CI leg are set up here (R4, R6):
the window is editor UI, so screenshot + vision review + human gate apply again
(AGENTS.md).
**Budgets:** detached = 0 B/tick and no measurable tick overhead (CI-enforced);
attached = ≤ 10% tick-time overhead in the standard workload (recorded number).
**Exit:** demo scenario debugged end-to-end (scripted checklist); the debugger
scenarios S42 and S43 pass; both budgets met; the UI loop meets P5's thresholds —
layout tests catch ≥ 9/10 of pre-classified geometry defects, vision ≥ 6/10
aesthetic, together 100% of defects classified blocking.

## Phase 5 — Performance & scale (3–4 weeks; depends on Phases 2 and 4)

Scope: profiler module (per-tree timings, abort/event counts); stress tests;
optimization; **go/no-go on the optional DOTS/Burst module** (isolated asmdef/package;
core must compile and run with zero Entities references — AGENTS.md rule).
**Exit (canonical target set, same as R5):** ≥ 1.5× better-baseline agent count within
16.6 ms frame budget **and** 0 B/tick on IL2CPP; DOTS decision recorded with numbers.

## Phase 6 — Productization (starts alongside Phase 4; continuous)

Docs (manual + API reference), samples mirroring official UE BT examples in C#,
integration tasks (AI Navigation MoveTo etc.), UPM release (scoped registry / Git URL /
openUPM submission — channel decision with checklist, RISK-14), semantic versioning
of the public API from the first release (RISK-20), contribution guide, versioned
releases + changelog, **pre-release IP review** (RISK-02).

---

## Timeline summary (single implementer: overlap is agent-parallel only)

| Phase | Nominal weeks | With the +25% buffer | Depends on |
|---|---|---|---|
| 0 research (remaining to the gate) | — | 2–3 (an estimate to the gate, slack included) | — |
| 1 runtime core + authoring API | 3–4 | 4–5 | R2 R3 R4 R5 R6 R8 R9 |
| 2 UE parity | 3–4 | 4–5 | Phase 1 |
| 3 authoring hardening | 1–2 | 2–3 | Phase 2 (agent-parallel with Phase 4's start) |
| 4 debugger | 5–6 | 6–8 | Phases 1–2 |
| 5 performance | 3–4 | 4–5 | Phases 2, 4 |
| 6 productization | continuous — not summed | | starts at Phase 4 |

Phases 0–5 with their buffers sum to **22–29 weeks (≈ 5–7 months)** to a
feature-complete product; v1.0 ships once the Phase-6 release steps are done. For
comparison, v0.2's Phases 1–5 summed to 22–29 weeks and v0.3's to 20–26: a saving
of two to three weeks, because the debugger grew by what it absorbed. The larger
gain is in the risk register.

**Re-plan triggers:** P9 fails a leg twice (re-work the surface for 1–2 weeks, or
evaluate a data format before Phase 1); P8 leaves no model at 0 B/tick (relax the
invariant explicitly — an architectural decision, not a silent slip); Phase 2
differential failures > 2 scenario classes (spec revision + 1 wk); CI license
friction unresolved in R6 (pivot CI host).

## Definition of done (v1.0)

- [ ] Every in-scope R2 statement covered by a passing golden test (coverage table)
- [ ] Differential traces: all R7 scenario classes pass "equality modulo timing"
- [ ] Trees authorable in code by a non-author following the guide (the scripted
  Phase-3 scenario: both agents pass the thresholds; the maintainer completes it
  unaided)
- [ ] Debugger: breakpoints, live flow, history replay; detached 0 B/tick + no
  measurable overhead (CI-enforced); attached ≤ 10% (recorded)
- [ ] Canonical perf target met: ≥ 1.5× better baseline in 16.6 ms frame budget,
  0 B/tick steady-state on IL2CPP
- [ ] Per-agent parameters are serialized component fields; prefab-variant overrides
  verified by test; the runtime has no custom serialization
- [ ] Public API under semantic versioning and covered by the API inventory job
- [ ] Docs + UE-parity samples in C#; UPM package installable; CI green on 6.6 + 6.7
- [ ] Naming/trademark check passed; pre-release IP review pass (RISK-02)

---

## Edits that follow acceptance

Each is a small, separate commit once v0.3 is accepted.

1. **README.md** — mission (trees authored in C#; no editor bullet); landscape
   table column "Editor on Unity's supported graph stack" → code-first authoring;
   the 6.6 non-goal rationale; ground rule 5 scoped to the debugger window; spec
   version in the documentation list.
2. **AGENTS.md** — invariant 1 no longer prescribes "pools for node instances"
   (what is pooled follows P8); invariant 2 becomes "per-agent data is native Unity
   serialization" (parameters are component fields); invariant 5 drops the Graph
   Toolkit clause; the editor/UI rule applies to the debugger window; the 6.6
   rationale.
3. **docs/ue-bt-spec.md** (proposed at sign-off) — six wording changes: S09
   "(editor schema)" → "(authoring validation)"; S12 "our editor warns" → "the
   builder warns"; S31's [ours] instancing note and S60's "per-runner instances" →
   state model chosen by P8; the catalog's "the editor documents the degrees-Euler
   bridge" → the authoring guide documents it; naming map `TreeAsset` →
   `TreeDefinition`, to be settled with RISK-12.
4. **docs/research.md** — §1 (the 6.6 feature rationale), §3 (Graph Toolkit:
   validated, not adopted), §8 directives 2–4 (the code-first workflow).
5. **docs/licenses.md** — Graph Toolkit section marked as no longer a dependency
   (incl. "editor package depends on the GTK module"); the "Phase-2 design notes"
   sentence.
6. **docs/npbehave-audit.md** — decision rationale item 1 and "our Phase 2 depends
   on nodes as data" (see R3 above).
7. **docs/r7-sandbox-plan.md** — the differential validation is Phase 2 (two
   places say Phase 3).
8. **docs/console-allowlist.txt** — the editor-assembly note: the editor assembly
   now gets its scripts at Phase 4.
9. **spikes/README.md** — "P1–P8" → "P1–P9".
10. **Packages/com.gorecode.unity-bt/package.json** — description.
