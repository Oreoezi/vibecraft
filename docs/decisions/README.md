# Decision briefs

All 40 planned topics are researched. Each brief remains `Proposed` until the owner greenlights its contract, experiment, deferral, or rejection. Start with [`../READ_ME_FIRST.md`](../READ_ME_FIRST.md), the [`../research/GREENLIGHT-CHECKLIST.md`](../research/GREENLIGHT-CHECKLIST.md), and the acyclic [`../research/DEPENDENCY-MAP.md`](../research/DEPENDENCY-MAP.md); do not implement these files in numeric order or treat every `Requires` edge as a hard prerequisite.

[`FOUNDATION-00`](FOUNDATION-00-spec-risk-audit.md) is the cross-cutting risk audit rather than one of the 40 topic IDs.

## Architecture

- [`ARCH-01`](ARCH-01-authority-and-simulation.md) — authority and simulation boundary
- [`ARCH-02`](ARCH-02-simulation-data-model.md) — simulation data model
- [`ARCH-03`](ARCH-03-godot-client-boundary.md) — Godot/client renderer boundary
- [`ARCH-04`](ARCH-04-singleplayer-server-lifecycle.md) — singleplayer hosting and lifecycle
- [`ARCH-05`](ARCH-05-server-plugin-boundary.md) — server plugin boundary

## Networking

- [`NET-01`](NET-01-network-simulation-model.md) — authoritative network simulation
- [`NET-02`](NET-02-movement-prediction-reconciliation.md) — movement prediction and reconciliation
- [`NET-03`](NET-03-transport-and-reliability.md) — transport and reliability
- [`NET-04`](NET-04-block-interaction-lag-compensation.md) — block interaction and lag compensation
- [`NET-05`](NET-05-interest-management.md) — interest management and prioritization
- [`NET-06`](NET-06-tick-and-simulation-rates.md) — world and packet cadence
- [`NET-07`](NET-07-protocol-versioning.md) — protocol versioning/capabilities
- [`NET-08`](NET-08-server-abuse-and-ddos-boundary.md) — abuse and DDoS boundary
- [`NET-09`](NET-09-client-content-agreement.md) — client content agreement

## World and persistence

- [`WORLD-01`](WORLD-01-chunk-coordinate-and-memory-model.md) — section coordinates and memory model
- [`WORLD-02`](WORLD-02-chunk-job-scheduling.md) — generation/job scheduling
- [`WORLD-03`](WORLD-03-world-storage-layout.md) — storage layout
- [`WORLD-04`](WORLD-04-crash-safe-persistence.md) — crash-safe persistence and backups
- [`WORLD-05`](WORLD-05-chunk-lifecycle.md) — lifecycle, dirty tracking, and budgets
- [`WORLD-06`](WORLD-06-versioned-world-generation.md) — deterministic versioned generation
- [`WORLD-07`](WORLD-07-structure-generation.md) — deterministic editable structures
- [`WORLD-08`](WORLD-08-ticking-and-activation.md) — ticking and activation
- [`WORLD-09`](WORLD-09-world-format-migration.md) — world-format migration

## Rendering and lighting

- [`RENDER-01`](RENDER-01-chunk-meshing.md) — chunk meshing
- [`RENDER-02`](RENDER-02-mesh-job-pipeline.md) — mesh rebuild/upload pipeline
- [`RENDER-03`](RENDER-03-far-terrain-lod.md) — far-terrain LoD
- [`RENDER-04`](RENDER-04-lighting-model.md) — lighting model
- [`RENDER-05`](RENDER-05-lighting-resolution.md) — lighting resolution
- [`RENDER-06`](RENDER-06-material-model.md) — material/batching model
- [`RENDER-07`](RENDER-07-fog-and-atmosphere.md) — fog and atmosphere

## Assets

- [`ASSET-01`](ASSET-01-packaging-and-namespaces.md) — resource packaging and namespaces
- [`ASSET-02`](ASSET-02-manifest-and-overrides.md) — manifests, dependencies, overrides, and locks
- [`ASSET-03`](ASSET-03-model-and-animation-contract.md) — normative models/materials/animation graph contract
- [`ASSET-04`](ASSET-04-animation-runtime.md) — texture animation and runtime separation; GLB clauses superseded by ASSET-03
- [`ASSET-05`](ASSET-05-procedural-assets.md) — procedural authoring/runtime boundary

## Mods and plugins

- [`MOD-01`](MOD-01-client-mod-runtime.md) — client extension runtimes and trust tiers
- [`MOD-02`](MOD-02-capability-security.md) — enforceable capabilities and quotas
- [`MOD-03`](MOD-03-extension-api-stability.md) — extension API lifecycle/stability

## Gameplay systems

- [`GAME-01`](GAME-01-content-registries.md) — block/item/entity registries
- [`GAME-02`](GAME-02-redstone-and-block-updates.md) — redstone and block-update substrate

## Status vocabulary

- `Proposed`: researched, awaiting an owner disposition or experiment.
- `Greenlit`: contract or measured mechanism approved for implementation.
- `Needs experiment`: the candidate cannot be selected from prose.
- `Deferred`: intentionally outside the current milestone.
- `Rejected`: considered and ruled out.

The adversarial reviews sometimes classify the *core contract* differently from a detailed mechanism in the same brief. The integrated requirements/checklist takes precedence until individual statuses are updated after owner review.
