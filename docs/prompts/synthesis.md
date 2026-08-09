# Synthesis prompts

## Cross-system architecture review

```text
Using the attached VibeCraft decision briefs, produce a cross-system architecture review.

Identify:
- decisions that conflict;
- decisions that create hidden coupling;
- premature optimizations;
- decisions requiring prototypes;
- constraints on future modding or multiplayer;
- decisions that should be simplified for the first playable survival loop.

Produce:
1. A final architecture recommendation.
2. A dependency graph showing which decisions must precede others.
3. A probability/impact risk register.
4. Prototype milestones.
5. A v1 implementation order.
6. Explicit decisions to defer until after multiplayer survival is playable.

Do not silently resolve contradictions. Flag each one and recommend a specific resolution.
```

## Prototype prioritization

```text
Review these VibeCraft decision briefs and rank the smallest prototypes that would eliminate the most architectural uncertainty.

For each prototype specify:
- hypothesis;
- minimal implementation boundary;
- inputs and test conditions;
- measurable success criteria;
- failure interpretation;
- which decision it can greenlight or reject;
- expected throwaway versus reusable code.

Favor prototypes for networking under loss/latency, chunk streaming and meshing, persistence recovery, lighting cost, resource-pack loading, block-update throughput, and mod isolation.
```

## Final implementation roadmap

```text
Using the greenlit VibeCraft decision briefs, create a dependency-aware implementation roadmap.

Target the first playable loop: create/load a world, join through the server, move, place/break blocks, save safely, reconnect, and support a small set of entities.

Separate:
- engine foundations;
- vertical-slice milestones;
- optional polish;
- deferred long-term systems.

For every milestone include interfaces that must be stable, tests, performance budgets, and a definition of done. Do not schedule advanced rendering, broad mod APIs, or modern gameplay before they are required by the playable loop.
```
