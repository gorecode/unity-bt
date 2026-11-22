# Risk Register & De-risking Prototypes — v0.2 (Nov 2026, post-critic-review)

Time-boxed prototypes with **objective pass/fail criteria**, run *before* the risk can
hurt the critical path — with two explicit exceptions: P6 (Phase-2 start, because it
needs the real asset model) and P7 (per release train, by nature). Prototypes live
under `spikes/`, are throwaway, and never become product code.

## R ↔ P mapping

| Research item | Prototype | Phase |
|---|---|---|
| R1 Graph Toolkit spike | P1 | 0 |
| R2 spec (legal review half) | P2 | 0 |
| (semantics harness) | P3 | 0→1 |
| R5 perf baselines | P4 | 0 |
| R4 agent loop | P5 | 0 |
| (serialization torture) | P6 | 2 start |
| (version-train canary) | P7 | per train |
| R8 zero-alloc spike | P8 | 0→1 |

R3 (NPBehave audit) has no prototype: the decision doc is its artifact (done).

## Register

| ID | Risk | Impact | Likelihood | Mitigation strategy | Validated by |
|---|---|---|---|---|---|
| RISK-01 | Graph Toolkit API churn breaks editor code **and saved graph assets** across 6.6→6.7→7 | high | high | adapter layer; pin version per release; **versioned graph schema + forward-migration test (save on N, load on N+1) from first release** | P1 + P7 + Phase-2 exit |
| RISK-02 | Legal: UE code/naming carryover (UE EULA); Unity Companion License carryover from studying Unity Behavior source | severe | medium | **spec-led independent implementation** (not "clean-room": same agents read UE source as spec); P2 = mechanical identifier/structure diff of our code vs studied UE modules; **paid/budgeted IP review before first public release** | P2 + pre-release review |
| RISK-03 | Observer-abort semantics systematically wrong (spec misreads UE; golden tests can't catch their own spec) | high | high | R2 formal spec + golden tests **plus mandatory differential traces per scenario class** (pass rule: trace equality modulo timing) | P3 harness + R7 traces + Phase-3 differential |
| RISK-04 | Performance regression (allocs/tick, event storms) | high | medium | zero-alloc invariants enforced by analyzer (R8) + alloc tests in CI on IL2CPP from Phase 1 | P4 + P8 + CI |
| RISK-05 | Blind-agent editor iteration ships broken UI | high | high | deterministic layout tests + screenshot/vision review (`ui_diff_check`) + human gate; pre-classified defect injection | P5 |
| RISK-06 | Graph serialization breaks VCS merges / prefab variants | high | medium | native serialization only; torture tests on a **real Phase-2 asset model** | P6 |
| RISK-07 | Unity version transitions (6.7 LTS; Unity 7 CoreCLR & no-domain-reload; alpha access may need Pro) | medium | high | no static mutable state; fast-play compatibility from day one; CI matrix on 6.6 + 6.7; **descoped to betas if alpha access unavailable** (checked in R6) | P7 + R6 |
| RISK-08 | Scope creep toward EQS/Perception/StateTree | medium | high | non-goals in README; "beyond-v1" label for requests | governance |
| RISK-09 | Agent hallucinations invent APIs / drift from spec | medium | high | compile+test loop after every change; second-agent reviewer on every PR-sized change; spec-statement IDs cited in prompts; **API audit: agent output diffed against a confirmed-API allowlist** (CI job) | CI + reviewer |
| RISK-10 | DOTS module drags Entities into non-ECS projects | medium | low | DOTS strictly optional, isolated package; gate on Phase-5 numbers | Phase-5 gate |
| RISK-11 | License contamination: Unity Companion License (Unity Behavior source) and Graph Toolkit package terms inside an MIT product | severe | medium | `docs/licenses.md` conclusions **before** any borrowing beyond patterns; RISK-02 mechanical diff covers Companion-License side too | R1 deliverable + P2 |
| RISK-12 | Naming/trademark: repo/product name leaning on Unity trademark; Asset Store rejects "Unity*" names; rename after traction is expensive | medium | high | product name + UPM package id decided **in Phase 0** (nominative use: "… for Unity"); repo name may stay as-is with product name distinct | Phase-0 gate item |
| RISK-13 | Continuity: single maintainer; hard dependency on specific models/tools (GLM-5.3-Flash, `@z_ai/mcp-server`, `ui_diff_check`) that can deprecate/lose quota | high | medium | model **roles** abstracted (any implementer agent / any vision model / any reviewer); loop documented so a second human can run it; AGENTS.md names the roles, not the products | docs + R4 harness |
| RISK-14 | Competitive/adoption: Unity revives `com.unity.behavior`; NodeCanvas/BD incumbency; distribution channel unclear | medium | medium | monitor Unity release notes for `com.unity.behavior` (trigger: revived investment → reposition toward UE-faithful niche + migration tools); distribution decision checklist in Phase 6 | monitoring |

## Prototypes

### P1 — Graph Toolkit viability (2–3 days, R1)
Toy editor on 6.6; rebuild on 6.7 beta.
- **Pass:** works on 6.6; breaking changes enumerated and confined to the adapter;
  6.7 rebuild ≤ 1 day.
- **Fail →** pin Toolkit version for v1 **or** canvas fallback (pre-estimated +4–6 wks;
  triggers re-plan before Phase 2).

### P2 — Spec-led carryover review (1 day + pre-release pass, R2-legal)
Mechanical diff of our spec wording and early code vs the UE modules studied
(identifiers, comments, structure) and vs Unity Behavior source (Companion License
side).
- **Pass:** no literal correspondence beyond unavoidable domain terms; findings rewritten.
- **Fail →** rewrite pass; ambiguous → budgeted IP review before any public release.

### P3 — Semantics correctness harness (3–4 days, Phase 0→1)
Golden-test runner: trees as neutral JSON scenarios; expected execution traces
(node transitions, abort paths, blackboard mutations) asserted. ~15 canonical UE
scenario classes ported (abort order, parallel finish modes, service timing, latent
abort cleanup).
- **Pass:** harness green on trivial trees; scenario format expressive enough for all
  R2 statements; **all 15 scenarios enumerated and captured in the R7 sandbox plan.**
- **Fail →** extend format before Phase 1 (cheap now, expensive later).

### P4 — Zero-alloc proof & harness sensitivity (2 days, R5)
Microbench 1k/10k agents; B/tick measured; event-driven vs polled baselines.
- **Pass:** harness reliably detects a single intentional 24 B/tick leak; baselines
  recorded (`docs/perf-baseline.md`).
- **Fail →** fix harness before Phase 1 (CI gates on it).

### P5 — Agent visual-loop validation (3 days, R4)
On the P1 toy: inject **pre-classified** defects — 10 geometry (overlap, clipped
label, detached edge…), 10 aesthetic (wrong emphasis, unreadable contrast…) —
classifications written down **before** the run. Run the loop: layout tests +
screenshot → vision (`ui_diff_check` / any vision model by file path).
- **Pass:** layout tests catch ≥ 9/10 geometry; vision catches ≥ 6/10 aesthetic;
  **combined loop catches 100% of defects classified blocking** (definition written
  with the classification).
- **Fail →** strengthen layout coverage; add serialized-graph→text diff as primary
  review surface; increase human-gate frequency (re-plan Phase 2 estimate).

### P6 — Serialization torture (2 days, Phase-2 start)
On a **minimal real Phase-2 asset model** (graph asset + blackboard asset + subgraph,
hand-built): scripted concurrent edits → git merge → reopen; prefab-variant overrides;
scene-reference moves; save-on-6.6 → load-on-6.7-beta.
- **Pass:** merges resolve without corruption or unrelated-node full-file conflicts;
  overrides survive; forward-load green.
- **Fail →** revisit asset layout (e.g., per-node sub-assets) **before** the format
  solidifies.

### P7 — Version-train canary (1 day, per release train; exception to Phase-0–1 rule)
CI job on 6.7 beta (and Unity 7 **beta**; alphas only if access — checked in R6):
compile, golden tests, no-domain-reload Play-mode compat.
- **Pass:** green, or enumerated breakage fixed inside the adapter layer.
- **Fail →** hold release-train support until adapter absorbs changes.

### P8 — Zero-alloc feasibility (2–3 days, R8)
One latent task + one observer abort under AGENTS.md constraints; banned-construct
analyzer configured (iterator methods, closures, LINQ, `params`, boxing in
tick-path-annotated code).
- **Pass:** 0 B/tick on IL2CPP via P4 harness; analyzer merged and enforced in CI.
- **Fail →** relax invariant explicitly (e.g., documented steady-state budget) — an
  **architectural decision**, not a silent slip.

## Sequencing

```
Week 1-2:  P4 ─┬─ P3 (harness)        P1 (GT spike) ─┬─ P2 (carryover review)
               └─ P8 (after P4)                     └─ R6 CI skeleton (parallel)
Week 2-3:  P5 (after P1)   R7 sandbox capture (human, parallel)
Week 3-4:  Phase-0 gate
Week 6-7:  P6 at Phase-2 start (real asset model)
Per train: P7
```

RISK-03 and RISK-05 are the two that can silently kill the product. Their
**harnesses** are validated before Phase 1 ends; **semantic validation itself**
(P3's golden tests over the full spec + differential traces) lands in Phase 3 with
mandatory per-class differential passes — that is where RISK-03 is actually retired.
