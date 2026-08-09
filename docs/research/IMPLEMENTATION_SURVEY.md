# Implementation survey: voxel games and Minecraft-adjacent engines

Status: Working research note  
Purpose: Extract architectural lessons, not rank projects or copy their designs.

## How to read this document

Each project optimized for a different product. A small creative sandbox, a Minecraft-compatible server, a moddable engine, and an action RPG do not have interchangeable requirements. “Worked” below means the design supports the project's demonstrated goals; “cost” means a tradeoff visible in its documentation, source structure, or maintenance history—not a claim that the project failed.

## Comparison snapshot

| Project | Most useful lesson for VibeCraft | Main caution |
| --- | --- | --- |
| Minecraft Java ecosystem | Version every durable/networked representation and separate sparse sections from region containers | Long-lived behavior and plugin compatibility make concurrency and format changes expensive |
| Paper/Folia | Parallel world simulation requires explicit spatial ownership, not merely worker threads | Cross-region actions, scheduling, plugins, time, and teleports become architectural concerns |
| Luanti | Cubic map blocks, database abstraction, namespaced content, and script APIs are proven voxel-engine patterns | Sandboxing and client-mod restrictions remain security-sensitive after years of development |
| Veloren | Shared headless libraries, a real local server, transport abstraction, and WASM plugins form a coherent separation | Several facilities are experimental; architecture alone does not prove production fitness for VibeCraft |
| Terasology | ECS plus module/event design enables broad content extension | Generic extensibility and entity/event layers can create memory and complexity costs |
| Craft | A deliberately small clone exposes the minimum viable chunk/render/cache loop | Its trust, protocol, persistence, collision, and fixed-height shortcuts do not scale to the stated VibeCraft goals |
| godot_voxel | Paging, meshing, LOD, collision, and worker orchestration benefit from a specialized subsystem | The mature implementation is overwhelmingly C++, warning against a Node-per-block or interop-heavy C# design |
| PrismarineJS | Protocol compatibility benefits from declarative schemas and broad automated tests | Supporting many historical versions creates a large permanent compatibility surface |

## Minecraft Java, Paper, and Folia

### What appears durable

- Minecraft's region layout groups chunks into region files; the Anvil transition changed chunk contents while retaining the region container concept. Vertical sections and omission of empty sections made increased height less wasteful ([Anvil format](https://minecraft.wiki/w/Anvil_file_format), [region format](https://minecraft.wiki/w/Region_file_format)). The lesson is to keep spatial grouping separate from the versioned section payload.
- Its ecosystem demonstrates the value of stable namespaced identifiers and explicit data/pack versions, but also the ongoing cost of translating between versions.
- Minecraft-style fixed authoritative ticks make behavior reproducible enough for complex block mechanics. The relevant lesson is deterministic ordering and scheduled work—not that VibeCraft should reproduce every ordering quirk.

### Where the accumulated cost is visible

