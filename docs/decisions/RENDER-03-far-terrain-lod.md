# RENDER-03 Far-terrain level of detail

Status: Proposed

Owner: Rendering research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Owner-selected outcome: Ship the first end-to-end playable with finite full-detail
sections plus fog, then require a deliberately modest fog-obscured far-terrain layer
before the v1 release. Preserve an LoD-aware render key/source interface and prototype
the simplest bounded derived representation that works; a sparse **3D power-of-two
voxel mip pyramid** remains the leading universal candidate, not a pre-approved quality
project.

One-sentence rationale: Distant terrain is part of the desired v1 world scale, but
heavy fog and low-detail silhouettes let it ship after the vertical slice without
requiring beautiful transitions, full materials, hidden interiors, or an extreme
horizon.

This is a staged decision:

- **First-playable target:** no far-world data system; render ordinary sections to a
  measured radius and terminate them with fog.
- **V1 release target:** bounded coarse terrain beyond the near radius, visibly
  softened/terminated by fog, with cheap material classes and graceful fog fallback.
  The exact representation and horizon remain prototype outputs.
- **Post-v1 quality target:** extend a successful representation toward the existing
  2,048-block experiment, better reducers/transitions, and per-dimension quality.
- **Production claim boundary:** never promise arbitrary or “infinite” render distance.

## Context and constraints

- VibeCraft wants far chunks across a tall sparse 3D world whose initial build range is
  approximately 10,000 blocks. It also wants caves, Nether-like enclosed terrain,
  structures, player edits, modded blocks, lighting, transparent materials, and
  multiplayer.
- A heightmap is compact for an Overworld surface but cannot generally represent stacked caves, bridges, floating islands, interiors, or VibeCraft's tall sparse 3D build range.
- Far terrain is derived visual data. It must never become authoritative for collision, block selection, simulation, saves, or anti-cheat.
- Multiplayer clients cannot reconstruct the true far world from a seed: saved edits, generated structures, generator version, plugins, and undisclosed terrain all diverge. The server must decide what far data exists and may be sent.
- Every extra LoD multiplies storage, invalidation, streaming, meshing, GPU residency, and transition states. “LoD saves triangles” is only one term in the cost.
- Custom models and advanced materials necessarily lose detail. The far contract needs an explicit approximation/omission rule rather than silently running every near-field shader at kilometer distances.
- Godot's built-in mesh LoD simplifies geometry already in a mesh. It does not create a sparse world summary, reduce the number of world sections, solve streaming, or preserve voxel topology/material semantics.

### Spec assumptions challenged

- “Far chunk support through LoD” is not one renderer feature. It is a cross-system feature touching world generation/storage, networking, asset registries, lighting, fog, large coordinates, and cache migration.
- LoD cannot be visually universal across the Overworld, Nether, and End with one 2.5D surface approximation. VibeCraft either accepts per-dimension policies or pays for a 3D representation.
- Reflective/refractive/animated custom materials cannot retain near-field behavior indefinitely. Far LoD must reduce them to stable opaque/alpha-test/emissive summaries or omit them.
- Generating unexplored terrain on the client can reveal information and diverge from server state. The default must be “fog where the server has not authorized summary data.”

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| Full-detail sections + fog | Correct, one world representation, fastest route to playable | Finite view distance; section/draw count grows quadratically/cubically | **Required first-playable fallback** |
| Godot automatic per-mesh decimation | Built in; useful for imported props | Does not reduce section count/data; procedural block faces/material boundaries may collapse badly | Reject for terrain LoD |
| 2.5D heightfield/column spans | Compact and fast for distant surface landscapes | Weak for caves, overhangs, floating terrain, enclosed dimensions and vertical builds | Reject as universal format; possible optional Overworld backend later |
| Sparse 3D voxel mip pyramid | Represents any 3D occupancy; blocky style; edits can propagate up levels | Memory/cache/network cost; representative-material loss; hard transitions | **Leading v1 prototype candidate** |
| Independently generated coarse chunks | No need to store/downsample exact chunks | Generator divergence, seams, structures/edits mismatch, duplicate server work | Reject as default authoritative path |
| Smooth SDF octree + Transvoxel | Mature seamless LoD for smooth volumetric terrain | Changes block silhouette and material semantics; conversion is lossy | Reject for base block terrain |
| Impostor panoramas/terrain cards | Very low geometry for a fixed view | View-dependent updates, parallax/occlusion errors, hard edits and multiplayer caching | Defer to skybox/special vista use |
| GPU voxel ray traversal/clipmap | Can render huge sparse volumes with custom culling | Custom Godot renderer, hardware/bandwidth constraints, duplicate near pipeline | Research only after a shipped conventional renderer |

