# Proposed requirements baseline

Status: Proposed for owner review  
Scope: Product requirements and milestone boundaries, not an implementation plan

This document distills the current VibeCraft vision into a smaller, testable baseline.
It does not edit [`design_doc.md`](../../design_doc.md); that file remains the original
vision and hypothesis sheet. For deriving prototypes and implementation contracts,
this is the current proposed interpretation and takes precedence over conflicting
mechanisms in the source sheet or an individual brief. Explicit owner decisions take
precedence over this proposal. Nothing becomes a shipping promise until the owner
greenlights it and its required experiment passes.

The terms **MUST**, **SHOULD**, and **MAY** are normative:

- **MUST** is required to claim the named milestone.
- **SHOULD** is the expected default; departing from it requires recorded evidence or an owner decision.
- **MAY** is optional and may not block the milestone.

The baseline follows the [spec risk audit](../decisions/FOUNDATION-00-spec-risk-audit.md) and the three integrated reviews: [architecture/networking](REVIEW-architecture-networking.md), [assets/modding/security](REVIEW-assets-modding-security.md), and [product scope/sequencing](REVIEW-product-scope-and-sequencing.md).

## Product identity and compatibility language

- **MUST:** VibeCraft is an original voxel survival sandbox, initially inspired by the scale and legibility of Minecraft's early survival loop, then free to develop its own mechanics and content.
- **MUST:** “Minecraft 1.0-like” means design inspiration, not bug-for-bug behavior, protocol compatibility, save compatibility, seed compatibility, asset ownership, or an obligation to reproduce accidental update order.
- **MUST:** First-party names, art, audio, world generation, and balancing are original or appropriately licensed.
- **SHOULD:** Familiar concepts—blocks, tools, crafting, hostile nights, simple circuits, and distinct dimensions—should lower the learning curve without becoming compatibility contracts.
- **MAY:** Version-specific offline tools may convert supported Minecraft resource-pack inputs into VibeCraft's native resource format. Conversion must report unsupported or changed semantics; foreign formats do not become runtime APIs.

Owners: [GAME-01](../decisions/GAME-01-content-registries.md), [GAME-02](../decisions/GAME-02-redstone-and-block-updates.md), [ASSET-02](../decisions/ASSET-02-manifest-and-overrides.md).

## Milestone 1: first playable

The first playable proves a multiplayer building, streaming, durability, and recovery slice. It is not yet a survival game.

### Player-visible requirements

- **MUST:** A packaged client can start a private local session or connect to a server, enter one original Overworld-like test dimension, and leave cleanly.
- **MUST:** Two clients, or one client plus a human-scale bot, can walk, look, jump, collide, see one another, and observe authoritative movement.
- **MUST:** Players can stream nearby terrain and place and break a small fixed set of blocks through the authoritative action path.
- **MUST:** A player can disconnect, reconnect, and observe the same committed world state.
- **MUST:** Explicit save/quit reports success only after its documented durable barrier has completed.
- **MUST:** A forced server termination followed by restart recovers either the old or new valid state for each declared atomic transaction, never a half-applied or silently regenerated state.
- **MUST:** Terrain is rendered at full block detail within a finite measured radius and ends in ordinary distance/height fog. Far-terrain LoD, volumetric fog, and advanced atmosphere are not required.
- **MUST:** The build uses one external, namespaced first-party visual resource pack rather than embedding required art in the game source.
- **SHOULD:** The first-party visual target may use 64×64 source textures, provided texture resolution is not coupled to world-light resolution or gameplay rules.
- **MUST:** The user can inspect basic health diagnostics for simulation time/debt, network queues, section lifecycle, mesh queues, memory, dirty revisions, and persistence status.

### Authority and time

