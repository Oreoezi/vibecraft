# First-playable and v1 follow-up dependency map

Status: Implementation-useful synthesis; no prototype or milestone is claimed complete  
Scope: G0–G5 first playable plus G6 minimal v1 far terrain

This map converts the research packet into an acyclic implementation order. For this map, the current [`PROPOSED-REQUIREMENTS-BASELINE.md`](PROPOSED-REQUIREMENTS-BASELINE.md), [`REVIEW-product-scope-and-sequencing.md`](REVIEW-product-scope-and-sequencing.md), and [`GREENLIGHT-CHECKLIST.md`](GREENLIGHT-CHECKLIST.md) control over conflicting first-wave brief recommendations. A decision brief supplies evidence and an interface owner; its whole `Requires` list is not an implementation prerequisite.

No experiment in this document has passed merely because it is scheduled. Every candidate still needs a recorded `greenlight`, `revise`, `defer`, or `reject` result.

## Dependency vocabulary

| Relationship | Meaning in this map | Scheduling effect |
| --- | --- | --- |
| **Hard prerequisite** | A prior gate must produce a named contract or proven behavior before downstream implementation can safely depend on it. | Creates an edge in the milestone graph. |
| **Interface owner/reference** | A brief explains a contract, risk, or candidate used while implementing a gate. Only the minimal contract named here is in scope. | No edge between briefs; jointly owned contracts are frozen once inside their gate. |
| **Validation dependency** | A later test consumes an earlier artifact or repeats its acceptance checks under integration. | Requires evidence before the consuming gate exits, but creates no reverse implementation edge. |
| **Later coordination** | Later work must respect a frozen first-playable or v1 seam if it is eventually scheduled. | No unlisted hard edge and no permission to implement a future subsystem now. |

When an existing `Requires` edge is ambiguous, classify it in that order. It is hard only if the downstream gate cannot define or test its minimal contract without the upstream gate's output.

## Acyclic hard-prerequisite graph

```text
G0 Product envelope
 |
 v
G1 Core data and irreversible-format spike
 |\
 | +---------------------> G4B Godot client, renderer, base pack --+
 |                                                                  |
 +----> G2 Durable headless world ----------------------------------+
 |                                                                  |
 +----> G3 Authority and movement --+                                |
                                    v                                |
                         G4A Transport, trust, interest --------------+
                                                                       v
                                                     G5 Integrated first playable
                                                                       |
                                                                       v
                                                     G6 Minimal v1 far terrain
```

The first-playable hard edges are `G0→G1`, `G1→G2`, `G1→G3`, `G1→G4B`,
`G3→G4A`, and `{G2,G3,G4A,G4B}→G5`; the v1 follow-up adds `G5→G6`.
G2, G3, and G4B may proceed in parallel after G1. G4A uses synthetic section
payloads and therefore does not wait for G2 or G4B. G4B consumes G1 snapshots and
does not wait for transport or persistence. Their real interactions are validated at
G5 before far-terrain scope begins.

## Gate contracts and work order

