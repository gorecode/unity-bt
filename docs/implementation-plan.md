# Implementation Plan — draft v0.2 (Nov 2026, post-critic-review)

"Fully finished product" = **UE5 behavior trees in Unity, end to end**: faithful
event-driven runtime, Graph Toolkit editor, UE-grade debugger, native serialization,
docs, samples, CI, released as a UPM package — MIT.

Cadence assumption: **1 human, ~10 h/week** (review/gates) + agent workflow
(GLM-5.3 implement + vision review + CLI compile loop; roles in
[research §5](research.md), wired in [AGENTS.md](../AGENTS.md)).
Timeline carries a **+25% contingency buffer**; re-plan triggers are stated per phase.
**Minimum Unity version: 6.6 (6000.6)** — Graph Toolkit 6.2+, native `Dictionary`
serialization 6.6+, CI matrix starts at 6.6. The floor is encoded in the UPM package
(`package.json` `unity` field) from Phase 1 and in the CI matrix.

---

## Phase 0 — Preliminary research (3–4 weeks, partially parallel)

Research items **R1–R8** and prototypes **P1–P8** cross-map one-to-one
(see [risks-and-prototypes.md](risks-and-prototypes.md) mapping table). Each produces
a doc or a throwaway prototype under `spikes/`.

### R1 — Graph Toolkit spike (↔ P1)
Build a toy graph tool (10 node types, custom inspector fields, asset subgraph) on the
experimental Graph Toolkit in Unity 6.6. Repeat on the 6.7 beta when available.
Deliverables: `spikes/gt-spike/`, `docs/gt-notes.md` (API stability diff, gotchas,
serialization model, how Unity Behavior structures its model — patterns only, see
RISK-02/RISK-11), **`docs/licenses.md`** (license conclusions: Graph Toolkit package
terms as a dependency of an MIT product; what the Unity Companion License permits for
Unity Behavior source study).
**Exit (measurable):** toy editor usable; breaking changes between 6.6/6.7 enumerated
and confined to the adapter layer; `docs/licenses.md` merged with conclusions recorded.

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

Current state: **v1.0 exists** (2026-11-23): revised after the second-agent
review (findings B1–B11) and verified against UE 5.8 engine source (tag
5.8.0-release, commit 7deeb413 — citations inline). Pending: human maintainer
sign-off; remaining [V] items listed in the spec's R7 worklist.
**Exit:** sign-offs recorded; all [V] items resolved or explicitly deferred with
rationale.

### R3 — NPBehave audit (no separate prototype; decision doc is the artifact)
Map NPBehave's model onto the R2 spec; decide **adopt & adapt** vs **spec-led
reimplementation informed by it**.
Current state: **done** — see `docs/npbehave-audit.md` (decision: spec-led
reimplementation; NPBehave dormant since commit 2022-11-26, verified).
**Exit:** decision recorded and referenced by Phase 1.

### R4 — Agent workflow harness (↔ P5)
Set up and validate the dev loop before product code:
- Unity CLI installed; project wired for `unity command` compile + test runs;
- `unity skill install opencode` + this repo's AGENTS.md;
- Vision MCP (`@z_ai/mcp-server`) configured; screenshot pipeline: editor script →
  PNG to `Artifacts/screenshots/` (CI-style artifact storage, bounded retention —
  **not committed**; `.gitignore` already excludes `Artifacts/`);
- deterministic layout-test example (`worldBound` assertions) proven on the R1 toy;
- second-agent reviewer wired (trigger point: every PR-sized change; budget: plan line
  item).
**Exit (measurable): 5 consecutive agent changes with zero human intervention beyond
the final aesthetic gate**, each passing compile→test→screenshot→review.

### R5 — Performance baselines (↔ P4)
Microbenchmarks on Unity 6.6, **Mono for iteration + IL2CPP for enforcement**:
event-driven vs polled tree, 1k/10k agents, allocation profiling. Baselines: NPBehave,
Unity Behavior. Deliverable: `docs/perf-baseline.md`.
**Canonical target set (used identically in Phase 1, Phase 5, DoD):**
≥ 1.5× the agent count of the **better** baseline (NPBehave, Unity Behavior) within a
16.6 ms frame budget, **and 0 B/tick steady-state allocation on IL2CPP**.
**Exit:** harness checked in; harness proven to detect an injected 24 B/tick leak
(that proof is P4); numbers recorded.

