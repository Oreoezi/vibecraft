# NET-05 Interest management and replication prioritization

Status: Proposed

Owner: Networking architecture sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Build server-owned, layered interest management over a three-dimensional section grid, with separate simulation, terrain, entity, event, and private-state scopes feeding a congestion-aware per-client priority scheduler.

One-sentence rationale: A roughly 10,000-block-tall sparse voxel world still needs 3D
relevance, while responsive play under congestion requires near collision changes and
action outcomes to outrank far terrain without letting low-priority state starve
forever.

### Owner review status — 2026-08-13

This recommendation is **not owner-greenlit yet**. The owner requested a more concrete
discussion before choosing it. In player-facing terms, the proposal currently means:

- walking moves a server-owned 3D bubble of full block data; terrain ahead is queued
  before terrain behind, but the client cannot point the bubble somewhere else;
- simulation distance, full-detail terrain distance, entity distance, event distance,
  and far-terrain distance are separate knobs, so a distant hill does not activate its
  mobs or reveal its interiors;
- a teleport discards the old streaming epoch, loads only the collision-safe landing
  area first, then expands outward;
- when bandwidth or server load is tight, cosmetic/far terrain degrades first while
  corrections, actions, nearby collision, inventory, and player state retain priority;
- the server clamps advertised/requested distances and may reduce them under pressure.

Before approval, discuss the desired default distances, how visibly servers may clamp
them, how much pop-in is acceptable behind fog, spectator/map exceptions, and whether
the five-scope split is understandable in operator UI. The prototype can test the
mechanism meanwhile, but its current ellipsoid/radii are not product promises.

Godot scene visibility may consume the resulting client state, but it is not the authority or server interest model. Far LoD is a separate coarse representation; it must not subscribe a client to full blocks, entities, or simulation at LoD distance.

## Context and constraints

- `WORLD-01` now uses sparse 3D sections and an initial build range approximately
  10,000 blocks tall. Streaming a whole vertical column would still be hundreds of
  sections, so horizontal and vertical full-detail interest need explicit caps.
- Chunk generation/loading, mesh preparation, entity replication, block deltas, sounds, particles, inventory, and action acknowledgements compete for CPU and bandwidth.
- Client prediction needs collision-critical nearby changes promptly. A far chunk transfer must never head-of-line block a correction or input acknowledgement.
- A UDP-based transport requires real congestion control. The selected GNS foundation
  supplies transport behavior, while interest management still decides which game
  state is worth producing/sending under that bounded capacity.
- Client-provided view distance, camera, throughput feedback, and subscription requests are untrusted hints. The server derives the interest center from authoritative state and applies limits.
- Full-resolution 3D visibility grows cubically. A spherical radius of 12 sections contains roughly 7,238 grid cells before culling, versus roughly 452 positions in a 2D disk. “No max height + very far full-detail render + high tick rate” is internally incompatible without vertical limits and LoD.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| A. Broadcast all relevant world/entity changes to every client | Minimal logic; easy early prototype | Bandwidth and information leak scale with world/player count; no far-world feasibility | Rejected except tiny test maps |
| B. Minecraft-style horizontal columns and one view distance | Familiar and simple; good for short bounded-height worlds | A full approximately 10,000-block column is still too large; conflates terrain, entities, and simulation; cannot prioritize domains well | Poor fit |
| C. One 3D radius controls loading, ticking, terrain, entities, and events | Supports tall sparse worlds with one rule | Rendering choices dictate simulation cost; private/global state awkward; churn and over-replication | Better than B, still too coupled |
| D. Layered 3D scopes plus budgeted priority/aging | Explicit cost controls; supports LoD and differing system needs; graceful congestion behavior | More state machines, acknowledgements, and observability required | **Recommended** |

## Evidence

Labels: **Fact** is directly supported; **Inference** is a conclusion; **Recommendation** is VibeCraft policy.

### Minecraft

