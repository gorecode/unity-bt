# Implementation Plan — draft v0.1 (Nov 2026)

"Fully finished product" = **UE5 behavior trees in Unity 6+, end to end**: faithful
event-driven runtime, Graph Toolkit editor, UE-grade debugger, native serialization,
docs, samples, CI, released as a UPM package — MIT.

Cadence assumption: 1 human (review/gates) + agent workflow
(GLM-5.3 implement + vision review + CLI compile loop), see
[research §5](research.md). Estimates are for that topology and are deliberately rough.

---

## Phase 0 — Preliminary research (2–3 weeks, partially parallel)

Five spikes, each producing a doc or a throwaway prototype under `spikes/`. **Gate: the
go/no-go checklist at the end of this section.**

### R1 — Graph Toolkit spike
Build a toy graph tool (10 node types, custom inspector fields, asset subgraph) on the
experimental Graph Toolkit in Unity 6.6. Repeat on the 6.7 beta when available.
Deliverable: `spikes/gt-spike/` + `docs/gt-notes.md` (API stability diff, gotchas,
serialization model, how Unity Behavior structures its model — patterns only, see
RISK-02).
**Exit:** toy editor usable; breaking-change surface between 6.6/6.7 documented.

### R2 — UE5 BT formal specification
Write `docs/ue-bt-spec.md`: complete execution semantics — scheduler, search/abort
order (incl. Lower Priority search), observer abort modes, services timing, latent task
continuation, Simple Parallel memory modes, blackboard observer firing rules, node
instancing/memory. Source: UE docs + reading engine source **as behavior spec** +
empirical probes in a UE 5.8 sandbox project where docs are ambiguous.
Deliverable: spec with ~40 numbered, testable statements (each becomes a golden test in
Phase 1/3).
**Exit:** spec review passes; every statement is decidable by a test.

### R3 — NPBehave audit
Map NPBehave's model onto the R2 spec: what fits (event-driven core), what's missing
(Simple Parallel, guard-decorator placement, lower-priority aborts, services, node
instancing), what's wrong (allocation patterns, API age). Decision fork: **adopt &
adapt** vs **clean-room reimplementation informed by it**. MIT allows both.
**Exit:** decision recorded in `docs/npbehave-audit.md`.

### R4 — Agent workflow harness
Set up and validate the dev loop before writing product code:
- Unity CLI installed; project wired for `unity command` compile + `unity` test runs;
- `unity skill install opencode` + this repo's AGENTS.md;
- Vision MCP (`@z_ai/mcp-server`) configured; screenshot pipeline: editor script →
  PNG to `Artifacts/screenshots/` → review by vision model; `ui_diff_check` before/after;
- Deterministic layout test example (`worldBound` assertions) proven on the R1 toy.
**Exit:** a full implement→compile→test→screenshot→review cycle executed end-to-end by
the agents on a trivial change.

### R5 — Performance baselines
Microbenchmarks on Unity 6.6 (Mono + IL2CPP): event-driven vs polled tree,
1k/10k agents, allocation profiling. Baselines: NPBehave, Unity Behavior.
Deliverable: `docs/perf-baseline.md` with numbers our runtime must beat.
**Exit:** harness checked in; baseline numbers recorded.

### Phase 0 gate (go/no-go)
- [ ] Graph Toolkit viable & churn acceptable (else: fallback decision — pin version / raw UI Toolkit)
- [ ] Spec complete enough to write golden tests
- [ ] Runtime donor decision made
- [ ] Agent loop executes without human babysitting per change
- [ ] Baseline numbers exist

## Phase 1 — Runtime core, no editor (3–4 weeks)

Scope: event-driven scheduler; Selector/Sequence; instant & latent tasks; blackboard
with typed keys + observers; basic decorators (condition guard with Self abort);
per-agent node instancing; **C# builder API** (trees authored in code — the editor is
irrelevant at this phase).
- Golden tests generated from R2 spec statements (target: all Phase-1-applicable
  statements green); runs headless via Unity CLI in CI.