- **MUST:** The server accepts bounded player intent and alone commits position, velocity, grounded state, block outcomes, inventory-like state, health-like state, and all durable world mutations.
- **MUST:** V1 has one authoritative `WorldTick` at **20 Hz**. Movement, interactions, entities, world mutation, deterministic publication, and future gameplay schedules share this commit timeline.
- **MUST:** Rendering, input-device sampling, packet sending, snapshot sending, and interpolation may run at independent rates, but none may create a second authoritative gameplay clock.
- **MAY:** An exactly nested 40 Hz player-controller experiment may be considered only if a blind 20 Hz movement/correction test fails. Configurable 32/64/128 Hz whole-world profiles are not a v1 requirement.
- **MUST:** The owning client may predict local movement and reversible presentation, then reconcile to authority. Remote entities use authoritative snapshots and interpolation.
- **MUST:** First-playable block actions are idempotent, revision-aware, and validated against current authoritative state. Combat rewind, speculative collision, and per-player phantom support are disabled.
- **MUST:** Cosmetic lighting, animation, particles, and sound may be client-owned; any light or timing value that affects gameplay remains server-owned.

Owners: [ARCH-01](../decisions/ARCH-01-authority-and-simulation.md), [NET-01](../decisions/NET-01-network-simulation-model.md), [NET-02](../decisions/NET-02-movement-prediction-reconciliation.md), [NET-04](../decisions/NET-04-block-interaction-lag-compensation.md), [NET-06](../decisions/NET-06-tick-and-simulation-rates.md), [WORLD-08](../decisions/WORLD-08-ticking-and-activation.md).

### World model and concurrency

- **MUST:** The authoritative world is sparse and three-dimensional. It uses finite signed section coordinates, explicit operational borders/ranges, and finite generation requests; “unlimited height” must never mean literal infinity or an unbounded job.
- **MUST:** Empty or absent vertical space consumes no dense column-height allocation. Full-detail, simulation, entity, and generation interest all have finite horizontal and vertical extents.
- **MUST:** The section edge, indexing order, coordinate division, revision representation, and persistent key encoding are frozen only after negative-coordinate, overflow, memory, edit, save, network, and remesh tests select them. A 16³ section is the leading candidate, not a requirement of this baseline.
- **MUST:** Ordinary blocks are compact data, not one managed or Godot object per block. Stable namespaced identities are distinct from world-local and session/runtime numeric IDs; missing content is distinguishable from air.
- **MUST:** One owner commits live world state in deterministic order. Worker jobs may load, generate, light, mesh, compress, or inspect immutable snapshots, but only bounded, revisioned results may be published at an owner-defined boundary.
- **MUST:** Every asynchronous result has an identity, revision, lifetime/epoch, owner, cancellation/failure behavior, and bounded queue. A stale result can never replace newer state.
- **SHOULD:** Parallel materialization should be used where measurements justify it. Parallel live-region mutation is a post-v1 architecture option, not “threaded chunk ticking” for the first playable.

Owners: [ARCH-02](../decisions/ARCH-02-simulation-data-model.md), [WORLD-01](../decisions/WORLD-01-chunk-coordinate-and-memory-model.md), [WORLD-02](../decisions/WORLD-02-chunk-job-scheduling.md), [WORLD-05](../decisions/WORLD-05-chunk-lifecycle.md), [WORLD-08](../decisions/WORLD-08-ticking-and-activation.md).

### Persistence and compatibility

- **MUST:** “Durable” means committed under a named crash model and storage contract, not merely queued, serialized, or handed to an operating-system API.
- **MUST:** Dirty-during-save state remains dirty after an older revision is acknowledged. Dirty authoritative state may not be evicted before its required revision is durably acknowledged.
- **MUST:** Corrupt, truncated, unsupported-newer, or incomplete data fails closed with a recoverable diagnostic; it must not silently become air, default content, or newly generated terrain.
- **MUST:** Network protocol, gameplay rules, world records, generator profiles, resource packs, data packs, and mod ABIs have separate version domains.
- **MUST:** Persistent record families have explicit versions and checks/integrity metadata. Small migrations may be bounded and lazy; major or bulk migrations require an explicit backup/copy workflow.
- **MUST:** Already generated or edited terrain is authoritative. An engine update may not silently regenerate it, and unavailable historical generator content may not be replaced with plausible but different terrain.
- **SHOULD:** The first storage candidate should be tested with forced termination, disk-full, read-only, corruption, writer-stall, and backup/restore cases before any persistent user world is created. SQLite WAL is a candidate, not a product requirement.
- **MAY:** Alternative storage backends may be selected later behind the logical world-store contract if measured product workloads justify them.

