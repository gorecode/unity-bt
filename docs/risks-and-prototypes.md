# Risk Register & De-risking Prototypes — v0.3 PROPOSAL (Nov 2026, code-first authoring)

> **Status: proposal, paired with implementation-plan v0.3.** The direction is the
> maintainer's decision of 2026-11-24; this reworked register replaces v0.2 (last
> state: commit `7ef53da`) once the plan is accepted.

Time-boxed prototypes with **objective pass/fail criteria**, run *before* the risk can
hurt the critical path — with two explicit exceptions: P7 (per release train, by
nature) and the debugger-window UI loop at Phase 4 start (it needs the real window).
Prototypes live under `spikes/`, are throwaway, and never become product code.

## R ↔ P mapping

Most research items pair one-to-one with a prototype; the exceptions are listed
below the table.

| Research item | Prototype | Phase | v0.3 |
|---|---|---|---|
| R1 Graph Toolkit spike | P1 | 0 | **closed** — not adopted |
| R2 spec (legal review half) | P2 | 0 | unchanged |
| (semantics harness) | P3 | 0→2 | unchanged |
| R5 perf baselines | P4 | 0 | unchanged |
| R4 agent loop | P5 | 0 → 4 | **deferred to Phase 4** — there is no UI before the debugger window |
| (serialization torture) | P6 | — | **dropped** — no graph assets |
| (version-train canary) | P7 | per train | unchanged, smaller surface |
| R8 zero-alloc spike | P8 | 0→1 | **extended** — also chooses the state model |
| R9 authoring API | P9 | 0 | **new** |

