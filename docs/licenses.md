# License conclusions (R1 deliverable; RISK-02 / RISK-11)

Recorded 2026-11-23 after the P1 Graph Toolkit spike. This file records working
conclusions; the paid pre-release IP review (implementation-plan Phase 6) remains
the final gate.

## Unity Graph Toolkit (built-in editor module)

- Graph Toolkit ships **inside the Unity Editor** (`UnityEditor.GraphToolkitModule`,
  namespace `Unity.GraphToolkit.Editor`) and is governed by the Unity Editor
  Software Terms / EULA that already cover the user's editor install.
- Our MIT product only **references the module's public editor API** from its own
  editor assembly (compile-time type references). We do not copy, vendor, or
  redistribute any Unity code.
- Conclusion: **compatible with an MIT product.** The package's `unity` floor
  (6000.6) is the honest declaration of the API dependency. The deprecated
  `com.unity.graphtoolkit` shim must never appear in any manifest we ship.

## Unity Behavior (`com.unity.behavior`) source study

- Source-available under the **Unity Companion License**: reading to learn behavior
  and patterns is permitted; redistribution and literal reuse are restricted.
- We study it for **patterns only** (how it structures graph model / blackboard /
  subgraph authoring); no identifiers, comments, structure, or code carry over
  (AGENTS.md legal rule). Phase-2 design notes must cite patterns, not code.
- The P2 mechanical carryover diff + pre-release IP review cover this dependency.

## Graph Toolkit Samples (`com.unity.graphtoolkit-samples`)

- Not used by the spike or the product. If a sample is ever used as a starting
  point, do a terms pass on that package version **before** borrowing, and prefer
  reimplementation from the documented public API (as the spike did).

## Unreal Engine 5.8 source (private GitHub, tag `5.8.0-release`, commit `7deeb413`)

- Access and use are governed by the UE EULA. We read engine source **as a
  behavioral specification only** (AGENTS.md): semantics are restated in
  `docs/ue-bt-spec.md` in our own words with file:line citations (facts, not
  expression). No UE identifiers, code, or structure enter the product; the spec's
  naming map deliberately differs from UE's.
- The P2 mechanical identifier/structure diff and the pre-release IP review cover
  this side too.

## Our dependencies

- Product: none at runtime beyond the Unity engine itself (editor package depends
  on the GTK module; runtime assembly references no editor assembly — AGENTS.md
  invariant 5).
- Dev project: Unity CLI (`com.unity.pipeline`) and test framework are dev-tooling
  only, not product dependencies.
- Tests only: `Newtonsoft.Json` (via Unity's `com.unity.nuget.newtonsoft-json`,
  a transitive dependency of the dev project) parses the golden scenarios; it is
  referenced from the test assembly's `precompiledReferences` and ships in no
  product assembly. Newtonsoft.Json is MIT-licensed.