Owners: [WORLD-03](../decisions/WORLD-03-world-storage-layout.md), [WORLD-04](../decisions/WORLD-04-crash-safe-persistence.md), [WORLD-06](../decisions/WORLD-06-versioned-world-generation.md), [WORLD-09](../decisions/WORLD-09-world-format-migration.md).

### Client and rendering

- **MUST:** The authoritative core and dedicated server compile and test without Godot. Godot/C# owns application flow and presentation, not authoritative simulation state.
- **MUST:** No ordinary block is represented as a Godot node. Terrain rendering consumes immutable section snapshots and publishes/removes resources under explicit ownership.
- **MUST:** Worker code does not mutate live Godot scene or renderer objects. Mesh/upload/disposal queues and resident memory remain bounded during movement, editing, unload, reconnect, and render-origin rebasing.
- **MUST:** The first playable supports a small fixed number of batched terrain render classes and deterministic material fallbacks. One resource-pack material per draw surface is not acceptable.
- **MUST:** World/gameplay light is block-scale and bounded; 64×64 texture detail is shaded per fragment. A persistent 64³ light lattice per block is not a requirement and must not be implemented as the v1 world-light model.
- **SHOULD:** Begin with the simplest full-detail hidden-face mesh and ambient/directional presentation lighting that meet the declared fixture. Greedy meshing, shader light pages, PBR maps, and low-level renderer paths are optimizations selected by measurement.
- **MAY:** Emissive, reflective, refractive, translucent, animated, and higher-quality material tiers may be added incrementally with explicit batching, sorting, fallback, memory, and frame-time criteria.

Owners: [ARCH-03](../decisions/ARCH-03-godot-client-boundary.md), [RENDER-01](../decisions/RENDER-01-chunk-meshing.md), [RENDER-02](../decisions/RENDER-02-mesh-job-pipeline.md), [RENDER-04](../decisions/RENDER-04-lighting-model.md), [RENDER-05](../decisions/RENDER-05-lighting-resolution.md), [RENDER-06](../decisions/RENDER-06-material-model.md), [RENDER-07](../decisions/RENDER-07-fog-and-atmosphere.md).

### Networking and local hosting

- **MUST:** Gameplay protocol semantics are independent of Godot RPC paths and of the selected transport. Cross-class correctness uses explicit identities, epochs, revisions, sequence rules, and acknowledgements rather than arrival order.
- **MUST:** The protocol distinguishes bounded realtime, control, and bulk traffic semantics. Obsolete realtime state may be superseded; reliable bulk work may not cause unbounded memory or indefinitely starve current control/realtime state.
- **MUST:** Message framing, maximum sizes, malformed-input handling, replay behavior, backpressure, authentication, authorization, and compatibility negotiation are explicit regardless of payload codec.
- **SHOULD:** Protobuf may be used for low-frequency structured messages. Hot snapshots and chunk payloads may use other versioned encodings if measurement shows a material benefit.
- **MUST:** Transport selection remains open until packaged target-platform tests cover native ownership, congestion, lane behavior, connection churn, trust, and admission. GameNetworkingSockets/reliable UDP and QUIC streams plus datagrams are candidates; no candidate's marketing claims are VibeCraft guarantees.
- **MUST:** The same host-agnostic `ServerCore`, authority rules, action handlers, and persistence path serve dedicated multiplayer and singleplayer.
- **MUST:** Supervised child-process loopback and embedded hosting remain candidates until startup, memory, packaging, signing, pause/save, crash isolation, orphan cleanup, and protocol-trace equivalence are compared on supported platforms.
- **MUST:** First-playable public exposure is private/invite-only unless authenticated server identity, authenticated player sessions, pre-auth admission bounds, and operator/upstream responsibilities pass their gates.
- **MAY:** LAN hosting is a later explicit mode. It must not be created by silently rebinding a private singleplayer process.

