# VibeCraft research packet — read this first

This repository now contains a complete pre-implementation research pass: 40 topic briefs, one cross-cutting risk audit, implementation comparisons, prototype gates, a proposed requirements baseline, and independent adversarial reviews. The original [`design_doc.md`](../design_doc.md) remains untouched.

Nothing is silently greenlit. Decision files are `Proposed`; measured mechanisms still need a prototype result, and product choices still need owner approval.

## Document authority

For this research packet, use this order when documents disagree:

1. explicit owner decisions recorded against the greenlight checklist;
2. the proposed requirements baseline and integration resolutions;
3. individual decision briefs for details owned by that brief;
4. adversarial reviews as dated evidence about the snapshot they reviewed;
5. `design_doc.md` as the original product vision and hypothesis sheet.

The original sheet is intentionally preserved, not treated as a frozen implementation
contract. Until the owner approves the baseline, this hierarchy selects the coherent
prototype interpretation; it does not claim a product decision has already been made.

## Recommended reading order

1. [`PROPOSED-REQUIREMENTS-BASELINE.md`](research/PROPOSED-REQUIREMENTS-BASELINE.md) — the shortest implementation-independent rewrite of the vision into first-playable, survival, and post-v1 requirements.
2. [`REVIEW-product-scope-and-sequencing.md`](research/REVIEW-product-scope-and-sequencing.md) — what the first playable actually is, G0–G5 gates, lock-in points, and the compact owner decision card.
3. [`GREENLIGHT-CHECKLIST.md`](research/GREENLIGHT-CHECKLIST.md) — the choices to approve, prototype, defer, or reject without replying to 40 files individually.
4. [`DEPENDENCY-MAP.md`](research/DEPENDENCY-MAP.md) — the acyclic G0–G5 path and the distinction between hard prerequisites, interface references, validation dependencies, and post-v1 coordination.
5. [`FOUNDATION-00`](decisions/FOUNDATION-00-spec-risk-audit.md) — the original spec assumptions that were converted into testable requirements.
6. The adversarial reviews: [architecture/networking](research/REVIEW-architecture-networking.md), [assets/modding/security](research/REVIEW-assets-modding-security.md), and [world/render/storage](research/REVIEW-world-render-storage.md), followed by the [independent document-integrity audit](research/REVIEW-document-integrity.md). The first two preserve their pre-integration findings; use their disposition tables with the integration log, not as descriptions of unedited current files.
7. Individual briefs from the [complete decision index](decisions/README.md) when a recommendation or trade-off needs inspection.
8. [`PROTOTYPE_PROGRAM.md`](research/PROTOTYPE_PROGRAM.md) only after reading the product sequence; it maps all long-term experiments and is intentionally broader than the first playable.

## Current integrated baseline

- One Godot-free authoritative `ServerCore`; Godot is a client/presentation adapter.
- One 20 Hz authoritative `WorldTick` for v1. Rendering and packet cadence are independent; only a failed 20 Hz movement test unlocks an exactly nested 40 Hz controller branch.
- The server accepts intent and owns durable outcomes. The owner predicts movement; remote entities interpolate; first-playable block actions use current-time idempotent validation.
- Sparse finite 3D sections with signed coordinates and explicit operational ranges. `16³` is a leading candidate, not frozen until the core-data benchmark.
- One live-world writer. Workers return bounded immutable revisioned proposals; stale results never publish.
- SQLite WAL is the leading persistence candidate, not a foregone conclusion. Crash/disk/corruption/fault tests decide it before user worlds exist.
- GNS is the first transport candidate, not the selected transport. Public direct IP is blocked on authenticated server identity/channel binding, admission, native ownership, lane behavior, and packaging.
- Child-process and embedded singleplayer are host adapters around the same core; the packaged lifecycle comparison selects the default.
- First playable uses finite full-detail terrain, simple bounded meshing/materials, basic lighting, and ordinary fog. Far LoD and advanced visuals are later work.
- `.vcpak` is resource-only. Future data packs, `.vcmod` Wasm components, and trusted native plugins are different artifact classes and parsers.
- Native .NET plugins are unrestricted trusted code. Wasm is only a sandbox candidate after hostile validation, compilation, cache, capability, transaction, and platform tests.
- “DDoS proof,” “cheating almost impossible,” literal infinite height, hash-based attestation, and a persistent 64³-per-block light field are rejected claims.

## First playable

The recommended first playable is deliberately small:

> Two clients enter one server-owned voxel world, move responsively, stream nearby full-detail terrain, place and break a few blocks, explicitly save, survive a forced server restart, reconnect, and observe the same authoritative result.

It excludes survival systems, mobs, redstone, structures, broad worldgen, executable mods, public anonymous hosting, far LoD, volumetrics, advanced materials, and Minecraft conversion. Those remain researched, not implementation dependencies.

## What to decide before implementation

The only immediate owner blocker is the product envelope: target platforms and named machines, frame target, first-playable player/stress load, full-detail radius, network impairment envelope, exposure model, and save rollback promise. Proposed defaults are in the [greenlight checklist](research/GREENLIGHT-CHECKLIST.md).

After that, implementation should begin with ephemeral G1 core-data experiments. No persistent user world or public compatibility promise should exist until its irreversible contracts pass their gates.