## Evidence

### Minecraft and its renderer ecosystem

**Sourced facts.** Mojang/Microsoft's Bedrock GDC 2026 presentation describes terrain assembly in queued 16×16×16 render units with one vertex-buffer range and multiple terrain-layer index ranges ([official slides, p. 14](https://media.gdcvault.com/gdc2026/Slides/Fairfield_AJ_ModernizingTheRenderingOfMinecraft.pdf)). This establishes the ordinary near-terrain baseline examined here; the presentation does not describe a built-in far LoD pyramid.

Distant Horizons is an open source Minecraft mod whose stated purpose is to render simplified chunks outside normal render distance ([project repository](https://gitlab.com/distant-horizons-team/distant-horizons)). Its history provides valuable negative evidence:

- Version 2.1.0 fixed a `ColumnRenderSource` massive-memory issue, removed an “unlimited horizontal quality” mode that its renderer could not run at higher distances, removed a failed seamless-overdraw experiment, and disabled cave culling above detail level 0 because holes could reveal the void ([official release notes](https://gitlab.com/distant-horizons-team/distant-horizons/-/releases/2.1.0a)).
- Server-plugin releases have required old LoD data to be deleted/rebuilt after format changes, and recent fixes explicitly mention worlds taller than roughly 2,000 blocks ([official server-plugin releases](https://gitlab.com/distant-horizons-team/distant-horizons-server-plugin/-/releases)). Derived LoD formats need versioning and a rebuild path.
- Its API and issue history expose column data, vertical quality reduction, cave-geometry culling, and difficulties updating/splitting data columns for custom blocks ([graphics API](https://distant-horizons-f74d0f.gitlab.io/com/seibel/distanthorizons/api/interfaces/config/client/IDhApiGraphicsConfig.html), [custom framed-block issue](https://gitlab.com/distant-horizons-team/distant-horizons/-/issues/1029)).

Voxy is another open source Minecraft LoD renderer. Its source stores a 16³ section plus successive 8³, 4³, 2³, and 1³ mip levels in one voxelized section ([`VoxelizedSection`](https://github.com/MCRcortex/voxy/blob/dev/src/main/java/me/cortex/voxy/common/voxelization/VoxelizedSection.java)) and reduces each 2×2×2 group through a dedicated `Mipper` ([mip builder](https://github.com/MCRcortex/voxy/blob/dev/src/main/java/me/cortex/voxy/common/voxelization/WorldVoxilizedSectionMipper.java)). Its separate world and client-render packages show that voxelization/storage and rendering are distinct systems ([world source](https://github.com/MCRcortex/voxy/tree/dev/src/main/java/me/cortex/voxy/common/world), [renderer source](https://github.com/MCRcortex/voxy/tree/dev/src/main/java/me/cortex/voxy/client/core/rendering)).

**Inference.** Distant Horizons demonstrates the viability and pitfalls of aggressive far rendering; Voxy provides stronger evidence for a full 3D mip hierarchy. Neither implementation can be copied into Godot/C#, and their achieved distances are not VibeCraft performance promises.

### Clones and voxel engines

#### Terasology

Terasology's official LoD post says distant chunks are static/non-interactable and discard all CPU data except the GPU mesh. It also documents failures from generating each LoD chunk independently: neighbor-dependent lighting, AO, and face culling become wrong; slightly oversized/overlapping meshes were used to cover edges and scale gaps; forcing all exterior faces visible made underground chunks inefficient ([official technical postmortem](https://terasology.org/blog/tera-spotlight-extreme-view-distances-with-lod-chunks/)). The same post describes far-clip/depth-precision tradeoffs once distances exceeded the prior 5,000-unit assumption.

This directly supports overlap as a pragmatic seam tool and warns against independent coarse generation without neighbor context.

#### Cubyz

Cubyz advertises 3D chunks with no height/depth limit and LoD for far view ([project repository](https://github.com/PixelGuys/Cubyz)). Its source models chunk LoD explicitly and has a dedicated GPU-oriented chunk renderer ([chunk source](https://github.com/PixelGuys/Cubyz/blob/master/src/chunk.zig), [renderer source](https://github.com/PixelGuys/Cubyz/blob/master/src/renderer/chunk_meshing.zig)). Cubyz is evidence that 3D chunks and blocky far LoD can coexist, but its Zig/custom-renderer architecture is much lower-level than Godot `ArrayMesh`.

#### Voxel Tools for Godot

Voxel Tools has an octree `VoxelLodTerrain` and seamless Transvoxel LoD for smooth terrain, while its project roadmap lists LoD with blocky voxels as a separate future area ([official overview](https://voxel-tools.readthedocs.io/en/latest/overview/), [project readme](https://github.com/Zylann/godot_voxel)). The Transvoxel algorithm itself uses transition cells to join multiresolution marching-cubes meshes ([primary algorithm site/paper](https://transvoxel.org/)).

This is direct evidence that smooth-voxel transition technology is real, and equally direct evidence that it does not automatically solve blocky LoD in the closest Godot-specific voxel implementation.

### Godot rendering facilities

- Godot supports automatic mesh LoD generated on asset import and manual visibility-range/HLOD switching. Manual HLOD can replace several nearby objects with one distant object and includes hysteresis/fade margins ([mesh LoD docs](https://docs.godotengine.org/en/stable/tutorials/3d/mesh_lod.html), [visibility-range docs](https://docs.godotengine.org/en/stable/tutorials/3d/visibility_ranges.html)).
- `ArrayMesh` accepts explicit alternate index buffers keyed by LoD distance/error ([ArrayMesh API](https://docs.godotengine.org/en/stable/classes/class_arraymesh.html)). This can switch topology within one already-created mesh but does not decide which world-resolution source is correct.
- Godot's large-world performance guide recommends streaming worlds in tiles and notes float-precision mitigation through large-world coordinates or origin shifting ([official 3D optimization guide](https://docs.godotengine.org/en/stable/tutorials/performance/optimizing_3d_performance.html)).

### Conclusions and uncertainty

**Directly supported:** far LoD needs its own derived data, versioning, memory policy, update pipeline, and seam strategy. 3D mip pyramids are used by Voxy; independent coarse generation causes neighbor artifacts in Terasology; smooth Transvoxel is not a blocky-terrain solution in Voxel Tools.

**Informed inference:** a sparse 3D pyramid is the least-wrong universal representation for VibeCraft's current goals. It is more expensive than columns, but it does not bake an Overworld-only shape assumption into the engine.

**Unknown until prototyped:** the correct 2×2×2 material reducer, visible quality of blocky transitions, Godot object/upload limits at a 2,048-block horizon, compressed bytes per far cell, and acceptable server bandwidth.

## Proposed design

### 1. Stage 0: first-playable finite renderer

- Use `RENDER-01/02` full-detail section meshes to a configurable measured radius. Initial product tuning should test 12, 16, and 20 horizontal chunk radii; do not hard-code an “extreme” number into architecture.
- Use `RENDER-07` fog to hide the streaming frontier. Missing data is fog, never a client-generated guess.
- Keep `RenderSectionKey` LoD-aware (`Lod = 0` for near terrain) and route mesh requests through an interface rather than directly reading the near world store:

```csharp
public interface ITerrainRenderSource
{
    ValueTask<TerrainSnapshotLease?> TryAcquireAsync(
        TerrainTileKey key,
        TerrainRevision minimumRevision,
        CancellationToken cancellationToken);
}

public readonly record struct TerrainTileKey(
    DimensionId Dimension,
    FarTileCoord Coordinate,
    byte Lod);

public readonly record struct FarTileCoord(long X, long Y, long Z);
```

`TerrainTileKey` remains engine-independent and signed-64-bit. The Godot adapter may
derive a checked, render-origin-relative `Vector3I` only for a resident local window;
that temporary value is never a world key, hash input, or persisted/network identity.

- Do not build the LoD cache, network messages, hierarchy selector, or transition
  shader before the end-to-end vertical slice passes. The interface/key are the
  migration seam. Far-terrain implementation begins as a bounded v1 release follow-up.

### 2. Leading v1 far-data candidate

The design below is the universal 3D candidate to prototype, not a mandate to ship
all of its 2,048-block quality targets in v1. The first v1 profile may use fewer levels,
a shorter fog-hidden horizon, no shadows, opaque/cutout/emissive summaries only, and
simple overlap/skirts. A cheaper per-dimension column-span candidate may win for the
initial Overworld-like dimension if it preserves the documented replacement seam and
does not become the universal world/save contract.

Each LoD level `L` contains sparse cubic tiles of 16³ logical cells. One cell spans `(2^L)³` world blocks; therefore one tile spans `16 × 2^L` blocks on each axis. Empty tiles are absent.

```csharp
public readonly record struct FarCell(
    uint FarMaterialId,
    byte OpaqueCoverage,
    byte SkyLight,
    byte BlockLight,
    FarCellFlags Flags);

[Flags]
public enum FarCellFlags : byte
{
    None = 0,
    Empty = 1,
    AlphaTest = 2,
    Fluid = 4,
    Emissive = 8,
    Unknown = 16,
    PreserveThinFeature = 32
}
```

This is the logical format, not a promised physical struct layout. The cache/network codec may palette materials and bit-pack fields after measuring quality and compression.

Near block render definitions compile an explicit `FarRepresentation`:

- `Omit`: particles, tiny decorations, and models too small to survive.
- `Cube(material)`: ordinary opaque/alpha-tested blocks and simplified custom models.
- `Fluid(material)`: stable far-water/lava approximation; no refraction.
- `Emissive(material, intensity)`: simplified emissive landmark.
- `PreserveThinFeature`: opt-in for visually important thin structures, with strict per-pack quotas because it increases aliasing/geometry.

The far registry is deterministic and versioned independently from near assets. Unknown/mod-missing materials map to a stable diagnostic or neutral fallback material, never to executable pack behavior.

### 3. Authoritative derivation and reduction

- The server derives LoD 0 summaries from **generated canonical chunks including saved edits and plugin results**, then reduces parents in 2×2×2 groups. Singleplayer uses the same local server path.
- By default, the server sends no summary for ungenerated/unauthorized terrain. Optional LoD-only world generation is a future server policy requiring `WORLD-06` and threat/game-design review.
- Parent reduction is a pure versioned function over eight child cells. It outputs empty only when all children are empty/omitted, tracks coverage, chooses a representative material from surface-contributing children, takes conservative/max emissive light, and carries fluid/alpha flags only when they dominate a visible contribution.
- The exact representative-material score and occupancy threshold are the riskiest visual assumptions. Implement three reducer policies behind the same interface for the prototype: majority occupancy, exposed-face weighted, and thin-feature preserving. Greenlight one from image/geometry/memory results; do not let implementations improvise different reducers.
- A block edit updates the affected base summary and propagates through its parent chain. Batch parent updates for 100 ms before network/persistence publication; revisions make temporary delay safe.
- Coarse data is a rebuildable cache, not part of the canonical save transaction. Store a format version, reducer version, world/dimension ID, generator epoch, and far-material-registry hash. Any incompatible mismatch discards/rebuilds the affected cache; it never blocks loading the world.

### 4. Multiplayer contract

The server owns a separate far-terrain interest stream. It sends versioned compressed tiles by `(dimension, coordinate, lod, revision)` with cancellation/supersession and bandwidth quotas. Near canonical chunks always supersede far summaries in overlapping space.

Clients may persist encrypted-or-plain cache according to project privacy policy, but keys must include server identity, world instance ID, dimension, format/reducer version, and far registry hash. A cache entry is display-only and is discarded when the server rejects its revision.

The stream must support:

- sparse tile add/update/remove;
- material palette negotiation independent of gameplay block IDs;
- request radius/capability negotiation;
- server maximum LoD/radius and per-client rate limit;
- “not available” without triggering full chunk generation;
- world/dimension switch cancellation.

Do not put full block states, inventories, block-entity data, hidden ore identities, or collision data in far cells.

### 5. Selection hierarchy

- Maintain a sparse balanced octree/clipmap index centered on the active camera and dimension.
- Choose the finest available tile whose projected cell size is below the configured pixel-error target, clamped so neighboring visible tiles differ by at most one LoD level.
- Use 20% distance hysteresis around each threshold and hold the old tile until replacements covering its area are resident. Camera teleports rebuild selection by priority but keep fog as the fallback.
- Frustum-cull sparse tile AABBs before mesh submission. Optional occlusion culling is a later optimization; cave culling is disabled until it passes hole/void tests, following the failure observed by Distant Horizons.
- Selection is camera-dependent but data residency is bounded by a shell around selected tiles. Do not keep all visited render columns in managed memory; Distant Horizons' memory history makes this a greenlight requirement.

### 6. Coarse meshing and materials

- Reuse the cube visibility/greedy concepts from `RENDER-01` over `FarCell`, scaled by `2^L`; use a dedicated far vertex/material format so near custom-model data is not carried kilometers away.
- Mesh with a one-cell halo at the same LoD for correct same-level face culling. Neighbor absence is `Unknown`; exposed frontier faces are permitted under fog and invalidated when data arrives.
- Far surfaces are limited to opaque, alpha-tested, fluid approximation, and emissive approximation. Disable normal/parallax/refraction/detail animation by default; shader cost should fall with geometry detail.
- Decorations/entities use Godot HLOD/impostors independently from terrain and may have shorter visibility ranges.

### 7. LoD transitions and seams

Blocky terrain has no adopted Transvoxel equivalent in this plan. Use this staged seam policy:

1. Same-level tiles use exact ownership bounds plus a halo; they may not generate duplicate coplanar boundary faces when both neighbors are resident.
2. Selection guarantees adjacent levels differ by at most one.
3. Coarser tiles remain under the finer ring for a two-coarse-cell overlap. Complementary blue-noise/dither fade masks and fog make handoff gradual; 20% hysteresis prevents rapid switching.
4. Coarse boundaries adjacent to finer tiles generate conservative inward/downward skirts from boundary faces to hide subpixel and T-junction cracks. Skirts never participate in collision or shadows at the finest cascade.
5. If the prototype shows visible caves, walls, or water leaking through skirts/overlap, replace that boundary with explicit fine-to-coarse transition faces derived from both border resolutions. Do not ship by merely increasing fog until tests become impossible to see.

Terasology's documented overlapping LoD chunks support overlap as a useful first mitigation, but its remaining lighting/face issues mean this is an experiment, not proof of seamlessness.

### 8. Precision and origin

- All tile identities and derivation use 64-bit integer world coordinates. Mesh vertices remain local to a tile.
- Rendering uses the same camera-relative origin/rebasing policy as near sections. Far distance must not convert canonical coordinates to `float` early.
- Start the far prototype at a 2,048-block radius. Increasing the camera far plane is not free: measure depth precision, shadow cascades, fog, and PBR artifacts before testing 4,096+.

### 9. Failure behavior

- Missing, delayed, rate-limited, corrupt, or incompatible far data renders fog. It never triggers a near full-chunk request by accident.
- Cache checksum/codec/version failure deletes only the derived entry and requests/rebuilds it; canonical world saves are untouched.
- A newer tile revision makes queued mesh/upload work stale through `RENDER-02`.
- Unsupported far material maps to its pack-declared fallback; a pack without one is omitted and diagnosed during pack validation.
- If the far subsystem exceeds CPU/GPU/network memory caps, evict farthest/coarsest-priority residents and shorten the far horizon. Never evict required near terrain to preserve a cosmetic horizon.
- If the server has no far capability, the client silently uses finite near rendering/fog; joining does not fail.

## Greenlight criteria

### First-playable finite renderer

- The first-playable renderer holds 60 Hz on agreed reference hardware at the selected ordinary render radius with `RENDER-01/02` budgets and no far system enabled.
- Fog/stream-frontier transitions do not reveal persistent holes during normal movement and teleport recovery.
- `TerrainTileKey` and source abstraction add no measurable hot-loop cost and do not
  force LoD cache/network code into the first-playable core.

### Minimal v1 far renderer

- A coarse authorized terrain silhouette is visible beyond the full-detail radius in
  the Overworld-like v1 dimension and terminates naturally in fog; it need not expose
  caves, interiors, transparent detail, shadows, or near-field materials.
- Missing/late/corrupt/over-budget far data falls back to fog without blocking near
  terrain, interaction, or joining.
- CPU, GPU, memory, disk-cache, and per-client network use have hard caps and plateau
  under a one-hour movement/edit soak; far work is shed before near gameplay work.
- Edits never make an older far revision reappear, and no far representation is used
  for collision, selection, generation authority, spawning, or saves.
- The selected v1 horizon and quality are recorded with the target hardware and fog
  profile rather than becoming an unbounded render-distance promise.

### Extended post-v1 far renderer

- At a 2,048-block radius and 1080p on the agreed reference desktop, far terrain adds **≤3.0 ms GPU p95**, **≤1.0 ms client CPU p95 in steady movement**, and **≤3.0 ms client CPU p99 during ring changes**, measured separately from near terrain.
- Resident far CPU cache is **≤256 MiB** and far GPU mesh storage is **≤256 MiB** at the target radius/quality; managed memory remains bounded after one hour of continuous exploration.
- At 20 blocks/s after warmup, far-stream traffic is **≤1 MiB/s p95** per client and never delays required near chunk traffic; final bandwidth is server-configurable downward.
- Same-level and mixed-level boundaries show no background/void crack wider than one pixel in automated camera sweeps across flat, mountainous, cave, floating-island, tall-build, water, and Nether-like fixtures.
- A canonical block edit appears in all affected resident LoD levels within **1 second p95** without stale parent reappearing after a newer child revision.
- The selected reducer wins a blinded comparison over heightfield and majority baselines on silhouette error while meeting memory/geometry budgets; thin towers, bridges, emissive landmarks, and fluids have documented approximation behavior.
- Corrupt caches, reducer-version upgrades, server capability absence, and material-registry changes all recover to fog/rebuild without world corruption, disconnect, or crash.
- Increasing far distance cannot force generation or disclose block-level hidden terrain unless the server explicitly enables that policy.

## Prototype or benchmark

Required: yes, after the first-playable loop and before v1 far terrain ships

Smallest useful experiment:

1. Build a standalone 16³ `FarCell` pyramid with levels 0–4, patterned after the independently verifiable 16³→8³→4³→2³→1³ shape in Voxy, but using VibeCraft's own logical format and no copied code.
2. Implement the three reducers (majority, exposed-face weighted, preserve-thin-feature) over fixtures: mountain/cave, floating islands, 1-block tower/bridge, village-sized structures, layered glass/water, emissive path, Nether cavern, and checkerboard adversary.
3. Render fixed near/far scenes in Godot with 128-, 512-, 1,024-, and 2,048-block radii. Capture triangle/draw counts, CPU selection/mesh/upload, GPU frame time, memory, and screenshots from a scripted camera path.
4. Implement balanced one-level adjacency, overlap/dither, and skirts. Run pixel/background-leak detection over boundary sweeps and compare against explicit transition-face experiment on failed fixtures.
5. Simulate multiplayer tile compression/streaming at 20 and 80 blocks/s under 100 ms latency and 1% loss/reordering using the proposed `NET-05/07` transport contract. Measure bytes and time-to-horizon; do not count local disk prewarming as network success.
6. Edit blocks continuously at LoD boundaries and assert revision monotonicity from child summaries through parents, meshes, and committed residents.
7. Soak for one hour while circling through already visited and new regions. The resident/queued/cache memory graph must plateau.

Success metrics: the minimal v1 criteria are mandatory. The 2,048-block numbers are
extended-profile targets. If 3D mip memory/bandwidth fails but Overworld visuals pass,
investigate a per-dimension column-span backend rather than weakening future
Nether/cave correctness globally. If every candidate fails the bounded v1 profile,
record the failed release requirement and fall back visibly to fog while revising the
scope; do not disguise near terrain as far LoD.

## Risks and open questions

- A 3D pyramid can be much more expensive than column spans. Sparse empty-tile omission and compression are assumptions to validate, especially in cave-filled dimensions.
- Representative-material reduction may erase player art, thin redstone-like structures, or modded geometry. Pack opt-ins can also be abused and require quotas.
- Opaque occupancy thresholds can either inflate thin geometry or erase it. This is why the reducer is a measured prototype decision.
- A 2,048-block radius may still exceed Godot's practical `MeshInstance3D`/buffer churn budget. A later arena/indirect renderer could be necessary.
- Dither overlap and skirts can hide cracks but produce overdraw, shadow discontinuities, or visible internal cave walls. Explicit transition meshes may be unavoidable.
- Far cache persistence can become a migration burden. It must remain deletable/rebuildable and separately versioned from canonical saves.
- Server-side summary generation consumes CPU, disk, and egress. Public servers need quotas and may disable it entirely.
- Multiple cameras/portals complicate camera-centered hierarchies and dimension ownership. Production v1 should support one active terrain camera; portal views need an explicit later design.
- Huge far planes interact with depth precision, cascaded shadows, atmospheric fog, reflections, and origin shifting; `RENDER-07` and large-world tests are dependencies, not polish.
- Exact target hardware and acceptable view distance remain product decisions. This brief supplies a 2,048-block experiment target, not a shipping guarantee.

## Dependencies

- Requires: `WORLD-01` 3D section coordinates and snapshots; `WORLD-03/09` derived-cache placement/version policy; `WORLD-06` generator-version/authorization policy; `RENDER-01/02` meshing and revisioned jobs; `RENDER-07` fog; large-world coordinate policy.
- Requires for multiplayer: `NET-05` interest/prioritization and `NET-07` capability/version negotiation; server resource budgets and privacy policy.
- Requires for visuals: `ASSET-03` far representations/material fallbacks and `RENDER-04/05/06` coarse light/material rules.
- Blocks: claims about maximum render distance, far terrain networking/storage implementation, and per-dimension LoD quality settings.

## Rejected or deferred alternatives

- **Implement far LoD before the first playable loop:** rejected; the vertical slice
  remains the prerequisite. Minimal far terrain is a v1 release requirement after it.
- **Use Godot automatic mesh LoD as the terrain system:** rejected; it simplifies an existing mesh but does not solve world-resolution data, tile count, streaming, edits, or materials.
- **Universal heightmap:** rejected because it contradicts caves, tall sparse vertical sections, floating terrain, and enclosed dimensions.
- **Generate coarse terrain independently from the seed on clients:** rejected by default because it diverges from edits/plugins/generator versions and can reveal unauthorized terrain.
- **Store far data in canonical chunk saves:** rejected; derived data must be discardable after format/reducer changes.
- **Transvoxel for exact block terrain:** rejected; it solves smooth isosurface transitions and changes the desired silhouette.
- **Unlimited/fixed extreme distance:** rejected; Distant Horizons' history demonstrates that “unlimited quality” and far memory need real bounds.
- **Cave occlusion/culling in the first far version:** deferred until hole/void regression tests pass.
- **Full PBR/refraction/custom animation at every LoD:** rejected; far materials use explicit cheaper representations.
- **GPU-driven Voxy/Cubyz-style backend immediately:** deferred. Their source is architectural evidence, not proof that Godot/C# should begin at their optimization endpoint.
