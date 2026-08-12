# Master research prompt

Copy this prompt into ChatGPT Deep Research or another research-capable model. Replace `[INSERT TOPIC]` with one topic from [`topics.md`](topics.md).

```text
You are researching one architecture decision for VibeCraft, a voxel sandbox game.

Project context:
- Godot client using C# bindings.
- Separate authoritative C# server.
- Singleplayer runs through the same server architecture.
- Initial gameplay targets the scope and simplicity of early Minecraft.
- Long-term goals include custom resource packs, permissioned client-side mods, large worlds, multiplayer, efficient redstone, and extensibility.
- Original networking hypotheses included UDP, Protobuf messages, and configurable
  32/64/128 Hz server ticks. Those tick profiles were rejected. The owner-selected
  baseline is one fixed 60 TPS authoritative world loop, while transport packet and
  snapshot rates remain independently paced and measurable.
- Standalone GameNetworkingSockets is the selected transport foundation. Validate its
  fit behind a narrow adapter; do not restart a custom-UDP design contest or add a
  Steam Datagram Relay dependency.
- Server target: threaded chunk generation/ticking, crash-safe saves, plugins, and client-mod compatibility checks.
- Initial world target: square chunks with an approximately 10,000-block build-height
  range. The exact minimum/maximum split remains a world-format decision.
- Client target: optimized chunk meshes, a minimal fog-obscured far-terrain pass before
  v1, client-side smoothing of block-scale 0–15 gameplay light, advanced materials,
  animated assets, and custom models.

Research topic:
[INSERT TOPIC]

Before recommending anything, read the project's foundation audit and integrated
requirements baseline if they are available. When a topic brief conflicts with the
integrated baseline, identify the conflict explicitly instead of silently reviving
an older hypothesis.

Investigate:
1. How relevant Minecraft versions implemented this over time.
2. How at least three relevant clones, voxel engines, or open-source games implemented it.
3. What worked well and why.
4. What failed, caused performance problems, or created compatibility debt.
5. Relevant source code, specifications, papers, technical talks, benchmarks, postmortems, and issue discussions.
6. Which claims are directly sourced, which are inference, and which remain uncertain.

Compare at least three viable designs for VibeCraft. For each design cover:
- core mechanism;
- runtime and memory cost;
- storage implications;
- multiplayer implications;
- mod/plugin implications;
- debugging complexity;
- migration and compatibility risks;
- failure behavior;
- implementation difficulty in C#/Godot;
- future extensibility.

Recommend one design for VibeCraft. Tie the recommendation to the project constraints; do not choose based only on popularity or novelty.

Produce:
A. A concise decision brief suitable for review.
B. A source-heavy deep-dive appendix.
C. A comparison table.
D. Unresolved questions and assumptions.
E. The smallest prototype or benchmark that validates the riskiest assumption.
F. Proposed interfaces, schemas, or state models where relevant.
G. Explicit greenlight criteria.

Use primary sources wherever possible: official documentation, source code, technical talks, papers, and credible postmortems. Identify exact versions. Do not treat “Minecraft” as one consistent implementation. Include links for important claims and distinguish facts from inference.
```
