# Prototype program

Status: Proposed synthesis

The decision briefs intentionally ask for many experiments, but they should not become dozens of throwaway projects. Consolidate them into reusable harnesses. This document is the **full uncertainty program**, not the first-playable implementation checklist. The owner-facing G0–G5 sequence and explicit exclusions in [`REVIEW-product-scope-and-sequencing.md`](REVIEW-product-scope-and-sequencing.md) control product order; P5–P7 here are post-slice research unless the owner deliberately expands scope.

## P0 — Core data and determinism lab

Answers: `WORLD-01`, `ARCH-02`, `GAME-01`, part of `WORLD-09`.

Build a normal .NET test/benchmark project with no Godot dependency:

- signed block/section coordinates and negative floor division;
- explicit dimension min/max build Y values spanning the initial 10,000-block policy;
- 16³ and 32³ candidate section containers;
- `Uniform | Paletted | Direct` block states;
- sparse block entities and generational dynamic-entity handles;
- persistent namespaced registry mappings;
- deterministic logical serialization fixtures.

Decision outputs:

- freeze v1 section side before any real world is created;
- choose mutable/persisted palette representations;
- establish stable identifiers versus runtime compact IDs;
- prove stale handles are fail-safe and missing required gameplay content produces an
  exact zero-write world-open refusal.

Minimum pass conditions:

- exhaustive boundary/property tests for negative and large coordinates;
- no per-block objects or steady-state read allocation;
- uniform/generated sections materially smaller than a flat `uint[]` baseline;
- deterministic serialized projections independent of insertion/runtime registry order;
- stale entity handles always rejected.

## P1 — Persistence and recovery lab

Answers: `WORLD-03`, `WORLD-04`, `WORLD-05`, `WORLD-09`.

Build a headless storage program using the candidate SQLite layout, one writer, revisioned immutable save intents, checksummed envelopes, world metadata, and old-schema fixtures. Add a controller process that terminates the writer at every instrumented persistence stage.

Test:

- atomic section plus block-entity/inventory transactions;
- dirty-during-save and stale save completion;
- WAL recovery, checksum failure, disk-full, permission errors, and I/O stalls;
- verified backup/snapshot publication;
- lazy record migration and explicit copy migration;
- future-major read refusal with zero writes;
- missing required mod/content lock refusal plus read-only recovery/export of bounded
  opaque records.

Minimum pass conditions:

- recovery produces the previous or new committed state, never an invented mixture;
- acknowledged durability matches the documented barrier;
- dirty data cannot be evicted before its current revision commits;
- corrupt critical data fails closed with location/reason diagnostics;
- backup restore is exercised automatically, not merely documented.

## P2 — Authoritative multiplayer slice

Answers: `ARCH-01`, `ARCH-04`, `NET-01`, `NET-02`, `NET-03`, `NET-04`, `NET-06`, `NET-07`, `NET-08`.

Build one shared 60 TPS voxel movement kernel, host-agnostic `ServerCore`, Godot
client, observer/bot client, supervised child-loopback host, embedded
conformance/fallback adapter, selected GNS transport adapter, and deterministic network
impairment proxy. Use a tiny editable world; do not add broad content. GNS and the child
host are owner-selected directions whose packaging/trust/lifecycle acceptance remains
part of this prototype.

Required behaviors:

- sequenced/redundant input, server simulation, owner acknowledgements, replay reconciliation;
- remote interpolation and bounded extrapolation;
- visually predicted place/break with current-time authoritative idempotent result and revision recovery; confirmed collision remains the baseline;
- block removed under/near a moving player;
- connection/version/capability handshake;
- reliable and superseding message classes;
- pause, save barrier, graceful shutdown, forced kill, orphan prevention;
- malformed/replayed/flooded packet harness plus one authenticated-session abuse
  harness that attributes and bounds packet/action/chunk/update work.

Network matrix:

- RTT: 0, 50, 100, 150, 250 ms;
- jitter: 0–50 ms;
- random loss: 0%, 1%, 5%, 10%;
- duplication and reordering;
- bandwidth caps plus burst congestion;
- server tick hitch and client frame hitch.

Decision outputs:

- GNS acceptance plus authenticated server/channel-binding and admission facts;
- whether fixed 60 TPS prediction/replay and the declared workload pass with headroom;
- movement correction thresholds/smoothing;
- receive-time/current-state block and combat policy; historical/subtick validation and
  support grace remain disabled later capabilities;
- child-loopback lifecycle acceptance and embedded-adapter protocol/authority conformance;
- exact overload and resync behavior.

Minimum pass conditions:

- immediate local presentation and eventual authoritative convergence;
- no client packet directly assigns durable position, inventory, health, or world state;
- no unbounded queue, history, retransmission, or log growth;
- one authenticated attacker is throttled/disconnected without crashing or materially
  stalling healthy sessions in the declared fixture;
- unsupported/malformed protocol fails before world allocation;
- forced local-server failure never falsely reports a successful save;
- measured p99 tick and bandwidth budgets on declared target hardware/workload.

## P3 — Streaming and rendering slice

Answers: `WORLD-02`, `WORLD-05`, `NET-05`, `ARCH-03`, `RENDER-01`, `RENDER-02`.

Combine the section model with a bounded generation/load scheduler, interest manager, synthetic network payloads, CPU mesher, and Godot renderer adapter.