| Gate | Hard input | Minimal contracts jointly frozen at this gate | Candidate work and required disposition | Must not enter this gate |
| --- | --- | --- | --- | --- |
| **G0 — product envelope** | Owner approval of the first-playable slice | One versioned acceptance fixture: supported OS/architecture and pinned Godot build; named client/server machines; frame target; human/bot load; horizontal/vertical full-detail radius; network impairment and usable-bitrate envelope; private/invite exposure; explicit-save barrier and autosave rollback promise; first-playable exclusions. | Accept or amend the defaults in the greenlight checklist. This is an owner/product result, not a benchmark result. | Performance claims without a named fixture; survival breadth; public-anonymous exposure. |
| **G1 — core data** | G0 | Canonical signed 3D `BlockCoord`, `SectionCoord`, and dimension-bearing `SectionKey`; floor division/local-index rules; initial 10,000-block build-range policy; selected section side and indexing order; stable namespaced content identity separated from world/session `uint32` IDs; missing required gameplay content blocks open; checked revision domains; deterministic logical serialization and record-key encoding; one fixed 60 TPS `WorldTick` owner and phase vocabulary. | **E1:** measure 16³ vs 32³ and adaptive vs simpler storage. Record one section representation disposition before user worlds exist. | Persistent user worlds; Godot types in world identity; general generator epochs; structures; plugin data. |
| **G2 — durable headless world** | G1 | Godot-free `ServerCore` ownership seam; one-writer mutation/publication rule; immutable revisioned save intent; atomic record envelope for the slice; dirty/queued/durable revision state machine; durable receipt and crash model; corrupt/newer-data failure; minimal migration/read-refusal behavior; one pinned simple generator identity. | **E2:** test SQLite WAL/FULL first and compare one bounded fallback only if it fails. Fault-inject kill, disk-full, read-only, corrupt, stalled-writer, reopen, and restore paths. | General backup product, alternate production databases, mixed generator epochs, structures, inventories, mods. |
| **G3 — authority and movement** | G1 | Server authority matrix; shared 60 TPS voxel-controller and collision rules version; authoritative `WorldTick` distinct from wrapping client input sequence; bounded redundant input and missing-input rule; one causal action timeline; `ActionId`, expected revision, idempotent receive-time/current-state block/combat result; owner acknowledgement/reconciliation and remote interpolation semantics; bounded history and queues. | **E3:** test fixed 60 TPS prediction, replay, correction, and capacity under G0 impairment using in-memory transport plus deterministic impairment. | Native transport integration; Protobuf field-number freeze; historical/subtick action evaluation; support lease; speculative collision; alternate tick profiles. |
| **G4A — transport, trust, interest** | G3 | Transport-independent application envelope; realtime/control/bulk semantics; explicit ownership and backpressure results; message/count/byte/work bounds; handshake and negotiated protocol major/capabilities used by the slice; authenticated channel/server identity appropriate to G0 exposure; section baseline/epoch/revision and interest semantics that never rely on cross-lane arrival order; one-session abuse attribution. | **E4:** package and fault-test the selected pinned GNS implementation; `greenlight`, `revise`, or reopen an alternative only for a measured showstopper. Measure compressed payloads, queue age, settling, churn, malformed/adversarial admission, single-session abuse, and amplification. NET-05 details still require owner discussion. | Custom UDP fallback; public-anonymous promises; production relays/accounts/discovery; hash-as-attestation; fixed transport lane numbers as gameplay semantics. |
| **G4B — Godot client, renderer, base pack** | G1 | Godot-free core/client boundary; immutable section-render snapshot; mesh product key + revision + lifetime/epoch; bounded build/upload/disposal ownership; render-local checked conversion and floating origin; minimal block-render template/material ID; fixed small terrain surface classes; resource-only `.vcpak` path, identity, explicit low-to-high whole-asset overlay stack, and bounded VFS contract exercised by one first-party pack. | **E6:** hidden-face baseline vs measured greedy optimization; node/`ArrayMesh` vs low-level renderer only if needed. **E7:** validate one minimal base pack, canonical paths, logical digest, artifact digest/length, and reproducible ordered-stack resolution. | Far LoD; light pages; volumetrics/GI/OIT/refraction; custom model source-format spike; user-authored animation graphs; hot reload; procedural runtime assets. |
| **G5 — integrated first playable** | G2 + G3 + G4A + G4B | Packaged lifecycle contract for connect/start, enter, move, stream, edit, save, stop, restart, and reconnect; supervised child-loopback desktop hosting; an embedded conformance/fallback adapter; explicit save/quit status; cross-branch diagnostics and bounded-resource stop rules. No new world, wire, or pack primitive should be invented at integration without sending the affected branch back to its owning gate. | **E5:** validate the selected child-loopback host through startup, memory, crash containment, packaging/signing, pause, save, orphan cleanup, and protocol-trace tests; run the embedded adapter only as a conformance/fallback comparison. Run the declared acceptance and soak/fault matrix. | Survival milestone features; public mods; redstone; advanced rendering; additional dimensions; claims based on an individual branch demo. |
| **G6 — minimal v1 far terrain** | G5 | Derived/cosmetic far-tile identity and revision; bounded authorization/interest/cache/job/upload path; heavy-fog fallback; near terrain and gameplay always win resource contention. | **E9:** compare the shallow 3D-mip candidate with one cheaper per-dimension representation at a modest horizon. Greenlight the cheapest profile meeting silhouette, revision, plateau, and fault criteria. | Delaying G5; 2,048-block shipping promise; universal cave/interior fidelity; far collision/simulation; advanced materials/shadows. |

