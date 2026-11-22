# AGENTS.md — working rules for coding agents (and humans) on unity-bt

Binding rules. When an agent's habit conflicts with a rule here, the rule wins.

## Legal (hard)

- **UE engine source is a spec, not a codebase.** Reading Epic's source to understand
  behavior is allowed. Copying identifiers, comments, code structure, or literal
  translations is NOT (UE EULA). Write everything clean-room; name things our way;
  unavoidable domain terms (Blackboard, Observer Abort as concepts) are fine.
- When unsure whether a piece resembles UE source too closely — rewrite it and flag
  the area in the PR description.

## Architecture invariants (hard)

1. **Zero allocations in the tick path.** No `new`, no LINQ, no boxing, no closures,
   no params arrays, no string ops in per-tick/per-event runtime code. Pools for node
   instances, event args, and enumerator objects. CI fails on steady-state GC allocs.
2. **Native Unity serialization only.** Graphs, blackboards, and node data are plain
   serialized Unity assets. No custom snapshot/capture subsystems for variables.
3. **No static mutable state** in runtime code (Unity 7 removes domain reload; fast
   Play mode is the default). Everything re-initializes through explicit lifecycles.
4. **DOTS stays out of the core.** If it ever exists, it is a separate optional package;
   core must compile and run with zero Entities references.
5. Runtime assembly references no editor assemblies and no Graph Toolkit.

## Workflow (hard)

- A change is **not done** until: it compiles in a real Unity editor via Unity CLI
  (`unity command` / batchmate) **and** the relevant tests pass **and** no new
  allocation-test failures. Console errors block completion even if "unrelated".
- **Never invent APIs.** If an interface you need isn't confirmed to exist, stop and
  check (compile loop catches this; don't silence it — resolve it).
- Editor/UI changes additionally require: deterministic layout assertions
  (`worldBound`/`layout` tests) for geometry; a screenshot written to
  `Artifacts/screenshots/<pr-or-branch>/<name>.png`; a vision review pass
  (`ui_diff_check` before/after or GLM-5.3-Flash by file path) — and the human gate
  for anything aesthetic.
- Comments describe the code as it is now. No history essays, no "why we chose X over
  Y" in code — that belongs in `docs/`. Link the doc instead.
- Every public runtime API ships with a test. No test, no merge.

## Conventions

- Product code: English identifiers, US spelling. Docs: English.
- Node/task/decorator naming follows `docs/ue-bt-spec.md` mapping table (our names, not
  Epic's).
- Golden-test scenarios live in a neutral JSON format; never hardcode expected traces
  in C#.
- Experimental/throwaway code lives under `spikes/` and is never referenced from
  product assemblies.