Without a prototype: R3 (the NPBehave audit's decision doc is its artifact, done),
R6 (the CI skeleton stands on P4's harness), R7 (the sandbox is human-owned), and
P3 and P7 (no research item of their own).

## Register

| ID | Risk | Impact | Likelihood | Mitigation strategy | Validated by | v0.3 |
|---|---|---|---|---|---|---|
| RISK-01 | Graph Toolkit API churn breaks editor code **and saved graph assets** across 6.6→6.7→7 | high | high | — | — | **retired while no editor exists**; reopens if RISK-14's trigger re-evaluates an editor |
| RISK-02 | Legal: UE code/naming carryover (UE EULA); Unity Companion License carryover from studying Unity Behavior source | severe | medium | **spec-led independent implementation** (not "clean-room": same agents read UE source as spec); P2 = mechanical identifier/structure diff of our code vs studied UE modules; **paid/budgeted IP review before first public release** | P2 + pre-release review | unchanged |
| RISK-03 | Observer-abort semantics systematically wrong (spec misreads UE; golden tests can't catch their own spec) | high | high | R2 formal spec + golden tests **plus mandatory differential traces per scenario class** (pass rule: trace equality modulo timing) | P3 harness + R7 traces + Phase-2 differential | unchanged |
| RISK-04 | Performance regression (allocs/tick, event storms) | high | medium | zero-alloc invariants enforced by analyzer (R8) + alloc tests in CI on IL2CPP from Phase 1 | P4 + P8 + CI | unchanged |
| RISK-05 | Blind-agent editor iteration ships broken UI | high | high | deterministic layout tests + screenshot/vision review + human gate; defect classification written before the run | — | **deferred to Phase 4, not retired**: the P5 loop has not been run; its thresholds are part of the Phase-4 exit |
| RISK-06 | Graph serialization breaks VCS merges / prefab variants | high | medium | — | — | **retired while trees are code**; returns with any future front end that serializes trees |
| RISK-07 | Unity version transitions (6.7 LTS; Unity 7 CoreCLR & no-domain-reload; alpha access may need Pro) | medium | high | no static mutable state; fast-play compatibility from day one; CI matrix on 6.6 + 6.7; **descoped to betas if alpha access unavailable** (checked in R6) | P7 + R6 | **narrowed**: the product's Unity API surface is engine core only |
| RISK-08 | Scope creep toward EQS/Perception/StateTree | medium | high | non-goals in README; "beyond-v1" label for requests | governance | unchanged — a visual editor is itself now a beyond-v1 request |
| RISK-09 | Agent hallucinations invent APIs / drift from spec | medium | high | compile+test loop after every change; second-agent reviewer on every PR-sized change; spec-statement IDs cited in prompts; **API audit: agent output diffed against a confirmed-API allowlist** (CI job); building a definition rejects an invalid tree with a node-path message | CI + reviewer + P9 | strengthened |
| RISK-10 | DOTS module drags Entities into non-ECS projects | medium | low | DOTS strictly optional, isolated package; gate on Phase-5 numbers | Phase-5 gate | unchanged |
| RISK-11 | License contamination: Unity Companion License (Unity Behavior source) and Graph Toolkit package terms inside an MIT product | severe | medium | `docs/licenses.md` conclusions **before** any borrowing beyond patterns; RISK-02 mechanical diff covers Companion-License side too | P2 | **narrowed** to Unity Behavior and UE source; Graph Toolkit is no longer referenced |
| RISK-12 | Naming/trademark: repo/product name leaning on Unity trademark; Asset Store rejects "Unity*" names; rename after traction is expensive | medium | high | product name + UPM package id decided **in Phase 0** (nominative use: "… for Unity"); repo name may stay as-is with product name distinct | Phase-0 gate item | unchanged |
| RISK-13 | Continuity: single maintainer; hard dependency on specific models/tools that can deprecate/lose quota | high | medium | model **roles** abstracted (any implementer agent / any vision model / any reviewer); loop documented so a second human can run it; AGENTS.md names the roles, not the products | docs + R4 harness | unchanged |
| RISK-14 | Competitive/adoption: Unity revives `com.unity.behavior`; NodeCanvas/BD incumbency; distribution channel unclear | medium | medium | monitor Unity release notes for `com.unity.behavior`; distribution decision checklist in Phase 6 | monitoring | **raised**: no visual editor narrows the audience to code-first teams and agent-driven projects. Position honestly (code-first, UE-faithful, agent-validated); C# samples; the tree definition is the seam for a future front end. Re-evaluate an editor only when 6000.7+ `NodeView<T>` ships and users ask for it |
| RISK-15 | **Composing a tree through the API is not agent-friendly in practice** — part of the premise of v0.3 | high | medium | surface designed against three constraints (reads like the tree; expresses every scenario; usable from the docs alone); text dump as the review surface; validation messages name the node path | P9 leg A (Phase 0), re-run at the Phase-3 exit | **new** |
| RISK-16 | Without an asset to edit, non-programmers cannot tune trees | medium | high | per-agent parameters are serialized component fields and sit next to the structure in the tree class | prefab-override test and the tuning step of the Phase-3 exit | **new** |
| RISK-17 | Shared definitions and pooled runners leak state between agents, or across Play sessions without domain reload | high | medium | definition immutable after build and holding no session state; explicit lifecycles (AGENTS.md invariant 3); multi-runner golden scenarios (S60); fast-play test at the Phase-1 exit | P8 + Phase-1 exit | **new** |
| RISK-18 | The debugger has no graph asset to draw from | medium | low | the tree definition is plain data; the read-only tree window renders from it; the text dump is tested from Phase 1 | scripted checklist at Phase-4 start | **new** |
| RISK-19 | **Writing custom tasks and services under the zero-alloc rules is too error-prone** — the other part of the v0.3 premise | high | medium | the authoring guide carries worked examples of a latent task with an asynchronous abort and of a periodic service; analyzer messages point to the pattern | P9 leg B; golden tests | **new** |
| RISK-20 | The C# API is the only tree format: a breaking change is a source break that no loader can migrate | medium | high | semantic versioning from the first release; the public-API inventory job doubles as the compatibility log; a deprecation stays for one minor release. There are no asset files to migrate, but users' trees are source: every breaking change is a compile error in user code, and the deprecation cycle is what protects them | API inventory CI job + release checklist (Phase 6) | **new** |
| RISK-21 | Only agents are measured: the guide and every exit test agents, never people | medium | medium | the guide is written for agents and for people; the maintainer — who wrote neither the API nor the guide — runs the Phase-3 scripted scenario unaided | Phase-3 exit (one human's run: a data point, not a population; a second human is not budgeted) | **new** |
| RISK-22 | String-named parameter slots and IL2CPP stripping silently break trees in a build: a renamed field or slot, a tree type nothing references statically | high | medium | a binding failure is an error when the runner is created; the Phase-1 edit-mode test instantiates every runner type; runner types are marked against stripping; an IL2CPP player smoke test runs in the nightly leg (R6) | Phase-1 exit + nightly IL2CPP leg | **new** |

## Prototypes

### P1 — Graph Toolkit viability (2–3 days, R1) — CLOSED 2026-11-24
Toy editor on 6.6; rebuild on 6.7 beta.
- **Result:** works on 6.6 (core pass, round-trip test green). Neither 6.6 nor 6.7
  can attach a decorator to a branch; 6.7's `NodeView<T>` approximates it inside the
  node card. Notes: [gt-notes.md](gt-notes.md).
- **Outcome:** not adopted; the visual editor is dropped (plan v0.3, decision
  record). The 6.7 rebuild was not run.

### P2 — Spec-led carryover review (1 day + pre-release pass, R2-legal)
Mechanical diff of our spec wording and early code vs the UE modules studied
(identifiers, comments, structure) and vs Unity Behavior source (Companion License
side).
- **Pass:** no literal correspondence beyond unavoidable domain terms; findings rewritten.
- **Fail →** rewrite pass; ambiguous → budgeted IP review before any public release.

### P3 — Semantics correctness harness (3–4 days, Phase 0→2)
Golden-test runner: trees as neutral JSON scenarios; expected execution traces
(node transitions, abort paths, blackboard mutations) asserted. The canonical UE
scenario classes ported (abort order, parallel finish modes, service timing, latent
abort cleanup).
- **Pass:** harness green on trivial trees; every traceable R2 statement has a
  scenario; every scenario class enumerated and captured in the R7 sandbox plan.
- **State (2026-11-24):** harness pass. Of 60 scenarios, 37 are executed on the
  naive reference runtime; 23 are skipped — 11 carry an R7 `verify:` gate, 20 need a
  capability the reference runtime does not implement (parallels, services,
  subtrees, multi-runner, the debugger), 8 of them for both reasons. The format's
  known gaps ([golden-format.md](golden-format.md), "Not yet expressible") are
  closed before the statements they carry gate a test.
- **From Phase 1:** every scenario tree is built through the public builder API.

### P4 — Zero-alloc proof & harness sensitivity (2 days, R5)
Microbench 1k/10k agents; B/tick measured; event-driven vs polled baselines.
- **Standard workload** (reused by P8, Phase 1 and Phase 5): 1k and 10k agents, each
  running the same tree — a latent task under an observer-abort guard — updated every
  frame, with blackboard writes firing the observer at a fixed rate; an event-driven
  and a polled variant.
- **Pass:** harness reliably detects a single intentional 24 B/tick leak; baselines
  recorded (`docs/perf-baseline.md`).
- **Fail →** fix harness before Phase 1 (CI gates on it).

### P5 — Agent visual-loop validation (3 days, R4) — DEFERRED to Phase 4
There is no UI to validate a loop against until the debugger window exists, and
the loop itself — classified defects, layout tests, vision triage, human gate — has
not been run; spike screenshots were only reviewed ad hoc. Phase 4 starts by running
it there, on a small surface: defect classifications written down before the run,
layout tests for geometry, vision for triage, human gate.
- **Pass (moved into the Phase-4 exit):** layout tests catch ≥ 9/10 geometry defects;
  vision ≥ 6/10 aesthetic; the combined loop catches 100% of defects classified
  blocking.
- **Fail →** strengthen layout coverage; use the text dump as the primary review
  surface; raise the human-gate frequency before polishing the window.

### P6 — Serialization torture (2 days) — DROPPED
Graph, blackboard and subgraph assets no longer exist. The authoring side of the
old risk (concurrent edits to one graph file) is gone; the tuning side is RISK-16.
The prototype returns with any future front end that serializes trees.

### P7 — Version-train canary (1 day, per release train)
CI job on 6.7 beta (and Unity 7 **beta**; alphas only if access — checked in R6):
compile, golden tests, no-domain-reload Play-mode compat.
- **Pass:** green, or the breakage fixed in the runtime. There is no editor adapter
  left to absorb changes; the surface is engine core only.
- **Fail →** hold release-train support until fixed.

### P8 — Zero-alloc feasibility (3–4 days, R8)
One latent task under one observer abort, built **twice**: with pooled per-runner
node instances, and with a shared definition plus per-runner state arrays.
Banned-construct analyzer configured (iterator methods, closures, LINQ, `params`,
boxing in tick-path-annotated code).
- **Workload:** the P4 standard workload at 1k and 10k agents, event path included —
  blackboard writes that fire the observer.
- **Choice rule:** a model qualifies only at 0 B/tick at both agent counts; of the
  qualifying models the one with the lower µs/update wins; within 10% of each other,
  the simpler one (pooled instances).
- **Pass:** a model qualifies and is at or below the better baseline's µs/update (P4
  numbers); the decision is recorded with numbers; analyzer merged and enforced in
  CI.
- **Fail →** relax invariant explicitly (e.g., documented steady-state budget) — an
  **architectural decision**, not a silent slip.

### P9 — Authoring API & agent usability (3–4 days, R9)
A thin builder that only produces tree definitions and text dumps (no runtime
needed), plus the golden loader adapted to build each scenario tree through it.
Two legs, because the premise of v0.3 can fail in two different places.

**Leg A — composing** (week 1–2).
- *Expressiveness:* all 60 golden scenarios build through the builder without error.
- *Reference trees:* one scenario per canonical class — `S02-S03-selector-fallback`,
  `S06-sequence-latent-park`, `S07-S46-one-task-per-update`, `S04-S49-keep-running`,
  `S18-self-abort`, `S19-lower-priority-abort`, `S20-both-abort`,
  `S29-slow-abort-blocks`, `S09-parallel-activates-both`, `S22-cooldown-window`,
  `S23-loop-finite-failure-passthrough`, `S24-time-limit-fails`,
  `S32-service-on-task`, `S34-authored-default-read`, `S39-subtree-runs-inline`,
  `S25-force-success-converts-self-abort`, `S54-notify-value-change-restarts`,
  `S62-restart-on-exhaustion`.
- *Method:* two agents in separate clean-context sessions, given only the authoring
  guide and the API reference (not the plan), write each reference tree from a
  one-line brief. The 18 briefs are written before the run and kept in the P9 notes;
  each names the node kinds, the nesting, the decorator order and the parameter
  values, so no tree has to be guessed from a trace. Each tree is compared with the
  scenario's tree by text dump: node types, nesting and order, decorators, services,
  keys and parameter values must match; node ids are ignored.
- *Pass (per agent):* at least 16 of 18 trees match; a median of ≤ 2 compile
  attempts per tree (one `unity recompile` run is one attempt, also when it reports
  errors); three change requests (move a guard up a level, add a service, wrap a
  branch in a loop) each change the dump only inside the affected subtree.

**Leg B — writing nodes** (after P8, on its chosen state model).
- Each agent writes, from the guide alone: one latent task with an asynchronous
  abort, one periodic service, and a runner that binds two per-agent parameters to
  slots.
- *Pass (per agent):* the code passes the banned-construct analyzer unaided; an
  allocation test shows 0 B over 10,000 updates; a golden check of the agent's own
  tree passes.

**Fail →** revise the surface and the guide, re-run the failed leg once. Failing
twice means C# is not the right front end — evaluate a data format before Phase 1.

## Sequencing

```
Week 1-2:  P4 ─┬─ P9 leg A (composing)        P2 (carryover review)
               └─ P8 (after P4; both state models) ── P9 leg B (writing nodes)
Week 2-3:  R7 sandbox capture (human, parallel)
Week 2-3:  Phase-0 gate (2-3 weeks from 2026-11-24)
Per train: P7
Phase 4 start: P5 (screenshot → vision loop) and the desktop CI leg
```

RISK-03 and the two halves of the v0.3 premise (RISK-15, RISK-19) are what can
silently kill the product: the first makes the runtime wrong, the others remove the
reason for v0.3. Their **harnesses** are validated in Phase 0 (P3 is done, P8 and P9
are new); **semantic validation itself** (the golden tests over the full spec +
differential traces) lands in Phase 2 with mandatory per-class differential passes —
that is where RISK-03 is actually retired.