Folia parallelizes loaded worlds by dynamically splitting them into independently ticking regions. Its own overview requires non-adjacent ticking regions, region-local state, merge/split callbacks, separate global state, region/entity schedulers, and special teleport handling ([Folia architecture overview](https://docs.papermc.io/folia/reference/overview/)). This is strong evidence against the idea that “threaded chunk ticking” is a local optimization. Once two regions can tick concurrently, ownership and cross-region messages become public plugin/API semantics.

Folia also illustrates compatibility debt: plugins must use the correct global, region, async, and entity schedulers to work under both Paper and Folia ([Paper/Folia plugin guidance](https://docs.papermc.io/paper/dev/folia-support/)). VibeCraft should establish an ownership-aware API before plugins exist, but should not attempt dynamic region splitting in v1.

### VibeCraft takeaway

Use one deterministic authoritative simulation loop initially. Parallelize pure jobs—generation, compression, meshing, I/O preparation—around immutable/versioned chunk data. If simulation parallelism becomes necessary, introduce coarse stable simulation regions with exclusive ownership and message-based cross-region operations before exposing a broad plugin API.

## Luanti (formerly Minetest)

### What appears durable

- Luanti represents the map as 16×16×16 `MapBlock`s ([engine data structures](https://docs.luanti.org/for-engine-devs/basic-data-structures/)). Cubic units naturally support vertical paging and bound the unit of generation, transfer, light, and persistence.
- It separates map, player, authentication, and mod storage, and supports selectable database backends for different deployment needs ([database backends](https://docs.luanti.org/for-server-hosts/database-backends/)). This is useful evidence for separating persistence domains even if VibeCraft ships one backend.
- Its content APIs enforce names such as `modname:item`, preventing registry collisions and keeping persistent identifiers readable ([client Lua API](https://github.com/luanti-org/luanti/blob/master/doc/client_lua_api.md)).
- The project is explicitly a game-creation platform rather than a single hard-coded game ([Luanti repository](https://github.com/luanti-org/luanti)). Its longevity makes it a better modding comparison than a visually similar but non-extensible clone.

### Costs and warnings

Client mods run in a shared scripting environment and are subject to server restriction flags; the documentation states that server-to-client transfer remains unimplemented and that client mods cannot independently provide all media ([client Lua API](https://github.com/luanti-org/luanti/blob/master/doc/client_lua_api.md)). This shows how mod distribution, capabilities, assets, and protocol negotiation become intertwined.

Luanti's published security history includes sandbox escapes and insecure-environment/API access-control bypasses ([security advisories](https://github.com/luanti-org/luanti/security)). This is not a criticism unique to Luanti; it is evidence that sandboxing a flexible mod API is a sustained security program, not a one-time permission-manifest feature.

### VibeCraft takeaway

Adopt cubic sections, namespaced stable IDs, separate persistence domains, and a narrow capability API. Do not equate a script language with a secure sandbox. Ship server-side content scripting first; keep untrusted client scripting behind an explicit threat model and escape-test suite.

## Veloren

### What appears coherent

- Veloren splits the game into a client and standalone server; singleplayer starts a server on a random local port and connects automatically ([server hosting manual](https://veloren.gitlab.io/book/players/server-hosting/introduction.html)). This closely matches VibeCraft's intended product behavior.
- Its project structure uses headless core libraries, separate frontends, a shared client/server `common` layer, and a dedicated network crate ([architecture manual](https://veloren.gitlab.io/book/contributors/developers/codebase-structure.html)). This is a strong model for keeping Godot out of the authoritative simulation.
- Its network protocol is separated from I/O through drain/sink traits and supports MPSC, TCP, and QUIC implementations, allowing in-process tests and transport changes without rewriting message semantics ([network protocol API](https://veloren.gitlab.io/veloren/veloren_network_protocol/index.html)).
- Veloren uses ECS for batchable entity data and dynamic composition ([ECS manual](https://book.veloren.net/contributors/developers/ecs.html)).
- Plugins target WebAssembly and are described as sandboxed and shareable with clients, with the plugin API/runtime split from engine implementation ([architecture manual](https://veloren.gitlab.io/book/contributors/developers/codebase-structure.html)).

### Costs and warnings

Veloren's manual labels its plugin API highly experimental. Its choices are evidence that the boundaries are feasible, not evidence that VibeCraft can copy the API or security claims without its own audit. An ECS also benefits frequently processed entities more than dense voxel terrain; blocks should not automatically become entities.

### VibeCraft takeaway

Put simulation and protocol contracts in Godot-independent .NET libraries. Make local loopback one implementation of a transport interface. Use component-style data for dynamic entities and separate sparse section storage for blocks. Evaluate WASM as the untrusted extension boundary, but keep a smaller data-driven tier for most content.

## Terasology

### What appears durable

Terasology's entity system separates data components from systems and events, allowing modules to replace behavior or intercept events ([entity-system documentation](https://metaterasology.github.io/docs/concepts/entitySystem.html)). Its blocks remain compact data; only blocks needing extra state, such as chests, receive backing entities. This is exactly the distinction VibeCraft needs between ordinary blocks and block entities.

Its world is chunked, coordinate-limited rather than literally infinite, and its documentation explicitly notes that compact block attributes benefit disk and network serialization ([block-world documentation](https://metaterasology.github.io/docs/concepts/blockWorld.html)).

### Costs and warnings

A generic module/event platform can turn every behavior into indirection. Terasology issue discussions also show pressure from direct/off-heap memory and short-lived allocation patterns around chunks and meshes ([direct-memory discussion](https://github.com/MovingBlocks/Terasology/issues/4948)). VibeCraft should earn an ECS through measured entity workloads rather than making it an ideological prerequisite.

### VibeCraft takeaway

Use compact block states plus optional block-entity records. Prefer typed commands/events at extension boundaries, but keep direct internal calls where replacement/interception is unnecessary. Measure allocation rates and pooled-buffer retention from the first streaming prototype.

## Craft

### What it gets right for a small clone

Craft is valuable because its README documents the entire simplified architecture: 32×32 horizontal chunks, exposed-face rendering, neighbor overlap for boundary visibility, whole-chunk buffer regeneration after edits, frustum culling, deterministic terrain, SQLite persistence of player modifications, and interpolation of remote player positions ([Craft repository and implementation notes](https://github.com/fogleman/Craft)). These are sensible ways to reach a playable result quickly.

Whole-chunk remeshing is especially important: it is often cheaper and much simpler than surgically editing GPU geometry. VibeCraft should benchmark this baseline before implementing elaborate incremental meshes.

### Where its shortcuts stop fitting

- The server uses an ASCII line protocol and clients send their own positions; this is unsuitable for authoritative competitive movement.
- Multiplayer positions are sent at most every 100 ms, demonstrating that a basic sandbox need not simulate or transmit everything at high frequency.
- Only world modifications are saved over deterministic base generation. This is elegant for a static generator but insufficient once generator versions, populated structures, random ticks, and entities change.
- Chunks are horizontal columns with a fixed `0 <= y < 256`; collision and ray tests are intentionally simple.
- Background SQLite writes are committed on a five-second timer, so the design does not provide VibeCraft's desired acknowledged durability semantics.

### VibeCraft takeaway

Build the first renderer from exposed-face culling plus asynchronous whole-section rebuilds. Borrow simplicity, not trust assumptions. Save authoritative generated/populated state or pin every section to a generator version; do not assume regenerated terrain remains identical forever.

## godot_voxel / Voxel Tools

### What appears durable

Voxel Tools is a specialized C++ Godot module/extension supporting editable paged terrain, blocky meshing, custom generators/streams, collision, baked ambient occlusion, and Transvoxel LOD ([project repository](https://github.com/Zylann/godot_voxel)). It is the closest technology comparison for the selected client engine.

Its architecture validates several boundaries VibeCraft should preserve:

- voxel data and rendering chunks are paged independently;
- generation/storage/meshing are worker-oriented services;
- meshes are published to Godot after CPU preparation;
- collision has a separate cost profile from visible geometry;
- blocky and smooth LOD require different algorithms.

Godot's own documentation states that the whole engine is not thread-safe and warns about synchronization stalls when creating/updating GPU resources ([thread-safe API documentation](https://docs.godotengine.org/en/stable/tutorials/performance/thread_safe_apis.html)). Its C# documentation also calls out native interop and marshalling costs for Godot objects and raw arrays ([C# basics](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html)).

### Costs and warnings

Voxel Tools is mostly C++, and its roadmap still lists blocky terrain LOD and multiplayer synchronization as areas of interest. This warns against assuming Godot supplies the required high-level voxel system. A pure C# implementation can work, but hot loops must use flat buffers and avoid per-block Godot objects or repeated managed/native crossings.

### VibeCraft takeaway

Start with a C# voxel core behind clean interfaces, but make the renderer backend replaceable. Establish profiling thresholds that trigger moving selected meshing/compression kernels to GDExtension/native code later. Do not begin by forking or depending on Voxel Tools unless its block-world and licensing/API tradeoffs are explicitly accepted.

## PrismarineJS and protocol compatibility

PrismarineJS's protocol library supports a long list of Minecraft releases and snapshots, including authentication, encryption, compression, handshakes, and packet parsing, backed by broad tests ([node-minecraft-protocol](https://github.com/PrismarineJS/node-minecraft-protocol)). It demonstrates both the power and cost of declarative versioned protocols.

VibeCraft should not promise every old client can connect. Prefer:

- one protocol major per incompatible semantic generation;
- additive capabilities within a supported major;
- an explicit minimum/maximum compatibility window;
- golden packet fixtures and old-client/new-server tests;
- translation at gateways only if demand justifies it.

## Conclusions worth carrying into decisions

1. Use cubic sparse world sections; group them into regions only for I/O locality.
2. Keep authoritative simulation independent of Godot and usable by local or dedicated server frontends.
3. Use explicit ownership and immutable/versioned job inputs before adding parallel simulation.
4. Keep blocks compact; reserve entities/components for dynamic things and stateful block entities.
5. Begin rendering with culled faces and whole-section remeshing; earn incremental geometry and far LOD through profiling.
6. Abstract protocol semantics from transport, but do not invent a transport until requirements and impairment tests exist.
7. Treat native plugins as trusted and sandboxed plugins as a separately constrained product.
8. Define native resource/save/network formats and put Minecraft compatibility in converters or adapters.
9. Design extension points after the core invariants are clear; broad early hooks permanently constrain concurrency and saves.
10. Preserve the ability to simplify. The most useful clones often reached playability through boring, replaceable baselines.
