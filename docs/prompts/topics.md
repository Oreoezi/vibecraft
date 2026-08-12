# Research topics

Use each line as the `Research topic` in [`master.md`](master.md). Keep one topic per decision brief.

## Foundation and architecture

- `ARCH-01` — Define the client/server authority model: server simulation, client prediction, and purely visual state.
- `ARCH-02` — Compare ECS, object-oriented entities, data-oriented simulation, and hybrid models.
- `ARCH-03` — Compare Godot scene nodes, custom rendering systems, MultiMesh-style rendering, and custom C# voxel subsystems.
- `ARCH-04` — Validate the selected supervised child-server plus loopback lifecycle on Windows/Linux; retain an embedded adapter only for conformance/fallback.
- `ARCH-05` — Design one public sandboxed extension boundary, brokered plugin storage, and tick-safe server scripting; do not create a parallel public native-plugin API.

## Networking

- `NET-01` — Compare authoritative server, client authority, lockstep, rollback, and hybrid simulation.
- `NET-02` — Design movement prediction, reconciliation, animation-state validation, and anti-cheat boundaries.
- `NET-03` — Validate standalone GameNetworkingSockets behind a narrow transport adapter: channels, backpressure, packaged deployment, metrics, and failure handling; exclude custom UDP and Steam Datagram Relay.
- `NET-04` — Specify current-authoritative-time validation for v1 block edits and combat, while reserving a negotiated post-v1 subtick/action-timestamp seam without building rewind history now.
- `NET-05` — Explain and test the player-visible interest rules: which nearby chunks/entities are guaranteed, what may be delayed, how priorities change during fast movement or teleport, and how memory/bandwidth overload degrades without holes or stale state. Keep this owner-reviewable before selecting constants.
- `NET-06` — Validate one fixed 60 TPS authoritative world loop with independently paced input packets, snapshots, chunk work, saves, and slower systems; retain 20/32/40/64/128 Hz only as historical or rejected cost comparisons.
- `NET-07` — Design protocol versioning, capability negotiation, feature flags, Protobuf evolution, and a post-v1 authenticated server-transfer offer that reconnects through the normal handshake.
- `NET-08` — Research DDoS resistance, admission control, rate limiting, validation, and safe server exposure, including the invariant that one authenticated session cannot exhaust the simulation, chunk, plugin, or outbound-work budgets. Treat proof-of-work as an optional post-v1 admission layer.
- `NET-09` — Design client-mod manifests, hashes, required/optional mods, compatibility, and join failure UX.

## World, chunks, and persistence

- `WORLD-01` — Compare chunk dimensions and section-based storage for an initial approximately 10,000-block build range whose exact minimum/maximum split remains configurable in the world descriptor.
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
- `RENDER-02` — Explain mesh rebuild scheduling in player-visible terms, then prototype worker cancellation, stale-result rejection, main-thread GPU upload, and bounded queues. Keep backend/thread-count constants pending owner review and measurement.
- `RENDER-03` — Select the smallest fog-obscured far-terrain silhouette representation that can ship after the first playable but before v1; compare universal 3D mips with cheaper per-dimension representations and defer high-fidelity/extreme-distance LoD.
- `RENDER-04` — Compare flood fill, voxel cone, sparse voxel, screen-space, and hybrid lighting models.
- `RENDER-05` — Implement two block-scale server gameplay-light values (sky and emitted, each 0–15) and compare client interpolation/filtering methods that make them appear smooth without changing authoritative resolution.
- `RENDER-06` — Design emissive, reflective, transparent, and refractive materials without destroying batching.
- `RENDER-07` — Design fog and atmospheric rendering for caves, weather, Nether-like spaces, and far terrain.

## Assets and mods

- `ASSET-01` — Validate engine-agnostic ZIP resource packs and a best-effort Minecraft visual-pack converter; do not make glTF/GLB the pack contract.
- `ASSET-02` — Design a low-to-high selected resource-pack stack where the last whole asset wins; exclude dependency DAGs, deep merge, and per-texture layering.
- `ASSET-03` — Run a bounded spike for VibeCraft voxel model/rig source format; define `RigProfile` compatibility and reusable built-in animations without Minecraft-bone compatibility.
- `ASSET-04` — Design fixed `RigProfile` clip selection and texture animation for v1; defer user-authored custom clips and animation graphs.
- `ASSET-05` — Design a bounded, engine-neutral material authoring graph that compiles to fixed engine templates, never arbitrary custom shaders.
- `MOD-01` — Compare sandbox runtime candidates (Wasm component and constrained Lua family) for one public extension API; private native forks are outside it.
- `MOD-02` — Design scoped permissions and trust levels for untrusted client mods.
- `MOD-03` — Research stable mod APIs, lifecycle hooks, registries, events, and lessons from Forge, Fabric, Bukkit, and clones.

## Gameplay systems

- `GAME-01` — Design separate typed, namespaced block/item/entity registries with uint32 runtime IDs, sparse per-position custom data, deterministic manifests, and strict saved-world content locks: normal play must refuse to open a world when a required mod is missing.
- `GAME-02` — Design pre-1.5 redstone and block updates with a path toward modern redstone.