### R6 — CI skeleton (new; no game-code prototype, but standing on P4's harness)
GitHub Actions: Linux headless leg (Unity CLI batch: compile + golden tests + alloc
tests) + one **desktop** leg (Windows or Linux with display, for editor/UI layout
tests). Resolve and document **Unity license activation on runners**
(Personal vs Pro; activation-count limits; secrets handling; game-ci or first-party
action — decided here, tested with a trivial green run). Artifact storage for
screenshots/test output.
**Exit:** both legs green on a trivial project; activation procedure documented in
`docs/ci.md`; IL2CPP test leg decision recorded (enforced nightly or per-PR — with
rationale).

### R7 — UE 5.8 sandbox & trace-capture procedure (human-owned, ~2 days)
Install UE **5.8 (pinned minor)**, minimal C++ project, author the ~15 canonical
scenarios (same list as P3), capture execution traces (BT debugger history /
`ai.debug` logging) in a repeatable way. Output: `docs/ue-sandbox-traces/` + capture
procedure. **This is an input to R2 [V]-resolution and Phase 3 differential tests.**
**Exit:** all 15 scenarios captured; procedure re-runnable by the maintainer.

### R8 — Zero-alloc feasibility spike (↔ P8)
Implement **one latent task + one observer abort** under AGENTS.md constraints
(no iterator methods, no closures, no boxing; struct enumerators; pooled nodes/events)
and prove 0 B/tick on IL2CPP with the P4 harness. Codify the permitted-construct list
and the analyzer that enforces it.
**Exit:** spike hits 0 B/tick; banned-construct analyzer config merged.

### Phase 0 gate (go/no-go — all measurable)
- [ ] P1: toy works on 6.6; ≤ 1 day of adapter-scope fixes to rebuild on 6.7 beta
- [ ] R2: spec sign-offs (human + second agent) recorded; coverage table complete
- [x] R3: donor decision recorded (done)
- [x] P3: harness green on trivial trees (36 scenarios passing on the naive
  reference runtime, whose update model was rebuilt from UE source after two
  harness reviews); scenario format covers every traceable R2 statement
  (coverage enforced by `Scenarios_CoverEveryTraceableSpecStatement`); 18
  scenario classes enumerated in `docs/r7-sandbox-plan.md`; format documented
  in `docs/golden-format.md`
- [ ] R4/P5: 5 consecutive agent changes, zero human intervention beyond final gate
- [ ] R5/P4: harness detects injected 24 B/tick leak; baselines recorded
- [ ] R6: both CI legs green; Unity license activation procedure documented & tested
- [ ] R7: 15 scenarios captured, procedure re-runnable
- [ ] R8/P8: spike at 0 B/tick; analyzer merged
- [ ] Naming/product-name decision (RISK-12) recorded — incl. UPM package id
- [ ] If P1 fails: canvas fallback pre-estimate (+4–6 weeks, re-plan before Phase 2)

## Phase 1 — Runtime core, no editor (3–4 weeks; depends on R2, R3, R4, R5, R6, R8)

Scope: event-driven scheduler; Selector/Sequence; instant & latent tasks; blackboard
with typed keys (R2 catalog) + observers; condition decorators with Self abort;
per-agent node instancing; **C# builder API**; **UPM package skeleton + asmdef split
(runtime/editor/tests/spikes) from day one**; **debugger instrumentation points**
(compile-time switched capture, zero-cost when off — Phase 4 depends on this);
golden tests from R2 statements (all Phase-1-applicable [D]/[K] ones green);
zero-alloc enforced by R8 analyzer + alloc tests in CI.
**Exit (numeric):** CI green (R6 legs) incl. alloc tests on IL2CPP;
perf ≥ NPBehave event-driven baseline (µs/agent·update and agents-per-16.6 ms-frame
recorded vs R5 numbers); instrumentation detached-overhead test = 0 B/tick and no
measurable tick-time delta.

## Phase 2 — Authoring on Graph Toolkit (4–6 weeks; depends on R1 + Phase 1)

Scope: graph asset model (native Unity serialization; subgraphs as assets);
node UI for Phase-1 node types; blackboard asset editor; task-field inspector;
graph → runtime compile step with actionable validation errors; **versioned graph
schema from the first release** (forward-migration test: save on N, load on N+1 —
RISK-01).
- P6 torture tests run against a **minimal real Phase-2 asset model** (not the R1 toy).
- Every editor change: screenshot + `ui_diff_check` + human gate (AGENTS.md).
**Exit (scripted):** the "PatrolXPatrol" scripted authoring scenario — a non-author
(the second agent or maintainer, documented steps only) builds and runs a two-guard
patrol/investigate tree without touching code; serialization torture green; graph
save-on-6.6 → load-on-6.7-beta green.

