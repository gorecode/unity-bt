# Research Notes — November 2026 session

Findings that shaped the plan. All facts checked against primary sources
(docs.unity3d.com / docs.unity.com, docs.z.ai, GitHub, Unity discussions, Reddit,
V2EX, vendor blogs) between 2026-11-11 and 2026-11-22. Links inline.

---

## 1. Target platform state

- **Latest stable editor: Unity 6.6** (stream `6000.6.x`, e.g. `6000.6.9f1`, Nov 12 2026).
  The `6000.0.x` stream is the 3-year LTS (bugfix-only backports).
- **Unity 6.7 LTS** announced at Unite Tokyo (Nov 17 2026), expected December 2026 —
  2 years of support; adds Neural Texture Compression (experimental), customizable 2D
  graphics, Physics Core 2D authoring, 2D profiler.
- **Unity 7** (~GDC, March 2027): full move to **CoreCLR**, domain reload removed
  entirely. Unity 6.6 already defaults new projects to *Enter Play Mode without domain
  reload* — a runtime designed for that (no static-state assumptions, proper re-init)
  is forward-compatible by construction.
- Relevant 6.6 features for this project: native `Dictionary` serialization (blackboard
  keys), new Hierarchy window (custom columns — debugger can integrate), Build Analysis,
  Unity Compute Light Baker (irrelevant but symptomatic: modern stack).
- **Linux**: editor officially supported on Ubuntu 22.04/24.04 (x64, GNOME on X11/Wayland,
  NVIDIA proprietary / AMD Mesa). Unity CLI runs headless on Linux → CI story is real.
  Other distros work but unsupported.

## 2. Official Unity Behavior package (`com.unity.behavior`)

Current version 1.0.17 (Nov 2 2026). Free, requires Unity 6+.

- **Editor stack**: Graph Toolkit (GTF successor) + App UI (`com.unity.dt.app-ui`). This
  is the reference implementation for "a BT editor on Unity's modern graph stack" — its
  source is readable under Unity Companion License (check redistribution terms before
  borrowing more than patterns).
- Semantics: *behavior graphs*, not trees — branches may merge; hybrid BT + FSM framing.
  Reads top-to-bottom with plain-language nodes. Blackboard assets, subgraphs, runtime
  debugging in Play mode, breakpoints without IDE (1.0.15, Feb 2026), observer aborts
  (1.0.15: `ConditionalGuardModifier`, `Repeat While`, `Observer Abort` nodes).
- **Critically: in 2025 Unity cut investment** — development is bug-fix/stability only.
  Treat as: (a) a stable floor users can compare against, (b) a source of patterns,
  (c) not a competitor that will evolve.

## 3. Unity Graph Toolkit — the editor foundation

- Public **experimental** package since Unity 6.2 (announced Q1 2025; evolution of the
  internal Graph Tools Foundation that powers Shader Graph / VFX Graph / Unity Behavior).
- Front-end **authoring framework only**: canvas, nodes, edges, serialization, undo/redo,
  copy/paste, local + asset subgraphs. **No execution backend, editor-only** — exactly
  the right scope for us (runtime is ours).
- Claims "graph front-end in under a day, node UI in minutes" — plausible per the Q1 2025
  announcement; still to be validated on a real editor (prototype P1).
- Risks: experimental status → API churn across 6.6 → 6.7 → 7; sparse public docs.
  Mitigated by prototypes P1 and CI matrix (RISK-01).

## 4. Agent ecosystem (Unity-side) — agent-driven development is first-class

- **Unity CLI 1.0** (GA Nov 12–17 2026, announced at Unite Tokyo): terminal control of
  the editor. `unity command` / `unity eval` drive the Editor directly (faster, cheaper
  in tokens than MCP); `unity mcp` exposes the same capabilities over MCP for agents
  that can't run shell commands. Headless: installs editors, creates projects, runs
  builds and **test suites** → our CI and agent compile/test loop.
- The **in-editor MCP server** (in `com.unity.ai.assistant`) is **deprecated** (Nov 2026)
  in favor of the CLI. Lesson learned by Unity: natural-language MCP loops burned tokens;
  precise CLI commands won. We build the agent loop on CLI from day one.
- **Official Unity skills** for agents: `unity skill install opencode` / `unity setup
  opencode` (CLI 1.0.0-beta.15/16) — Unity-authored skills installed into OpenCode.
  Free, no Unity AI subscription needed for CLI/skills/MCP.

## 5. LLM orchestration research (who does what)

Evidence base: community tests and our synthesis (Friendli.ai Unity case study,
TechStackups WebGL platformer test, nowcoder Unity C# spec test, Unity pro
testimonials on r/Unity3D / r/aigamedev, LinkedIn Unity-dev review).

- **GLM-5.3 (text)** — implementer. Strength: C# to a precise spec ("95% of code and
  wiring" per professional Unity dev). Weaknesses: **no vision** (validated screenshots
  as pixel-color scripts and shipped debug overlays — TechStackups), **API
  hallucination** (invented `AssetLoader.Instance.LoadAsset` in a real project test —
  nowcoder), long-context forgetfulness.
- **GLM-5.3-Flash** — first *natively multimodal* GLM-5 model (image/video input, 1M
  context); Z.ai explicitly trains it for "visual coding loop: observe → implement →
  use → refine" incl. CUA. Cheap (~$0.06/M input).
- **GLM-4.6V / 4.6V-Flash** — dedicated vision models (9B Flash, ~$0.02/M input, open
  weights, native multimodal function calling).
