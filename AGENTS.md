# AGENTS.md — working rules for coding agents (and humans) on unity-bt

Binding rules. When an agent's habit conflicts with a rule here, the rule wins.
Roles below are **roles, not products**: any capable implementer agent, any vision
model, any second frontier model can fill them (continuity, RISK-13).

## Legal (hard)

- **Spec-led independent implementation.** UE engine source and Unity Behavior source
  (Unity Companion License) may be read to understand *behavior*. Copying identifiers,
  comments, code structure, or literal translations is NOT allowed. Write everything
  independently; name things our way; unavoidable domain terms (Blackboard, Observer
  Abort as concepts) are fine. Carryover is checked mechanically (P2) and by IP review
  before the first public release.
- When unsure whether a piece resembles studied source too closely — rewrite it and
  flag the area in the change description.

## Architecture invariants (hard)

1. **Zero allocations in the tick path.** No `new`, no LINQ, no boxing, no closures,
   no `params`, no string ops in per-tick/per-event runtime code. Latent tasks must not
   use iterator methods (`yield`) or `async` state machines in the tick path — use
   struct state machines. Pools for node instances, event payloads, and enumerators.
2. **Native Unity serialization only.** Graphs, blackboards, and node data are plain
   serialized Unity assets. No custom snapshot/capture subsystems for variables.
3. **No static mutable state** in runtime code: must support Enter-Play-Mode without
   domain reload (Unity 6.6 default for new projects) and Unity 7's removal of domain
   reload. Everything re-initializes through explicit lifecycles.
4. **DOTS stays out of the core.** If it ever exists, it is a separate optional
   package; core compiles and runs with zero Entities references.
5. Runtime assemblies reference no editor assemblies and no Graph Toolkit.
6. **Debugger instrumentation is compiled in, switch-off-able at zero cost** (const/
   conditional switch); detached overhead must stay at 0 B/tick.

### Enforcement (rules above are only real if these run)

- **Roslyn analyzer / banned-API list** for tick-path code (annotated
  `[TickPath]`): iterator methods, closures, LINQ, `params`, `string.Concat` etc.
- **IL scan** in CI: no static writable fields in runtime assemblies.
- **asmdef-graph CI check**: edges runtime→editor / runtime→Graph Toolkit /
  anything→spikes fail the build.
- **Public-API ↔ test inventory CI job**: every public runtime API must be referenced
  by ≥1 test; diff against a confirmed-API allowlist (also the hallucination check).
- **Allocation tests on IL2CPP** gate every PR (steady-state 0 B/tick).

## Workflow (hard)

- A change is **not done** until: it compiles in a real Unity editor via Unity CLI
  (`unity command` / `unity eval`; classic editor `-batchmode -executeMethod` as
  fallback) **and** the relevant tests pass **and** no new allocation-test failures.
  Tool/command names codified here must be verified against the installed CLI before
  first use; fix this file if they drift.
- **Never invent APIs.** If an interface you need isn't confirmed to exist, stop and
  check — the compile loop catches this; don't silence it, resolve it.
- **Console errors block completion.** "Error" = `LogType.Error`/`LogType.Exception`
  entries after domain reload settles and project import completes. Known-unrelated
  noise goes into the reviewed, versioned allowlist file
  `docs/console-allowlist.txt`; anything else blocks.
- **Second-agent review** on every PR-sized change (any capable frontier model with
  clean context; findings addressed or explicitly waived).
- Editor/UI changes additionally require: deterministic layout assertions
  (`worldBound`/`layout` tests) for geometry; a screenshot written to
  `Artifacts/screenshots/<branch>/<name>.png` — **artifact storage with bounded
  retention, never committed** (`.gitignore` excludes `Artifacts/`); a vision review
  pass (`ui_diff_check` before/after, or any vision model given the file path) — and
  the human gate for anything aesthetic.
- Comments describe the code as it is now. No history essays, no "why we chose X over
  Y" in code — that belongs in `docs/`. Link the doc instead.
- Every public runtime API ships with a test (enforced by the inventory job).

## Conventions

- Product code: English identifiers, US spelling. Docs: English.
- Node/task/decorator naming follows the mapping table in `docs/ue-bt-spec.md`.
- Golden-test scenarios live in a neutral JSON format; never hardcode expected traces
  in C#.
- Experimental/throwaway code lives under `spikes/` and is never referenced from
  product assemblies (asmdef check enforces).
- Minimum Unity version: **6.6 (6000.6)** — encoded in the UPM package and CI matrix.