## Phase 3 — UE-parity runtime semantics (3–4 weeks; depends on Phase 1; partially
parallelizable with Phase 2 by agent capacity, not by dependency)

Scope: full observer abort semantics (Self/LowerPriority/Both + abort search order),
services (periodic + event), Simple Parallel (both finish modes), cooldown/loop/
time-limit/force-result decorators, deferred & event-driven activation paths.
- Golden tests for the full R2 spec.
- **Mandatory differential validation:** every scenario *class* from the R7 set is
  compared against UE traces; **pass rule: trace equality modulo timing** (same node
  activation/deactivation/result sequence; timestamps ignored). Results doc signed off
  by the human maintainer against this written rubric.
**Exit:** full spec statement list green; differential doc signed off per rubric.

## Phase 4 — Debugger (4–6 weeks; depends on Phases 2–3)

Scope: Play-mode live visualization; breakpoints pausing Play mode (per-node,
per-decorator); execution history with replay (R7-style trace capture already
instrumented in Phase 1); abort-path tracing; blackboard time-travel inspection.
**Budgets:** detached = 0 B/tick and no measurable tick overhead (CI-enforced);
attached = ≤ 10% tick-time overhead in the standard stress scene (recorded number).
**Exit:** demo scenario debugged end-to-end (scripted checklist); both budgets met.

## Phase 5 — Performance & scale (3–4 weeks; depends on Phases 3–4)

Scope: profiler module (per-tree timings, abort/event counts); stress tests;
optimization; **go/no-go on the optional DOTS/Burst module** (isolated asmdef/package;
core must compile and run with zero Entities references — AGENTS.md rule).
**Exit (canonical target set, same as R5):** ≥ 1.5× better-baseline agent count within
16.6 ms frame budget **and** 0 B/tick on IL2CPP; DOTS decision recorded with numbers.

## Phase 6 — Productization (starts alongside Phase 4; continuous)

Docs (manual + API reference), samples mirroring official UE BT examples,
integration tasks (AI Navigation MoveTo etc.), UPM release (scoped registry / Git URL /
openUPM submission — channel decision with checklist, RISK-14), contribution guide,
versioned releases + changelog, **pre-release IP review** (RISK-02).

## Timeline summary (single implementer: overlap is agent-parallel only)

| Phase | Weeks (+25% buffer included in totals) | Depends on |
|---|---|---|
| 0 research | 3–4 | — |
| 1 runtime core | 4–5 | R2 R3 R4 R5 R6 R8 |
| 2 authoring | 5–7 | R1, Phase 1 |
| 3 UE parity | 4–5 | Phase 1 (agent-parallel with Phase 2) |
| 4 debugger | 5–7 | Phases 2–3 |
| 5 performance | 4–5 | Phases 3–4 |
| 6 productization | continuous | starts at Phase 4 |

**≈ 7–8 months to v1.0** (6 nominal + contingency). **Re-plan triggers:** P1 fail
(+4–6 wks canvas fallback, pre-estimated); Phase 3 differential failures > 2 scenario
classes (spec revision + 1 wk); CI license friction unresolved in R6 (pivot CI host).

## Definition of done (v1.0)

- [ ] Every in-scope R2 statement covered by a passing golden test (coverage table)
- [ ] Differential traces: all R7 scenario classes pass "equality modulo timing"
- [ ] Trees authorable entirely in the editor (scripted scenario passes by a non-author)
- [ ] Debugger: breakpoints, live flow, history replay; detached 0 B/tick + no
      measurable overhead (CI-enforced); attached ≤ 10% (recorded)
- [ ] Canonical perf target met: ≥ 1.5× better baseline in 16.6 ms frame budget,
      0 B/tick steady-state on IL2CPP
- [ ] Serialization: merge/prefab-variant torture green; graph schema versioned with
      forward-migration test; save-on-6.6 → load-on-6.7 green
- [ ] Docs + UE-parity samples; UPM package installable; CI green on 6.6 + 6.7 LTS
- [ ] Naming/trademark check passed; pre-release IP review pass (RISK-02)
