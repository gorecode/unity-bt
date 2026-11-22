# unity-bt — UE5-style Behavior Trees for Unity 6+

> **Status: research & planning.** No runtime code yet. This repository holds the research
> notes, the implementation roadmap, and the de-risking plan. Development itself is
> agent-driven (GLM-5.3 implementer + vision-model reviewer + human gate) — the workflow
> is part of the design and is documented in the [research notes](docs/research.md).

## Mission

An open-source (MIT), production-grade port of **Unreal Engine 5 Behavior Tree semantics**
to Unity 6+:

- **Event-driven runtime** faithful to UE5: tasks, decorators with observer aborts
  (Self / Lower Priority / Both), services, Simple Parallel, blackboard with key
  observers. Zero allocations per tick — an architecture rule, not a later optimization.
- **Modern visual editor** built on Unity's official **Graph Toolkit** — the same stack
  Unity ships for Shader Graph and the official Unity Behavior package.
- **A debugger worthy of UE**: live execution-flow visualization, breakpoints that pause
  Play mode, execution history with replay.
- **Native Unity serialization**: graphs are plain Unity-serialized assets. Git merges and
  prefab variants work out of the box. No custom shared-variable snapshot machinery.

## Why this project exists (landscape as of Nov 2026)

| | UE semantics | Graph Toolkit editor | UE-grade debugger | Open source |
|---|---|---|---|---|
| Unity Behavior (official) | partial (own "behavior graph" hybrid) | ✅ | partial | source-available¹ |
| NPBehave | ✅ | ❌ | ❌ | ✅ MIT, dormant since 2022 |
| GraphView hobby editors | ❌ naive trees | ❌ legacy UI | ❌ | ✅ |
| Behavior Designer (Opsive, $) | partial | own UI | ✅ | ❌² |
| **unity-bt (this project)** | ✅ | ✅ | ✅ | ✅ MIT |

¹ Unity Companion License: read/learn, restricted redistribution.
² Community-reported: custom variable serialization fighting VCS and prefab variants,
glitchy subtrees, full source sold separately, paid major-version treadmill.
See [research notes §7](docs/research.md).

## Non-goals

- **EQS** (Environment Query System) and **AI Perception** ports — permanently out of
  scope. Use existing Unity solutions alongside.
- Cloning UE editor UX pixel-for-pixel.
- Unity versions below 6.0 LTS.

## Documentation

1. [Research notes (Nov 2026 session)](docs/research.md) — platform state, prior art,
   agent workflow evidence, decisions.
2. [Implementation plan](docs/implementation-plan.md) — phases, deliverables, gates.
3. [Risk register & de-risking prototypes](docs/risks-and-prototypes.md) — every risk
   with a concrete, time-boxed prototype and pass/fail criteria.

## Ground rules (short version — details in AGENTS.md)

1. **Zero allocations in the tick path.**
2. **UE engine source is a spec, not a codebase.** Read it to understand semantics;
   never copy names, structure, or code (UE EULA).
3. **Native serialization only.**
4. A change is not done until it **compiles in a real Unity editor and tests pass**
   (Unity CLI, from day one).
5. Editor UI changes require a **screenshot + vision review + human gate**; geometry is
   asserted deterministically (`worldBound`), not by eyes.

## License

MIT © 2026 gorecode