Owners: [ARCH-04](../decisions/ARCH-04-singleplayer-server-lifecycle.md), [NET-03](../decisions/NET-03-transport-and-reliability.md), [NET-05](../decisions/NET-05-interest-management.md), [NET-07](../decisions/NET-07-protocol-versioning.md), [NET-08](../decisions/NET-08-server-abuse-and-ddos-boundary.md).

### Resource and content packages

- **MUST:** `.vcpak` is a resource-only artifact containing a strict `pack.json` and bounded inert assets under an allowed tree. It contains no native code, Wasm, scripts, Godot scenes/resources, arbitrary shaders, or authoritative gameplay data.
- **MUST:** Resource-pack paths are canonical, namespaced, traversal-safe, case-policy-defined, and read through a VibeCraft-owned read-only boundary. Archive count, path, compressed, decoded, parser-work, and output sizes are bounded before publication.
- **MUST:** The source package is authoritative; platform/GPU/Godot-specific compiled products are disposable private caches and never package identity.
- **MUST:** Logical-content digest, literal artifact digest/length, resolved lock digest, and compiled-cache key are distinct concepts with one normative encoding each.
- **MUST:** The first playable needs only one minimal first-party `.vcpak`. General dependency solving, foreign-package overrides, conversion, GLB rigs, and hot reload do not block it.
- **SHOULD:** Later resolution selects one immutable version per `(artifact kind, package ID)` and produces an exact deterministic lock. Resource overrides are explicit, whole-asset, and resource-to-resource only.
- **MUST:** Visual assets cannot define authoritative collision, reach, movement, light rules, damage, inventory outcomes, or root motion.
- **MAY:** Later asset support can add constrained VibeCraft descriptors, cuboid block models, validated GLB-derived geometry/animation, and build-time procedural outputs. Original untrusted artifacts must not gain broad Godot import or execution authority.

Artifact taxonomy:

| Artifact | Stage | Required trust boundary |
| --- | --- | --- |
| `.vcpak` + `pack.json` | First playable | Resource-only; untrusted parser input but never executable |
| Future data pack | Survival or later | Declarative authoritative content under server validation; separate schema/parser |
| `.vcmod` + `mod.json` | Post-prototype | Standard sandbox component only; no native or precompiled runtime cache |
| Native plugin directory + native manifest | Optional later tier | Fully trusted local/operator code; never server-downloaded as a safe requirement |

Owners: [ASSET-01](../decisions/ASSET-01-packaging-and-namespaces.md), [ASSET-02](../decisions/ASSET-02-manifest-and-overrides.md), [ASSET-03](../decisions/ASSET-03-model-and-animation-contract.md), [ASSET-04](../decisions/ASSET-04-animation-runtime.md), [ASSET-05](../decisions/ASSET-05-procedural-assets.md), [NET-09](../decisions/NET-09-client-content-agreement.md).

## Milestone 2: survival milestone

The survival milestone begins only after the first playable's authority, persistence, streaming, and recovery criteria remain healthy under soak and fault tests.

- **MUST:** Add inventory/hotbar, item drops, deterministic recipes, crafting, one tool/material progression, health, damage, death, respawn, food, and regeneration as server-authoritative systems.
- **MUST:** Persist and replicate those systems without weakening durable transaction, unknown-content, versioning, or bounded-queue requirements.
- **MUST:** Add day/night and at least one simple hostile and one simple passive creature with bounded activation and AI work.
- **MUST:** Ship a coherent original survival loop with explicit completion criteria; “has Minecraft systems” is not itself an acceptance test.
- **SHOULD:** Add furnace-like processing, simple equipment, sound, and presentation animation sufficient to communicate the loop.
- **SHOULD:** Add original biomes, caves, a small structure vocabulary, and weather only after generator identity, deterministic stage inputs, finite work, and crash-safe publication are proven.
- **SHOULD:** Preserve a deterministic, non-recursive block-update substrate so simple circuits can be added without recursive storms or load-order authority.
- **MAY:** A small circuit set inspired by early redstone—dust, input devices, torches, repeaters, doors, lamps, and basic pistons—may enter late in this milestone. It does not promise Java quasi-connectivity, update suppression, duplication, zero-tick behavior, or historical bugs.
- **MAY:** A general resource-pack resolver, Minecraft visual converter, constrained animated assets, and cooperative required-content lock may be introduced once first-party content demonstrates their actual requirements.
- **MAY:** Sandboxed extension dogfooding may begin only after the runtime and capability gates below pass; no public stable ABI is required for the survival milestone.