## Minimal cross-gate interfaces

These are the only planned hand-offs. Concrete internal containers, libraries, queue sizes, codecs, renderer backend, and host topology remain replaceable until their gate records a result.

| Producer | Consumer | Hard hand-off | Validation performed later |
| --- | --- | --- | --- |
| G0 | G1–G6 | Acceptance fixture and exclusions | Every numeric result names this fixture. |
| G1 | G2 | Section/content identity, revision types, deterministic logical projection | G2 round-trip, unknown-content, crash, and migration fixtures. |
| G1 | G3 | Collision-readable immutable world view, `WorldTick`, action/publication vocabulary | G3 deterministic traces and correction reasons. |
| G1 | G4B | Immutable section snapshot, checked render-local coordinates, content/material IDs | G4B stale-product, rebase, unload, memory, and frame tests. |
| G3 | G4A | Message-neutral intents, outcomes, IDs, revisions, acknowledgements, and traffic semantics | G4A impairment, reordering, malformed-input, congestion, and packaged-native tests. |
| G2 + G3 + G4A + G4B | G5 | Durable world, authority protocol, selected transport path, and client/render/base-pack path | End-to-end save/restart/reconnect, trace equivalence, resource plateau, and fault recovery. |
| G5 | G6 | Healthy integrated near-world client/server, LoD-aware render key, fog, revisions, bounded interest/jobs | Far-data failure falls back to fog and never degrades G5 correctness. |

Validation does not create a reverse dependency. For example, G2 later validates G1 serialization under crashes, but G1 does not require a completed storage backend; G5 validates all branches together, but a branch does not depend on G5 to begin.

## Cycle cuts from first-wave `Requires` edges

| Apparent cycle | First-playable treatment |
| --- | --- |
| `ARCH-02 ↔ GAME-01` | Co-freeze the minimal block-state/registry identity projection in G1. Broader item/entity/content registries wait for survival. |
| `WORLD-01 ↔ WORLD-06` | G1 selects coordinates and section representation. G2 stores one pinned simple generator ID/hash. General generator epochs consume those contracts after G5 and are not a prerequisite. |
| `WORLD-04 ↔ WORLD-05` | Co-freeze one dirty/save/durable-receipt state machine in G2; lifecycle and storage are two sides of that interface, not sequential subsystems. |
| `RENDER-01 ↔ RENDER-04` | G4B starts with a minimal mesh vertex/material/light input sufficient for ambient/directional presentation. Advanced propagated lighting is not required. |
| `NET-04 ↔ NET-06` and `NET-04/05/06` | G3 owns fixed 60 TPS world time and receive-time/current-state action causality. G4A interest management consumes revisions and traffic classes. Historical/subtick validation and support grace remain off. |
| `RENDER-03 ↔ RENDER-07` | Remove far implementation from G0–G5; ordinary fog closes the first-playable radius. G6 consumes the already-working fog/renderer to add minimal v1 far silhouettes without a reverse edge. |
| Large architecture/assets/modding component | Keep `.vcpak` resource-only in G4B. Executable extension artifacts, capability APIs, and plugin persistence have no first-playable interface beyond opaque namespaced IDs and separate version domains. |

## Decision briefs are references, not whole-brief blockers