- Zero-alloc rule enforced: allocation tests fail the build on GC allocs in steady-state
  tick (10k ticks, 1k agents).
**Exit:** test suite green on CI (Linux headless + one desktop runner), perf ≥ NPBehave.

## Phase 2 — Authoring on Graph Toolkit (4–6 weeks)

Scope: graph asset model (native Unity serialization — subgraphs as assets); node UI for
all Phase-1 node types; blackboard asset editor; inspector for task fields; drag/wire/
undo/redo via Toolkit primitives; graph → runtime compile step (validation with
actionable error nodes).
- Serialization torture tests: git merge of concurrent edits, prefab variants, scene
  references (this is the anti-BD proof point — RISK-06).
- Every editor change: screenshot + `ui_diff_check` + human gate (AGENTS.md).
**Exit:** author + run a tree without touching code; serialization tests green.

## Phase 3 — UE-parity runtime semantics (3–4 weeks)

Scope: full observer abort semantics (Self/LowerPriority/Both + abort search order),
services (periodic + event), Simple Parallel (both memory modes), cooldown/loop/
time-limit/force-result decorators, deferred & event-driven activation paths.
- Golden tests for the full R2 spec; differential scenarios vs UE 5.8 sandbox where
  feasible (same tree described in both, compare execution traces by hand once per
  scenario class).
**Exit:** full spec statement list green; trace comparison doc signed off.

## Phase 4 — Debugger (4–6 weeks)

Scope: Play-mode live visualization (running/aborted states on nodes), breakpoints that
pause Play mode (per-node, per-decorator-condition), **execution history with replay**
(inspired by UE history + the browser editor's replay timeline), abort-path tracing,
time-travel inspection of blackboard values.
- Debugger overhead must be zero when not attached (alloc tests with debugger on/off).
**Exit:** demo scenario debugged end-to-end; overhead tests green.

## Phase 5 — Performance & scale (3–4 weeks, then optional track)

Scope: profiler module (per-tree timings, abort counts, event counts); stress tests;
optimization passes; decision point for the **optional DOTS/Burst module** (isolated
asmdef/package, never a core dependency) gated on Phase-5 numbers.
**Exit:** targets from R5 beaten with margin; go/no-go on DOTS track recorded.

## Phase 6 — Productization (continuous from Phase 4)

Docs (manual + API), samples mirroring official UE BT examples, integration tasks
(AI Navigation MoveTo etc.), UPM package layout + scoped registry / Git URL install,
versioned releases + changelog, contribution guide, CI matrix (6.6 + 6.7 LTS when out,
then 7 alphas).

## Timeline summary (rough)

| Phase | Weeks | Depends on |
|---|---|---|
| 0 research | 2–3 | — |
| 1 runtime core | 3–4 | R2, R3, R5 |
| 2 authoring | 4–6 | R1, Phase 1 |
| 3 UE parity | 3–4 | Phase 1 |
| 4 debugger | 4–6 | Phases 2–3 |
| 5 performance | 3–4 | Phases 3–4 |
| 6 productization | continuous | — |

**≈ 6 months to v1.0** on the assumed topology; the critical path is
R1 → Phase 2 (editor) and Phase 4 (debugger) — both are where the agent workflow is
weakest (visual iteration), hence their risks get the largest prototypes.

## Definition of done (v1.0)

- [ ] Every in-scope spec statement (R2) covered by a passing golden test
- [ ] Trees authorable entirely in the editor; zero custom serialization formats
- [ ] Debugger: breakpoints, live flow, history replay — zero overhead when detached
- [ ] Perf targets: ≥ NPBehave event-driven baseline; 0 B/tick steady-state allocs
- [ ] Docs + UE-parity samples; installable UPM package; CI green on 6.6 + 6.7 LTS
- [ ] Legal check: clean-room review pass (RISK-02)
