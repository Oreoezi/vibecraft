# VibeCraft Research Program

This directory is the pre-implementation research kit for VibeCraft. The initial topic decomposition happened to contain 40 briefs; that is coverage, not a quota or a reason to create more documents. The goal is to make irreversible contracts and experiment gates explicit before implementation while keeping future aspirations out of the first playable.

The original product constraints are recorded unchanged in [`../design_doc.md`](../design_doc.md). Start with [`READ_ME_FIRST.md`](READ_ME_FIRST.md) and the [proposed requirements baseline](research/PROPOSED-REQUIREMENTS-BASELINE.md), which convert ambiguous mechanisms in that sheet into staged requirements.

## Current state

All 40 topic briefs plus the foundation audit are present. They remain proposed; the user now greenlights contracts and prototype directions through [`research/GREENLIGHT-CHECKLIST.md`](research/GREENLIGHT-CHECKLIST.md). The prompt material below remains useful for refreshing a decision after evidence, libraries, or product targets change.

## Workflow

1. Pick one topic from [`prompts/topics.md`](prompts/topics.md).
2. Copy [`templates/decision-brief.md`](templates/decision-brief.md) to `decisions/<ID>-<slug>.md`.
3. Run the master prompt in [`prompts/master.md`](prompts/master.md), replacing the topic placeholder.
4. Paste the short answer into the decision brief and keep the detailed research as an appendix or linked source notes.
5. Record the recommendation, dependencies, risks, and prototype requirement.
6. Before greenlighting a claim that depends on mutable source code, pin the inspected
   repository link to a version tag or commit SHA and record the inspected date.
7. Mark the decision `Greenlit`, `Needs experiment`, `Deferred`, or `Rejected` after review.
8. Use [`prompts/synthesis.md`](prompts/synthesis.md) after a group of related decisions is complete.

Do not greenlight a decision merely because it is the most sophisticated option. Greenlight the smallest design that satisfies the current constraints and has a credible migration path.

## Decision IDs

| Area | IDs | Focus |
| --- | --- | --- |
| Architecture | ARCH-01–05 | Authority, simulation, engine boundaries, singleplayer, plugins |
| Networking | NET-01–09 | Prediction, transport, lag, interest, security, compatibility |
| World | WORLD-01–09 | Chunks, generation, persistence, ticking, migration |
| Rendering | RENDER-01–07 | Meshing, streaming, LoD, lighting, materials, atmosphere |
| Assets | ASSET-01–05 | Packs, models, animation, procedural assets |
| Mods | MOD-01–03 | Client extensions, permissions, API stability |
| Gameplay | GAME-01–02 | Registries and redstone/block updates |

## Status vocabulary

- `Proposed`: researched, awaiting a decision.
- `Greenlit`: approved for implementation.
- `Needs experiment`: the decision depends on a measurable prototype.
- `Deferred`: intentionally postponed; do not let it block the current milestone.
- `Rejected`: considered and ruled out for this project.

## Recommended order

Use the G0–G5 order in [`research/DEPENDENCY-MAP.md`](research/DEPENDENCY-MAP.md),
derived from the product sequencing review. In brief: define the product fixture; test
core data/irreversible formats; prove durable headless state and authority/movement;
then test transport and the Godot streaming slice; integrate only after every branch
has a written disposition. Visual polish, executable mods, broad worldgen, and
redstone are not first-playable dependencies.

## Prototype gate

The first-playable experiments should measure, rather than assume:

- 16³ versus 32³ sparse sections, indexing, palettes, and serialized projections;
- persistence durability/recovery under process kill, disk-full, read-only, corruption,
  writer stalls, and restore;
- 20 Hz prediction/reconciliation around authoritative block edits;
- transport behavior under latency, loss, duplication, reordering, churn, malformed
  admission, and bounded congestion;
- selected child-process loopback hosting in packaged builds, with the embedded adapter retained for conformance/fallback tests;
- bounded chunk streaming, meshing, upload/disposal, and renderer ownership;
- strict loading, identity, and reproducibility of one resource-only base pack.

Lighting pages, hot reload, redstone, broad worldgen, and mod isolation retain research
gates but do not enter the first-playable critical path.