The following briefs may be consulted at each gate. Their detailed mechanisms, benchmarks, and outgoing `Requires` lists do not become prerequisites unless the minimal gate contract above names them.

| Gate/use | Interface owners and references | Extract only |
| --- | --- | --- |
| Cross-cutting risk | [`FOUNDATION-00`](../decisions/FOUNDATION-00-spec-risk-audit.md) | Rejected claims, uncertainty, and experiment obligations. |
| G1 | [`WORLD-01`](../decisions/WORLD-01-chunk-coordinate-and-memory-model.md), [`ARCH-02`](../decisions/ARCH-02-simulation-data-model.md), [`GAME-01`](../decisions/GAME-01-content-registries.md), [`WORLD-08`](../decisions/WORLD-08-ticking-and-activation.md), [`WORLD-09`](../decisions/WORLD-09-world-format-migration.md) | Coordinate/data/ID/revision/time contracts; not survival registries, migrations at scale, or parallel simulation. |
| G2 | [`WORLD-02`](../decisions/WORLD-02-chunk-job-scheduling.md), [`WORLD-03`](../decisions/WORLD-03-world-storage-layout.md), [`WORLD-04`](../decisions/WORLD-04-crash-safe-persistence.md), [`WORLD-05`](../decisions/WORLD-05-chunk-lifecycle.md), [`WORLD-06`](../decisions/WORLD-06-versioned-world-generation.md), [`WORLD-09`](../decisions/WORLD-09-world-format-migration.md) | One writer, bounded revisioned work, v1 envelope/barrier, and one generator identity; not the general worldgen or backend roadmap. |
| G3 | [`ARCH-01`](../decisions/ARCH-01-authority-and-simulation.md), [`NET-01`](../decisions/NET-01-network-simulation-model.md), [`NET-02`](../decisions/NET-02-movement-prediction-reconciliation.md), [`NET-04`](../decisions/NET-04-block-interaction-lag-compensation.md), [`NET-06`](../decisions/NET-06-tick-and-simulation-rates.md), [`NET-07`](../decisions/NET-07-protocol-versioning.md) | Authority, prediction, current-time idempotent actions, one v1 clock, and version-domain separation; not historical tick profiles or optional compensation rules. |
| G4A | [`NET-03`](../decisions/NET-03-transport-and-reliability.md), [`NET-05`](../decisions/NET-05-interest-management.md), [`NET-07`](../decisions/NET-07-protocol-versioning.md), [`NET-08`](../decisions/NET-08-server-abuse-and-ddos-boundary.md), [`NET-09`](../decisions/NET-09-client-content-agreement.md) | Selected GNS transport boundary, proposed bounded interest/admission, handshake/versioning, and cooperating-client content declaration; not attestation or public service infrastructure. |
| G4B | [`ARCH-03`](../decisions/ARCH-03-godot-client-boundary.md), [`RENDER-01`](../decisions/RENDER-01-chunk-meshing.md), [`RENDER-02`](../decisions/RENDER-02-mesh-job-pipeline.md), [`RENDER-04`](../decisions/RENDER-04-lighting-model.md), [`RENDER-05`](../decisions/RENDER-05-lighting-resolution.md), [`RENDER-06`](../decisions/RENDER-06-material-model.md), [`RENDER-07`](../decisions/RENDER-07-fog-and-atmosphere.md), [`ASSET-01`](../decisions/ASSET-01-packaging-and-namespaces.md), [`ASSET-02`](../decisions/ASSET-02-manifest-and-overrides.md) | Basic full-detail render path, bounded jobs, ordinary lighting/fog, fixed material classes, and one minimal resource pack; not each brief's advanced tiers. |
| G5 | [`ARCH-04`](../decisions/ARCH-04-singleplayer-server-lifecycle.md) plus all branch references above | Host-neutral lifecycle, selected child-loopback desktop host, and embedded conformance/fallback adapter. |
| G6 | [`RENDER-03`](../decisions/RENDER-03-far-terrain-lod.md), [`RENDER-07`](../decisions/RENDER-07-fog-and-atmosphere.md), plus G4B/G5 references | Minimal fog-obscured far silhouettes and bounded derived-data behavior; not the extended far-rendering roadmap. |