Owners: [GAME-01](../decisions/GAME-01-content-registries.md), [GAME-02](../decisions/GAME-02-redstone-and-block-updates.md), [WORLD-06](../decisions/WORLD-06-versioned-world-generation.md), [WORLD-07](../decisions/WORLD-07-structure-generation.md), [ARCH-05](../decisions/ARCH-05-server-plugin-boundary.md).

## Modding and security requirements

- **MUST:** Documentation, manifests, installation UI, logs, and server policy distinguish inert resources, declarative gameplay data, sandboxed executable components, and trusted native plugins.
- **MUST:** Native .NET extensions are labeled unrestricted, full-process-trust code. `AssemblyLoadContext`, analyzers, or permission declarations are not a sandbox.
- **MUST:** A claim of “sandboxed” or “scoped permissions” is made only for a runtime that passes hostile validation, compilation, cache, import, capability, quota, handle-lifetime, transaction, disable, and supported-platform tests.
- **SHOULD:** Sandboxed executable mods use deny-by-default capabilities, immutable views, validated commands, host-owned scheduling, bounded output, and no ambient filesystem, network, process, native-library, Godot-object, or engine-object access.
- **MUST:** Sandboxed module storage belongs to an approved artifact/component/update lineage and authenticated server identity where relevant, not merely a reusable package name.
- **MUST:** Client content locks are compatibility and local-integrity checks for cooperating clients. They do not attest possession, execution, absence of extra code, an unmodified client, or honest permission enforcement.
- **MUST:** Server authority remains the anti-illegal-state boundary even when all clients report matching content.
- **SHOULD:** At least two first-party features must survive a real internal refactor through the proposed extension surface before any ABI is called stable.
- **MAY:** Wasm components are the leading sandbox candidate, but the runtime/host binding is not selected until it enforces all required limits on each supported platform. Native plugins remain a separate opt-in tier regardless of that result.

Owners: [MOD-01](../decisions/MOD-01-client-mod-runtime.md), [MOD-02](../decisions/MOD-02-capability-security.md), [MOD-03](../decisions/MOD-03-extension-api-stability.md), [ARCH-05](../decisions/ARCH-05-server-plugin-boundary.md), [NET-09](../decisions/NET-09-client-content-agreement.md).

## Security claim boundaries

- **MUST NOT claim:** “cheating is almost impossible.” Server authority prevents direct acceptance of illegal outcomes; it does not prevent aim assistance, automation, collusion, information extracted from replicated data, malicious native plugins, or every exploit.
- **MUST NOT claim:** “DDoS safe,” “DDoS proof,” or equivalent. The application must bound parsing, allocation, queues, admission work, amplification, and authenticated actions; volumetric availability requires operator/network-provider mitigation.
- **MUST NOT claim:** encryption alone authenticates an arbitrary public server or player. Server identity, player authentication, authorization, channel binding, and replay protection are separate requirements.
- **MUST NOT claim:** a content hash proves what a hostile client has installed or runs.
- **MUST NOT claim:** native .NET permissions contain malicious code.
- **MUST NOT claim:** crash safety survives lying storage hardware, destruction of both primary data and backups, or every form of random corruption.
- **MUST:** Security-controlled lengths, counts, work units, allocations, decompression, queues, handles, and outputs fail within hard bounds. No unauthenticated request may trigger world generation/load, plugin execution, or disproportionate response work.

Owners: [NET-08](../decisions/NET-08-server-abuse-and-ddos-boundary.md), [MOD-02](../decisions/MOD-02-capability-security.md), [WORLD-04](../decisions/WORLD-04-crash-safe-persistence.md), [REVIEW-assets-modding-security](REVIEW-assets-modding-security.md).