- **Z.ai Vision MCP server** (`@z_ai/mcp-server`, bundled with GLM Coding Plan, Apache-2.0,
  quota-billed): 8 tools — `ui_diff_check` (two-screenshot visual regression!),
  `diagnose_error_screenshot`, `extract_text_from_screenshot`, `ui_to_artifact`,
  `understand_technical_diagram`, `analyze_data_visualization`, `image_analysis`,
  `video_analysis`. Works in OpenCode via `mcp` config (local stdio). Images must be
  passed **by file path**, not pasted (clients transcode pasted images directly).
- **VLM limits that shape the workflow** (apidog GLM-5.3-Flash vision guide): good at
  judgment ("nodes overlap?"), bad at measurement ("12px off"); dense small text
  degrades → crop to region of interest; "confidently wrong numbers" → validate JSON
  output shape. Hence: **deterministic layout assertions for geometry, vision for
  triage, human for the final gate.**

### Division of labor (decision)

| Role | Who |
|---|---|
| Runtime + architecture implementation | GLM-5.3 (or 5.3-Flash driving the CLI) |
| Code review / second opinion | another frontier agent (Codex / Claude) |
| Screenshot triage ("does it look right") | GLM-5.3-Flash / 4.6V-Flash / `ui_diff_check` |
| Geometry correctness | deterministic editor tests (`worldBound` etc.) |
| Compile/test feedback | Unity CLI, after every change |
| Final UX gate, aesthetics, releases | human |

## 6. Prior art & landscape (detailed)

- **NPBehave** (meniku, MIT): event-driven BT for Unity, explicitly UE-inspired node
  types. Code-first, **no editor**, dormant since 2022. → candidate runtime donor (MIT
  allows adoption); audit in R3.
- **Unity Behavior**: see §2.
- **GraphView hobby editors** (JamesLaFritz/GraphViewBehaviorTree,
  thekiwicoder0/UnityBehaviourTreeEditor, H-IAAC/BehaviorTrees, Brainiac…): naive tree
  semantics on the **legacy, frozen GraphView API**. Tutorial-grade; useful as donor
  snippets and demand proof.
- **hansvidaure24/Behavior-Tree-Editor**: browser-based BT editor with **replay-timeline
  debugging** + small Unity C# runtime. The replay UX idea is directly relevant to our
  debugger (Phase 4).
- **Behavior Designer** (Opsive, $95+, plus Pro tiers): the commercial default per
  behaviortrees.com (Oct 2026) — but heavy, specific community criticism:
  - "trouble with serialization, absence of source code, handling of version control"
    (r/Unity3D, NodeCanvas-vs-BD thread);
  - "riddled with bugs… had to read and understand a lot of the source code"
    ("Opsive is a well known scammer" thread);
  - own forum: "Subtrees are unusable due to glitchy Shared Variables behaviour",
    "any shared variable modification snapshots all the shared variables and doesn't
    play well with prefab variants";
  - paid upgrade treadmill (→ Pro 2 → Pro 3), full source extra-charged, Pro 3 drags in
    the Entities package even for non-ECS users.
  → **Anti-benchmark**: the pain list is our feature list (native serialization,
  source-first, no treadmills, prefab-variant-friendly).
- **NodeCanvas** ($132): trees + HFSM + dialogue, live Play-mode editing; respected,
  commercial.
- Outside Unity (semantics references only): UE5 BT (the source of truth), Godot
  LimboAI/Beehave, BehaviorTree.CPP + Groot2 (ROS).

## 7. UE5 BT semantics — scope inventory

**In scope (the product):**
1. Event-driven scheduler (no per-frame full-tree traversal; reactive on blackboard
   changes), per-agent node instancing model.
2. Composites: Selector, Sequence, **Simple Parallel** (with finish/finish-and-branch
   memory modes).
3. **Decorators as guards** on composites/tasks (not leaf nodes): condition
   decorators, cooldown, loop, force-success/failure, time-limit; **observer aborts**:
   None / Self / Lower Priority / Both, with correct abort search and re-entry
   semantics.
4. **Services**: periodic (and event) tick attached to composites.
5. Tasks: instant + latent (async, multi-tick), instanced vs shared, abort/finish
   cleanup paths.
6. **Blackboard**: typed keys (bool/int/float/string/vector/enum/object-with-filters),
   per-asset vs per-instance values, key observers (OnBecomeRelevant /
   OnCeaseToBeRelevant), blackboard-driven eventing.
7. Debugger: live node-state visualization, breakpoints (pause Play mode), execution
   history & replay, search/decorator-abort traces.
8. Integrations: AI Navigation `MoveTo`-style tasks, basic gameplay tasks
   (wait, move, rotate-to-face, play animation hook).

**Out of scope (forever):** EQS, AI Perception, StateTree, UE Blueprint interop,
network-prediction AI.

## 8. Conclusions → architecture directives

1. Runtime first (Phase 1, no editor): the semantics are the product; tests define
   trees in code until the editor exists.
2. Editor = Graph Toolkit; Unity Behavior is the pattern reference; no custom canvas.
3. Serialization = native Unity assets; blackboard = serialized fields + blackboard
   asset; **no shared-variable subsystem** (BD anti-pattern).
4. Agent workflow = Unity CLI compile/test loop after every change + screenshot/vision
   review for UI + human gate. Rules codified in AGENTS.md.
5. UE engine source may be read as behavioral specification; code/naming must be
   clean-room (legal: RISK-02).
6. Performance: zero-alloc tick path, pooled node instances and event queues;
   DOTS/Burst = optional isolated module only if Phase 5 numbers justify.