## Explicit quarantine

The following work has no hard edge into G0–G5. Preserve only the named seam; do not create its production scheduler, schema, public API, cache format, or compatibility promise during the first playable.

| Quarantined work | Earliest coordination point | Seam preserved now |
| --- | --- | --- |
| Inventory, crafting, tools, health, mobs, day/night, and survival content | After G5 remains healthy under soak/fault tests | Server authority, namespaced IDs, durable transactions, one `WorldTick`. |
| Redstone/general block updates and fluids | Survival or later | Deterministic owner commit phase; no recursive live mutation contract. |
| Biomes, caves, structures, generator upgrades, retained epochs, seam adapters | After G2/G5; separate world-identity milestone | `SectionKey`, generator ID/hash, authoritative generated/edited records. |
| Minecraft conversion, native voxel model/rig format spike, user animation graphs, hot reload, procedural authoring | After first-party content demonstrates a requirement | Resource-only `.vcpak`, canonical names, explicit ordered whole-asset overlays, private compiled caches, `RigProfile` compatibility. |
| Wasm components, capability broker, trusted native loader, plugin persistence, public ABI/marketplace | Post-G5 hostile sandbox experiments and two first-party dogfood features | Artifact-kind/trust split, opaque handles, separate version domain; no executable pack embedding. |
| Extended far-terrain horizon/quality, advanced propagated/colored light, GI, volumetrics, OIT, reflection/refraction, advanced materials | After G6/post-v1 | Replaceable render snapshot/material boundary, minimal far profile, and ordinary fog. |
| Historical/subtick action evaluation, support-loss grace, speculative collision, alternate tick rate | Separate later capability after a demonstrated product need | `ActionId`, input sequence, revisions, and version domains; all features remain disabled. |
| Anonymous public hosting, accounts, discovery, relays, universal NAT traversal, volumetric DDoS mitigation | Later exposure milestone | Authenticated channel/server identity and bounded admission for the selected private/invite mode. |
| Parallel live-region simulation and alternate production stores | Post-v1 measurement | One-writer command/store interfaces; no distributed ownership fields now. |
| Additional dimensions, portals, bosses, broad content roster | Post-v1 product milestones | Dimension-bearing keys and original/licensed content policy. |

## Unresolved inputs and stop conditions

- **G0 is not owner-approved yet.** The checklist's Windows/Linux, 1080p/60, capacity, visibility, network, and save numbers are recommended fixture defaults, not shipping requirements.
- **G1 has not selected section side/storage.** `16³` remains the leading candidate and must not be encoded as a compatibility constant before E1 records a result.
- **Shared scalar domains are documented but not implemented or frozen.** G1 must
  enforce the proposed signed-64 world coordinates, dimension-bearing section
  identity, nonnegative signed-64 section revisions, unsigned-64 authoritative
  `WorldTick`, wrapping unsigned-32 client prediction order, and checked render-local
  narrowing in executable property/schema tests before wire or save formats freeze.
- **SQLite, mesher upload/backend, and pack details remain candidates.** GNS and
  child-process desktop hosting are owner-selected directions whose acceptance gates
  remain E4/E5. E2 and E6/E7 must record their mechanisms.
- **The 60 TPS world clock is owner-selected, not capacity-proven.** E3 must meet its
  replay/correction/load gate; failure cannot silently lower or split the authority
  clock.
- **No user world, public wire promise, or public pack/mod ABI may precede its freeze gate.** A failing gate returns `revise`, `defer`, or `reject`; it does not silently widen limits or import deferred systems.

G5 is the first point at which the packet permits the word “playable.” G6 is a v1
release follow-up, not permission to delay that vertical slice. Passing one branch
prototype supplies evidence only.