- **Fact (Java snapshot 21w38a / 1.18 development):** Mojang split simulation distance from render distance so entities could stop updating closer to players while clients render farther, explicitly to reduce CPU load. The initial release note says block/fluid ticking was still future work at that snapshot. See Mojang's [21w38a technical notes](https://feedback.minecraft.net/hc/en-us/articles/4409891990285-Minecraft-Java-Edition-Snapshot-21w38a#simulation-distance-setting).
- **Fact (Java 1.21 mapped implementation):** `ServerChunkLoadingManager` separately tracks watched chunks, chunk ticking, and entity trackers; `updatePosition` updates both chunk watching and entity tracking. Its `canTickChunk` documentation says it controls spawning/random ticks and includes an additional 128-block/player condition. See [`ServerChunkLoadingManager`](https://maven.fabricmc.net/docs/yarn-1.21%2Bbuild.7/net/minecraft/server/world/ServerChunkLoadingManager.html).
- **Fact:** Java's mapped `ChunkFilter` offers a `cylindrical(ChunkPos center, int viewDistance)` filter and computes changed chunks between filters. See [`ChunkFilter` uses](https://maven.fabricmc.net/docs/yarn-1.21%2Bbuild.9/net/minecraft/server/network/class-use/ChunkFilter.html).
- **Fact (Java 1.20.2+):** chunk delivery has explicit batches, client acknowledgement, a desired batch size, and a cap on unacknowledged batches. See mapped [`ChunkDataSender`](https://maven.fabricmc.net/docs/yarn-1.21.4%2Bbuild.1/net/minecraft/server/network/ChunkDataSender.html). The packet semantics are also recorded by the community-maintained [wiki.vg protocol](https://wikivg.booky.dev/Protocol#Chunk_Batch_Received).
- **Inference:** the simulation/render split and chunk backpressure are worth copying. The cylindrical 2D column assumption is not, because VibeCraft's vertical domain is not bounded in the same way.

### Luanti (formerly Minetest)

- **Fact:** Luanti uses 16³ mapblocks and separately configures active-object send range (default 8 mapblocks), active simulation volume (default radius 4), and block send distance (default 12). Its documentation explicitly calls the active range a **volume**. See the current [`minetest.conf.example`](https://raw.githubusercontent.com/luanti-org/luanti/master/minetest.conf.example#L3210-L3226).
- **Fact:** Luanti caps simultaneous block sends per client (default 40), limits per-player generation/load queues, and can use server-side mapblock occlusion culling. See [block-send limits](https://raw.githubusercontent.com/luanti-org/luanti/master/minetest.conf.example#L3135-L3157) and [server/mapgen limits](https://raw.githubusercontent.com/luanti-org/luanti/master/minetest.conf.example#L3273-L3328).
- **Fact:** its documented server-side occlusion mode claims a 50–80% block-transfer reduction but warns that clients no longer receive invisible blocks; an aggressive optimization warns of visible missing-block glitches. See the same [server performance settings](https://raw.githubusercontent.com/luanti-org/luanti/master/minetest.conf.example#L3273-L3294).
- **Inference:** 3D mapblock interest is proven practical in a voxel clone, while the cave/render warnings show that visibility culling must not be reused for collision or authoritative gameplay relevance.

### Veloren

- **Fact:** Veloren has separate terrain and entity view distances. The server clamps client requests to server settings and transitions each target over time instead of instantly accepting the request. See its [`SetViewDistance` handler](https://veloren.gitlab.io/veloren/src/veloren_server/sys/msg/in_game.rs.html#99-113) and periodic target update at [lines 546–550](https://veloren.gitlab.io/veloren/src/veloren_server/sys/msg/in_game.rs.html#546-550).
- **Fact:** Veloren 0.14's changelog says entity view distance controls both synchronization and display, is clamped to overall view distance, and previously had a bug where changes took effect only after crossing a chunk boundary. See the [0.14 changelog entry](https://gitlab.com/veloren/veloren/-/blob/master/CHANGELOG.md#0140-2023-01-07).
- **Inference:** independent entity/terrain limits and gradual changes are useful; the historical boundary-update bug is a warning to make subscription transitions explicit and test stationary view-distance changes.

### Replication engines and transport guidance

- **Fact (Unity Netcode for Entities):** relevancy is a per-connection/entity filter and is intended for distance, zones, hidden information, and paused replication. Losing relevancy despawns the client representation, which is semantically different from death. See [Ghost Relevancy](https://docs.unity.cn/Packages/com.unity.netcode%401.0/manual/optimizations.html#relevancy).
- **Fact (Unity):** importance determines which entities fit when bandwidth is insufficient; age increases later-send likelihood, and distance can scale importance. See [Ghost snapshots and importance](https://docs.unity.cn/Packages/com.unity.netcode%400.50/manual/ghost-snapshots.html#importance).
- **Fact (Lightyear):** replication targets, visibility, owner prediction, interpolation targets, and replication groups are separate concepts; groups preserve a coherent tick and parent-before-child relationships. See [replication concepts](https://cbournhonesque.github.io/lightyear/book/concepts/replication/replicate.html) and [replication-group guarantees](https://cbournhonesque.github.io/lightyear/book/concepts/advanced_replication/replication_logic.html).
- **Fact (Godot 4):** `MultiplayerSynchronizer` supports per-peer visibility filters and distinct full/delta intervals. See the official [`MultiplayerSynchronizer` API](https://docs.godotengine.org/en/stable/classes/class_multiplayersynchronizer.html). This is useful client-side precedent, but VibeCraft's separate C# server cannot delegate its world streaming to Godot nodes.
- **Fact (IETF):** UDP has no inherent congestion control, and Internet applications must congestion-control aggregate traffic to avoid collapse and unfairness. See [RFC 8085, UDP Usage Guidelines](https://www.rfc-editor.org/rfc/rfc8085.html#section-3.1).
- **Fact (Glenn Fiedler's implementation guidance):** a priority accumulator raises unsent objects over time, fits the highest accumulated priorities within a byte budget, and can reduce the budget as congestion worsens. See [State Synchronization: Priority Accumulator](https://gafferongames.com/post/state_synchronization/#priority-accumulator).

## Proposed design

### 1. Coordinate model and authoritative center

All full-detail world and spatial-entity interest is indexed by canonical
`SectionKey(DimensionId, SectionCoord(x, y, z))`, where section dimensions come from
WORLD-01. No API accepts a whole vertical column as an atomic subscription.

Each connection has a server-owned `InterestState`:

```text
epoch                    // changes on teleport/dimension transfer
center_section           // derived from authoritative player position
horizontal_full_radius   // client hint clamped by server/mode
vertical_full_radius     // independently clamped; mandatory for the tall sparse world
entity_radius
simulation_radius        // server policy, not a client preference
enter_set / retained_set
acked_section_baselines
known_entities
send_budget / queue_bytes
```

Normal players cannot nominate an arbitrary center. The server may bias terrain prefetch toward authoritative velocity and validated look direction, but the base center remains the player. A server-authorized spectator/camera entity may become the center under separate permissions.

Full-detail candidate sections use an ellipsoid:

```text
(dx² + dz²) / horizontal_radius² + dy² / vertical_radius² <= 1
```

The containing section and its immediate 3×3×3 neighborhood are always included when loaded because they are collision-critical. A section enters at the configured ellipsoid and leaves only after crossing an outer radius one section larger. This hysteresis prevents churn at boundaries. A stationary client changing its requested distance triggers the same diff logic; movement is not required.

### 2. Keep five scopes separate

1. **Simulation scope:** the server computes the union of activation around authoritative players plus explicit server tickets. It controls AI, items, fluids, block/random ticks, and redstone as defined by WORLD-08. It is never expanded merely because a client requests farther rendering.
2. **Terrain scope:** full section baselines and block deltas needed to render and collide. A separate far-LoD product contains immutable/coarse surface or impostor data and no hidden interiors, entities, or block-interaction authority.
3. **Entity scope:** spatially indexed dynamic entities, with type-specific caps. The controlling player's entity and required ownership hierarchy are always known to its connection; other entities use distance, visibility policy, and information-hiding rules.
4. **Event scope:** short-lived sounds, particles, animations, damage cues, and explosions are filtered at emission by position/dimension and policy. Events that establish durable state cannot substitute for a state delta.
5. **Private/global scope:** inventory, action results, permissions, chat/team data, time/weather, and protocol control use identity or global policy, not spatial AOI.

The split prevents the common error of using one “view distance” to decide everything. A furnace can continue by scheduled/offline logic without its chunk being rendered; a distant LoD hill does not imply its mobs are networked; an inventory response is never dropped because the player crossed a section boundary.

### 3. Section baseline and delta lifecycle

The lifecycle per connection and section is:

```text
UNKNOWN -> QUEUED_BASELINE -> BASELINE_IN_FLIGHT -> ACTIVE -> LEAVING -> UNKNOWN
```

- A baseline carries `interest_epoch`, `section_coord`, `section_revision`, and immutable payload identity/hash.
- The client acknowledges the baseline revision after decode/application, not merely packet receipt.
- Block deltas are held/coalesced until their base revision is acknowledged. Each delta names base and resulting revision.
- A revision gap, expired base, or changed content registry triggers a replacement baseline; it does not apply deltas speculatively.
- Leaving interest cancels application work not yet accepted by the transport. An unload names the epoch and section; already accepted reliable fragments may still arrive and are ignored under the stale epoch. Do not assume per-message cancellation from the candidate transport.
- Section generation/loading happens through bounded queues. An untrusted interest hint never causes a synchronous load on the simulation thread.

Entity spawn is dependency-aware. Terrain-dependent entities spawn only after the containing section baseline is active. Attachments, riders, and parent/child entities are one replication group so references never precede dependencies. Losing relevance sends a network despawn reason distinct from death/destruction.

### 4. Priority scheduler and congestion behavior

Every connection has one conservative application admission controller derived from configured caps, transport send-rate estimates, pending bytes, queue age, and measured delivered rate. The transport's congestion controller owns actual sending; it does not supply an exact atomically consumable byte budget. All traffic and producers share the resulting limits, and no “important UDP socket” bypasses congestion control.

Work enters these classes:

| Class | Examples | Queue behavior |
| --- | --- | --- |
| P0 control/ownership | action result, correction, inventory revision, spawn dependency, disconnect | reliable; bounded; never coalesced across semantic outcomes |
| P1 collision/live danger | near block changes, player/projectile snapshot, explosion result | newest state or reliable delta as semantics require; scheduled before bulk |
| P2 nearby dynamics | mobs, items, block entities, nearby sound/event | importance + age; stale snapshots/events coalesced or dropped |
| P3 full terrain | nearby-to-far section baselines, rebase, biome/light payload | bounded bulk fragments; stop production on AOI exit and ignore stale epochs |
| P4 far/cosmetic | LoD, ambience, nonessential particles | aggressively coalesced/dropped under pressure |

Within each class, score combines base semantic priority, distance, view/velocity bias, dependency readiness, and age. Unsent eligible work accumulates age so it eventually outranks repeatedly new work of the same class. P0/P1 are considered first, but may use only the transport budget actually available; P2–P4 may consume all spare budget when higher classes are empty.

Hard queue rules:

- entity transforms and player snapshots are “latest wins”; do not retransmit obsolete states;
- multiple block deltas for an unacknowledged section coalesce by cell into one next revision or trigger a fresh baseline when cheaper;
- expired cosmetic events are dropped instead of delivered late;
- queued application baselines outside retained interest are cancelled before transport acceptance; accepted stale fragments are ignored by epoch;
- each connection has byte/count limits. P4 is discarded first, then P3 is paused/cancelled. P0 overflow indicates a broken or malicious connection and disconnects it rather than growing memory without bound;
- periodically refresh static but interaction-relevant entities/state so a lost unreliable update cannot remain wrong forever.

Near collision data should normally fit before one full section baseline. Splitting large baseline payloads into independently paced fragments is a NET-03 concern, but the application scheduler must be able to interleave higher-priority packets between fragments.

### 5. Client hints, backpressure, and abuse resistance

The client may request horizontal/vertical terrain distance, entity distance, and far-LoD distance. The server clamps each by game mode, server policy, memory pressure, and connection health, then returns effective values.

- Distance increases are ramped by at most one section ring every 250 ms; decreases may apply immediately after the retention hysteresis.
- Ordinary requests are accepted at most once per second. Repeated values are ignored cheaply.
- Client “sections decoded per second” feedback is advisory, clamped, and can only lower/ramp processing relative to server limits; it cannot expand transport budget or simulation scope.
- Authoritative teleport/dimension change increments `interest_epoch`, cancels old bulk work, establishes the safety neighborhood first, then resumes outward streaming.
- Prefetch follows server-clamped authoritative speed. Forged camera spins or movement packets cannot force arbitrary generation.
- Interest calculation operates on already authorized candidate coordinates and bounded queues, preventing a request for radius 65,535 from allocating a proportional temporary set before clamping.

### 6. Initial defaults and configuration boundary

Exact block distance depends on section dimensions and rendering benchmarks, so v1 configuration is expressed in section counts, not hard-coded world meters:

```text
horizontal_full_radius: client request, server cap 12 sections
vertical_full_radius:   client request, server cap 6 sections
entity_horizontal_radius: independent server cap
entity_vertical_radius:   independent server cap
leave hysteresis:       +1 section
spawn/teleport gate:    measured compact collision envelope; full sections stream afterward
view-change cadence:    <= 1 request/s, grow <= 1 ring/250 ms
```

These are prototype defaults, not promises for shipped render distance. Servers may lower caps under load. Far-LoD distance is independently configured and never increases full-detail caps.

## Greenlight criteria

- WORLD-01 confirms a 3D section coordinate and provides dimensions/maximum serializable coordinate range.
- Simulation activation, full terrain, far LoD, entities, events, and private state have separate APIs and metrics.
- The prototype proves baseline/delta/spawn ordering under loss, reordering, movement, stationary distance changes, and teleport epochs.
- NET-03 exposes queue/rate statistics and one congestion-controlled connection; application admission is conservative and priority cannot bypass it.
- The load prototype meets the latency, CPU, bandwidth, and memory thresholds below without starvation.
- Product still needs to review/accept independent vertical full-detail limits for the
  10,000-block-tall sparse world.

## Prototype or benchmark

Required: yes  
Smallest useful experiment: a headless C# interest server over a sparse 3D section grid, with synthetic section payloads, moving entities, block edits, five priority classes, baseline acknowledgements, and a deterministic lossy/bandwidth-limited link. Rendering and real generation are unnecessary.

Scenarios:

- stationary spawn, walking section boundaries, rapid direction changes, elevator/vertical travel, legal teleport, disconnect/reconnect, and stationary view-distance changes;
- 1%, 5%, and burst loss; 100–350 ms RTT; 256/512/2,000 kbit/s per-client limits;
- 16-client acceptance fixture: four dense groups, 512 unique active full-detail sections, 2,000 dynamic entities, 10,000 block/event changes per second, and 256 KiB synthetic uncompressed section payloads before test compression;
- 64-client stress run for scaling data, without making 64 players a v1 product promise;
- malicious maximum/negative/repeated distance hints, forged centers, feedback oscillation, and teleport-like movement packets.

Success metrics:

- zero delta-before-baseline, entity-before-dependency, old-epoch application, or durable state divergence in a 30-minute fault run;
- when the congestion controller has at least one MTU available, P0/P1 p99 application-queue delay is no more than one network snapshot interval; no guarantee is made while the path exposes zero send capacity;
- movement admission waits only for a measured **collision envelope**: the containing section plus the neighbor cells/shapes the controller can reach before the next streaming deadline. Settling time must satisfy `transmitted_bytes * 8 / usable_bitrate` plus measured protocol/decode/application margin; do not require 27 full section baselines in 500 ms at 2 Mbit/s;
- every continuously relevant P2 entity receives a state refresh within two seconds under the 512 kbit/s test, while P3/P4 degrade instead of starving it;
- report p50/p95/p99 interest diffing, scheduling, and serialization cost for the
  declared acceptance load and a separate stress fixture against the 60 TPS world
  budget; set a production percentage only after target hardware/workload are fixed;
- bounded queued application data stays below 8 MiB per client; leaving-interest and teleport tests release/cancel old bulk work within one second;
- application producers remain within configured queue/admission caps and react to transport queue age without unbounded growth; packet captures report actual delivered/wire rates;
- malformed requests never allocate or queue work beyond configured caps and do not increase simulation scope.

## Risks and open questions

- WORLD-01 may choose chunk columns plus vertical sections rather than cubic chunks. The external AOI must still be 3D; only coordinate/storage adapters should change.
- Ellipsoid membership and one-section hysteresis need profiling. A precomputed offset table per allowed radius should avoid scanning a bounding cube per client every tick.
- Section payload size, compression cost, and LoD schema may dominate settling time. This decision sets scheduling semantics, not a codec.
- Occlusion culling can reduce visual data but must not hide collision cells, nearby interactive blocks, sounds, or anti-cheat-relevant state. Luanti's documented cave glitches are the warning case.
- Per-client entity filtering can become O(clients × entities). Spatial buckets should produce candidates, followed by policy filters; do not iterate the entire entity world per client.
- Parties, global bosses, maps, spectator cameras, portals, and cross-dimension sounds need explicit non-spatial tickets/policies later.
- A server cannot simultaneously guarantee a large view, immediate settling, low bandwidth, and zero pop-in. Effective distance/settling telemetry must be visible to operators and clients.

## Dependencies

- Requires: WORLD-01 section/chunk representation; ARCH-01 authority; NET-03 reliable channels/congestion control; NET-06 simulation/network rates; NET-07 versioned baseline/delta schemas.
- Blocks: WORLD-05 load/unload policy; RENDER-03 far LoD; entity replication; sound/particle routing; server capacity planning.

## Rejected or deferred alternatives

- Whole-build-range horizontal column subscriptions are rejected because their cost is
  excessive and poorly prioritized even under the finite initial height policy.
- Client-selected interest center: rejected outside authorized spectator mode because it enables information leaks and generation/IO abuse.
- One radius for render, simulation, entities, and events: rejected because it couples graphics preference to server CPU and bandwidth.
- Pure frustum/occlusion AOI: rejected for gameplay state; players need nearby collision and events behind the camera or walls.
- Reliable FIFO queue for all state: rejected because old transforms and far chunks can head-of-line block current corrections.
- Unlimited queues to “eventually send everything”: rejected because stale work increases latency and enables memory exhaustion.
- Full-resolution terrain at far-LoD distance: rejected; far representation is a separate rendering/storage decision.
- Delegating server AOI to Godot `MultiplayerSynchronizer`: rejected because the authoritative server is separate C# software and voxel streaming needs explicit baselines, revisions, and transport budgets.
