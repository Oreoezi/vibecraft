# Research topics

Use each line as the `Research topic` in [`master.md`](master.md). Keep one topic per decision brief.

## Foundation and architecture

- `ARCH-01` — Define the client/server authority model: server simulation, client prediction, and purely visual state.
- `ARCH-02` — Compare ECS, object-oriented entities, data-oriented simulation, and hybrid models.
- `ARCH-03` — Compare Godot scene nodes, custom rendering systems, MultiMesh-style rendering, and custom C# voxel subsystems.
- `ARCH-04` — Design the singleplayer-through-server lifecycle: startup, shutdown, pause, saving, errors, and local transport.
- `ARCH-05` — Design the dedicated-server plugin and scripting boundary without allowing plugins to corrupt state or block ticks.

## Networking

- `NET-01` — Compare authoritative server, client authority, lockstep, rollback, and hybrid simulation.
- `NET-02` — Design movement prediction, reconciliation, animation-state validation, and anti-cheat boundaries.
- `NET-03` — Compare UDP reliability strategies, including custom channels, ENet, QUIC, and RakNet-like designs.
- `NET-04` — Design lag compensation for movement, block edits, combat, and interactions near changing terrain.
- `NET-05` — Design interest management and prioritization for chunks, entities, updates, sounds, and inventories.
- `NET-06` — Compare one 20 Hz authoritative world clock, independently paced
  input/snapshots, and an optional exactly nested 40 Hz player-controller experiment;
  retain 32/64/128 Hz whole-world profiles only as rejected cost comparisons.
- `NET-07` — Design protocol versioning, capability negotiation, feature flags, and Protobuf evolution.
- `NET-08` — Research DDoS resistance, admission control, rate limiting, validation, and safe server exposure.
- `NET-09` — Design client-mod manifests, hashes, required/optional mods, compatibility, and join failure UX.

## World, chunks, and persistence

- `WORLD-01` — Compare chunk dimensions and section-based storage for worlds without fixed maximum height.
- `WORLD-02` — Design chunk-generation scheduling, worker pools, priorities, cancellation, and dependencies.
- `WORLD-03` — Compare region files, columnar formats, key-value stores, append-only logs, and custom binary storage.
- `WORLD-04` — Design crash-safe saves using journaling, copy-on-write, atomic renames, logs, checksums, and recovery.
- `WORLD-05` — Design loading/unloading, dirty tracking, save prioritization, memory budgets, and shutdown behavior.
- `WORLD-06` — Compare deterministic, versioned world-generation architectures for biomes, caves, and structures.
- `WORLD-07` — Design structure generation and persistence for deterministic yet editable structures.
- `WORLD-08` — Design ticking for items, fluids, block updates, AI, scheduled ticks, redstone, and distant chunks.
- `WORLD-09` — Design world migration and save-format versioning for engine and generator changes.

## Rendering and lighting

- `RENDER-01` — Compare greedy meshing, face culling, binary geometry, clipmaps, mesh shaders, and voxel alternatives.
- `RENDER-02` — Design mesh rebuild scheduling, worker synchronization, and GPU upload flow.
- `RENDER-03` — Compare impostors, hierarchical LoD, simplified meshes, and clipmaps for far chunks.
- `RENDER-04` — Compare flood fill, voxel cone, sparse voxel, screen-space, and hybrid lighting models.
- `RENDER-05` — Evaluate 64 subdivisions per block versus per-voxel, per-face, vertex, texel, or probe lighting.
- `RENDER-06` — Design emissive, reflective, transparent, and refractive materials without destroying batching.
- `RENDER-07` — Design fog and atmospheric rendering for caves, weather, Nether-like spaces, and far terrain.

## Assets and mods

- `ASSET-01` — Compare namespaced files, glTF, custom binary packs, ZIP packages, and hybrid asset-pack formats.
- `ASSET-02` — Design pack manifests, dependencies, overrides, namespaces, versions, validation, and Minecraft conversion.
- `ASSET-03` — Design custom block/entity models, states, materials, collision shapes, and animation references.
- `ASSET-04` — Compare keyframe animation formats and runtime systems for entities and block entities.
- `ASSET-05` — Design procedural assets with deterministic noise, sandboxing, caching, invalidation, and limits.
- `MOD-01` — Compare native C#, WebAssembly, sandboxed scripting, and data-driven client-mod architectures.
- `MOD-02` — Design scoped permissions and trust levels for untrusted client mods.
- `MOD-03` — Research stable mod APIs, lifecycle hooks, registries, events, and lessons from Forge, Fabric, Bukkit, and clones.

## Gameplay systems

- `GAME-01` — Design block/item/entity registries and data models for early gameplay plus future extensibility.
- `GAME-02` — Design pre-1.5 redstone and block updates with a path toward modern redstone.