## Platform-dependent budgets and release claims

- **MUST:** Before interpreting a performance number as a release requirement, define the supported OS/architecture, pinned Godot version/renderer, named minimum client hardware and resolution/frame target, named server hardware/runtime/GC/storage/uplink, player and bot load, view/simulation radii, movement/teleport speed, network impairment envelope, expected world size, and save/rollback promise.
- **MUST:** Every p95/p99, milliseconds, MiB, player-count, section-count, bitrate, startup, or throughput result names its exact fixture and build configuration.
- **MUST:** All producer, transport, simulation, persistence, parser, cache, and renderer queues have hard memory/work limits and observable age/debt. Overload sheds optional work before corrupting state or growing without bound.
- **SHOULD:** Worker counts, queue thresholds, view distance, cache grace, autosave cadence, compression, mesher, renderer backend, texture-bank size, fog tuning, transport lane mapping, host topology, and storage backend remain private/reversible configuration until measurements justify a public compatibility promise.
- **MAY:** Initial research fixtures may use the defaults proposed by the [product sequencing review](REVIEW-product-scope-and-sequencing.md#compact-owner-decision-card), but those numbers are not shipping promises until the owner names the actual product envelope.

## Post-v1 aspirations

These preserve the original ambition but are not prerequisites for the first playable or core survival milestone:

- **MAY:** Nether-like and End-like original dimensions, portals, bosses, broad biome/structure/weather sets, and a larger mob/content roster.
- **MAY:** Far-terrain 3D voxel LoD, larger horizons, shader-sampled light pages, colored light, GI, local volumetrics, advanced reflection/refraction/transparency, and higher material tiers.
- **MAY:** Rich animation graphs, larger rigged crowds, texture animation, deterministic build-time procedural assets, and bounded built-in runtime shader effects.
- **MAY:** A public sandboxed client/server mod ecosystem, stable versioned capability ABI, richer brokered capabilities, package repositories/signing, and carefully separated trusted-native distribution.
- **MAY:** Explicit generator upgrades with retained profiles and seam adapters, broader deterministic structures, and migration/repair tooling.
- **MAY:** Parallel region-owned simulation, additional storage backends, relays/account services/server discovery, wider platform support, and measured higher-rate nested subsystems.

Owners: [RENDER-03](../decisions/RENDER-03-far-terrain-lod.md), [ASSET-05](../decisions/ASSET-05-procedural-assets.md), [WORLD-06](../decisions/WORLD-06-versioned-world-generation.md), [WORLD-07](../decisions/WORLD-07-structure-generation.md), [MOD-03](../decisions/MOD-03-extension-api-stability.md).

## Explicit non-goals

Unless promoted by a later approved milestone, VibeCraft does not require:

- literal infinite coordinates, height, generation, simulation, or render distance;
- Minecraft protocol/save/seed parity, historical bug compatibility, or runtime loading of Minecraft formats;
- configurable 32/64/128 Hz whole-world simulation in v1;
- client-authored gameplay outcomes, global deterministic lockstep, or whole-world rollback;
- far LoD, volumetric fog, physically exact transparency/refraction, ray tracing, or arbitrary resource-pack shaders for v1;
- public anonymous hosting, universal NAT traversal, remote attestation, or a DDoS-proof claim;
- executable mods/plugins, public ABI stability, arbitrary network/filesystem capabilities, or a marketplace before their explicit gates;
- automatic server distribution or execution of native client code;
- parallel live-world mutation, every-database scalability, or bug-compatible redstone for the survival milestone.

## Milestone acceptance rule

A milestone is complete only when every **MUST** in its scope passes reproducible packaged-build tests on the declared product fixture, every candidate mechanism has a recorded `greenlight`, `revise`, `defer`, or `reject` result, and no deferred aspiration has become an undeclared dependency. Correctness and security gates cannot pass by raising limits, disabling validation/authentication, dropping authoritative work, or weakening durability semantics.