Scenarios:

- steady movement, sprint/turn, repeated teleport, disconnect, and four players diverging;
- generation/load deduplication and cancellation;
- edit storms at section boundaries and transparent/opaque layer changes;
- stale job completion after newer edit/unload;
- bounded main-thread mesh uploads and RID cleanup;
- render-origin rebase while streaming and editing;
- constrained bandwidth with near/far chunk priority.

Decision outputs:

- worker count and queue limits;
- view-distance/memory defaults;
- section snapshot/copy strategy;
- greedy-mesh rules and model fallback;
- direct `RenderingServer` versus node adapter;
- per-frame upload and eviction budgets.

Minimum pass conditions:

- stale worker results never publish;
- critical near work is not starved by speculative/far work;
- memory remains within a configured hard ceiling under teleport/edit abuse;
- main-thread publication respects a measured frame-time budget;
- all render/storage/lease resources return to baseline after unload/reconnect.

## P4 — Pack compiler and content agreement

Answers: `ASSET-01` through `ASSET-05`, `NET-09`.

Build a **resource-only** `.vcpak` offline/runtime toolchain before executable or data artifact classes:

- canonical manifest and archive validation;
- namespaced paths, explicit low-to-high profile stacks, and deterministic whole-asset override resolution;
- reproducible package/content hashes;
- PNG/audio plus profile-compatible first-party model fixtures; native voxel model/rig source format remains a separate bounded spike;
- block/model/material validation and fallbacks;
- texture animation without remeshing;
- toy Minecraft visual-pack converter with a conversion report;
- optional post-slice authoring CLI producing ordinary packaged textures; clients do not compile graphs to join;
- join mismatch UX for required content locks.

Security corpus:

- traversal, absolute paths, duplicate/case-colliding paths, symlinks if applicable;
- compressed bombs, huge counts/dimensions, malformed PNG and any optional offline-import source, external URIs;
- unsupported extensions, NaN transforms, cyclic hierarchies, duplicate stack entries, and invalid whole-asset replacement;
- reordered archive entries/timestamps and one-byte content changes.

Minimum pass conditions:

- canonical hashes reproducible across supported OSes;
- validation occurs before unsafe extraction/allocation;
- runtime instantiates no arbitrary Godot scene/script from a pack;
- unsupported Minecraft semantics are reported, not silently misconverted;
- gameplay results do not depend on model/clip/generated texture content.

## P5 — Extension sandbox and API dogfood (after the first playable)

Answers: `ARCH-05`, `MOD-01`, `MOD-02`, `MOD-03`.

Implement a minimal first-party data pack and sandbox module through the proposed public API. Include one registered block/item/recipe, command, scheduled action, persisted namespaced value, and client presentation event.

Attack and lifecycle tests:

- denied filesystem/network/process/environment access;
- memory/fuel/time/output exhaustion;
- recursive events and command floods;
- stale handles and wrong-owner mutations;
- disable/unload cancellation;
- host ABI mismatch and schema migration;
- missing extension on world reload;
- trusted-native plugin warning/policy behavior.

Minimum pass conditions:

- sandbox starts with no ambient authority and receives only declared host capabilities;
- all world mutation returns through validated owner-scheduled commands;
- extension failure cannot leave a half-committed tick transaction;
- a missing required extension blocks normal world open with zero writes; explicit
  recovery/export preserves or quarantines its bounded data;
- host storage/entity refactor does not change ABI fixtures.

Do not declare ABI 1.0 until at least two first-party features have been maintained through an internal refactor.

## P6 — World generation and block simulation (after the minimal slice generator)

Answers: `WORLD-06`, `WORLD-07`, `WORLD-08`, `GAME-02`.

After P0/P1, build deterministic generation stages and a bounded scheduled block-update engine. Include structures crossing section boundaries, generator-version transitions, redstone-like signal graphs, unloaded boundaries, and runaway circuits.

Minimum pass conditions:

- repeated generation with identical seed/version/content lock produces identical logical sections;
- generated/populated provenance prevents accidental regeneration;
- structure placement is idempotent across parallel load order and restart;
- block-update ordering is documented and fixture-tested;
- work is budgeted without losing durable scheduled updates;
- a pathological circuit cannot stall unrelated world simulation indefinitely.

## P7 — Minimal far terrain for v1, then advanced visuals

Answers: `RENDER-03` through `RENDER-07`.

After P0–P4 and the G5 vertical slice are healthy, prototype the minimal v1 far-terrain
profile in isolated repeatable scenes using GPU captures and target-hardware matrices.
Compare a shallow sparse 3D mip candidate with one cheaper per-dimension representation.
Use heavy fog, cheap material summaries, strict resource caps, and fog fallback. Compare
screenshots blindly where visual quality is subjective.

This prototype must not delay P0–P4 or G5. A bounded coarse silhouette is required
before v1; a 2,048-block horizon, better transitions/materials, reflection/refraction,
volumetrics, and procedural materials remain later quality features.

## Execution rule

Every prototype ends with one of four written results:

- `greenlight`: evidence satisfies the decision criteria;
- `revise`: change the proposed design and rerun a bounded test;
- `defer`: value does not justify current cost;
- `reject`: evidence contradicts the design.

Prototype code may become production code only after its ownership, error handling, tests, and interfaces are reviewed. “The demo worked once” is not a greenlight.
