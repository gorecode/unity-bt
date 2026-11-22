# Risk Register & De-risking Prototypes — v0.1 (Nov 2026)

Every high risk gets a **time-boxed prototype with objective pass/fail criteria**,
scheduled in Phase 0–1 *before* the risk can hurt the critical path. Prototypes live
under `spikes/`, are throwaway, and exist to kill risks — not to become product code.

## Register

| ID | Risk | Impact | Likelihood | Mitigation strategy | Validated by |
|---|---|---|---|---|---|
| RISK-01 | Graph Toolkit API churn (experimental) breaks editor across 6.6→6.7→7 | high | high | isolate Toolkit behind thin adapter; pin version per release; CI matrix | P1 |
| RISK-02 | Legal: UE EULA violation by copying engine code/names | severe | medium | clean-room policy in AGENTS.md; UE source = behavior spec only; distinct naming; review gate | P2 |
| RISK-03 | Observer-abort semantics incorrect (ordering, lower-priority search, latent interaction) | high | high | formal spec (R2) → golden tests; differential traces vs UE 5.8 sandbox | P3 |
| RISK-04 | Performance regression (allocs per tick, event storms) | high | medium | zero-alloc architecture rule + allocation tests in CI from Phase 1 | P4 |
| RISK-05 | Blind-agent editor iteration (GLM has no vision) ships broken UI | high | high | deterministic layout tests + screenshot/vision review + human gate | P5 |
| RISK-06 | Graph serialization breaks VCS merges / prefab variants (the BD anti-pattern) | high | medium | native serialization only; torture tests with scripted merges | P6 |
| RISK-07 | Unity version transitions (6.7 LTS, Unity 7 CoreCLR, no-domain-reload) | medium | high | no static-state assumptions; fast-play-mode compatibility from day one; CI matrix incl. 7 alphas | P7 |
| RISK-08 | Scope creep toward EQS/Perception/StateTree | medium | high | non-goals in README; requests parked in a "beyond-v1" label | governance |
| RISK-09 | Agent hallucinations invent APIs / drift from spec | medium | high | compile+test loop after every change (Unity CLI); reviewer agent; spec-first prompting | P5 (loop) + CI |
| RISK-10 | DOTS module drags Entities into non-ECS projects (BD Pro 3 anti-pattern) | medium | low | DOTS strictly optional, isolated package; gate on Phase-5 numbers | Phase 5 gate |

## Prototypes

### P1 — Graph Toolkit viability (2–3 days, Phase 0 / R1)
Build the toy editor; then bump the project to the 6.7 beta and rebuild.
- **Pass:** toy works on 6.6; breaking changes enumerated and confined to the adapter
  layer; rebuild on 6.7 takes ≤ 1 day.
- **Fail →** fallback decision: pin Toolkit version for v1, or invest in raw
  UI Toolkit canvas (cost estimate re-run; plan revisit).

### P2 — Clean-room legal check (1 day, Phase 0; re-run before v1.0)
Write the UE-BT spec (R2) from docs + behavior probes; diff-review spec wording and
early runtime code for UE-source carryover (identifiers, comments, structure).
- **Pass:** policy followed, no literal correspondence beyond unavoidable
  domain terms ("Blackboard", "Observer Abort" as concepts are fine).
- **Fail →** rewrite pass; if ambiguous, IP review before any public release.

### P3 — Semantics correctness harness (3–4 days, Phase 0→1)
Golden-test runner: trees described in a neutral JSON scenario format; expected
execution traces (node transitions, abort paths, blackboard mutations) asserted.
Port ~15 canonical UE BT scenarios (abort order, parallel memory, service timing,
latent abort cleanup).
- **Pass:** harness green on trivial trees; scenario format expressive enough for all
  R2 statements.
- **Fail →** extend format; delay Phase 1 start (cheap now, expensive later).

### P4 — Zero-alloc proof (2 days, Phase 0 / R5)
Microbench: 1k and 10k agents running a representative tree; measure B/tick
steady-state via profiler + GC counter; compare event-driven vs polled baseline.
- **Pass:** harness reliably detects a single intentional 24 B/tick leak; baselines
  recorded.
- **Fail →** harness is not trustworthy → fix harness before Phase 1 (we will be
  gating CI on it).

### P5 — Agent visual-loop validation (3 days, Phase 0 / R4)
On the P1 toy editor: inject 10 known visual defects (overlap, clipped label, wrong
port color, detached edge); run the loop — deterministic layout tests + screenshot →
vision model (`ui_diff_check` / GLM-5.3-Flash) — and measure detection rate.
- **Pass:** layout tests catch all geometry defects (≥9/10); vision catches ≥6/10
  aesthetic defects; combined loop leaves 0 unnoticed *blocking* defects.
- **Fail →** strengthen layout-test coverage; add editor-state dumps (serialized
  graph → text diff) as the primary review surface; increase human gate frequency.

### P6 — Serialization torture (2 days, Phase 2 start)
Scripted: two clones edit the same graph asset (different nodes) → git merge → reopen;
prefab-variant scenario with overridden blackboard values; scene-reference moves.
- **Pass:** merges resolve without corruption or full-file conflicts on unrelated
  nodes; prefab overrides survive.
- **Fail →** revisit asset layout (e.g., per-node sub-assets) *before* the format
  solidifies.

### P7 — Version-transition canary (1 day, per release train)
CI job on 6.7 beta / Unity 7 alpha: compile, run golden tests, fast-play-mode
compat (no domain reload).
- **Pass:** green or enumerated breakage with adapter-scoped fixes.
- **Fail →** hold release-train support until adapter absorbs changes.

## Sequencing

```
Week 1-2:  P4 ─┬─ P3 (spec harness)      P1 (GT spike) ─┬─ P2 (legal)
                └─ P5 (after P1)                        └─ P7 (from beta drops)
Week 3:    P6 runs at Phase-2 kickoff, using P1's toy as substrate
```

Risks RISK-03, RISK-05 are the two that can silently kill the product (wrong semantics;
broken UI shipped blind) — both get their validation *before* Phase 1 ends, not at the
end.
