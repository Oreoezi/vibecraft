# RENDER-01 Chunk meshing architecture

Status: Proposed

Owner: Rendering research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Use CPU-built, section-local indexed meshes with hidden-face culling and an axis-sweep greedy fast path for full cubes; route non-cubic block definitions through a separate immutable template emitter. Partition output by render layer and keep the mesher independent of Godot objects.

One-sentence rationale: This is the smallest architecture that substantially reduces geometry, handles editable terrain and arbitrary-height 3D chunks, and leaves room for custom models and later GPU packing without making the first playable build depend on experimental rendering techniques.

### Owner decision — 2026-08-13

The owner accepted the recommended architecture: section-local CPU meshes,
hidden-face correctness baseline, greedy full-cube fast path, immutable non-cube
templates, and no Godot objects in worker meshing. Throughput, exact section side,
vertex format, and upload backend remain benchmark gates. Binary/GPU meshing, mesh
shaders, and ray-cast voxels remain optimizations behind measured bottlenecks.

## Context and constraints

- The world is editable one block at a time and may have no fixed vertical limit. Rendering therefore needs independently rebuildable 3D sections, not one mesh for an entire vertical column.
- Gameplay initially resembles early Minecraft, but packs may later add custom block models and materials. A cube-only mesher cannot be the public extension interface.
- The client is Godot with C#. Expensive voxel traversal belongs in plain C# worker code; scene-tree and GPU-resource operations have stricter threading constraints.
- Lighting and block state affect face appearance. The merge key must preserve every visible discontinuity or decouple that attribute into shader-readable data.
- Texture resolution (16, 64, or otherwise) is not a geometry concern. A 64×64 texture must not create 64×64 geometry or per-pixel light records in the chunk mesh.
- Near-field correctness matters more than minimum triangle count: edits must not expose holes, merge different materials, smear light/AO, or leave stale faces at section seams.

### Spec conflicts to resolve explicitly

- “A different light level for each 1/64 of a block” conflicts with baked vertex lighting and can destroy most greedy merge opportunities if interpreted as geometry. Treat this as a shader/lighting-texture question for `RENDER-05`, not a meshing requirement.
- Arbitrary custom models, transparent/refractive materials, and aggressive greedy merging cannot all share one algorithm. VibeCraft needs render-definition classes with different emitters.
- Perfect alpha blending for intersecting water, glass, particles, and custom translucent models is not delivered by chunk meshing. Even Minecraft Java is still changing this area: the 26.3 Snapshot 2 notes describe triangle sorting as expensive and introduce approximate order-independent transparency with a higher performance cost ([official snapshot notes](https://feedback.minecraft.net/hc/en-us/articles/47030118645389-Minecraft-Java-Edition-26-3-Snapshot-2)).

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| Emit every cube | Trivial, useful correctness oracle | Six faces per block, hidden geometry, excessive vertices/draw work | Reject except as a test oracle |
| Hidden-face culling only | Simple, predictable rebuild cost, supports per-face data | Large flat areas remain many quads | Keep as reference implementation and fallback |
| Axis-sweep greedy quads | Large reduction on planar terrain; deterministic; CPU-friendly | Merge-key complexity; tiled UV and AO/light boundaries need care; no direct benefit for arbitrary models | **Recommended cube fast path** |
| Binary/bitwise greedy meshing | Very high throughput on dense cube grids | More specialized layout, debugging cost, awkward custom states/models; published demo benchmarks are not VibeCraft benchmarks | Defer until profiling proves CPU meshing is limiting |
| Monotone/polygon triangulation | Can reduce vertices in some shapes | More topology complexity and T-junction concerns; weaker fit for square textured faces | Reject for v1 |
| GPU compute/mesh-shader meshing | Potentially parallel and GPU-driven | Godot integration, portability, synchronization, readback/indirect-buffer complexity; competes with rendering | Defer as a backend experiment |
| Instanced cube rendering/ray casting | Avoids CPU mesh rebuilds in some workloads | Fill/bandwidth cost, hidden voxels, custom pipeline and collision mismatch | Reject for the conventional near field |

## Evidence

### Minecraft

**Sourced facts.** Microsoft/Mojang's GDC 2026 Bedrock rendering presentation says terrain is assembled in 16×16×16 units, queued around the moving player, assigned one range in a preallocated vertex pool, and given separate index-buffer ranges for opaque, foliage, water, and blended layers. It also says lighting is baked into each vertex ([official GDC slides, pp. 14–20](https://media.gdcvault.com/gdc2026/Slides/Fairfield_AJ_ModernizingTheRenderingOfMinecraft.pdf)). This is useful evidence for small rebuild units, pooled GPU storage, render-layer separation, neighbor-sampled AO, and the coupling caused by baked lighting.

Java Edition exposes the cost/correctness tradeoff of immediate rebuilds. Snapshot 21w37a added a “Priority Update” setting controlling which chunk sections update synchronously; less synchronous work reduces placement/light-source stutter but can make updates appear later ([official 21w37a notes](https://feedback.minecraft.net/hc/en-us/articles/4409293520269-Minecraft-Java-Edition-Snapshot-21w37a)). Modern Java mappings also expose section meshes and distinct chunk section layers rather than one monolithic terrain stream ([NeoForge 1.21.6 primer](https://github.com/neoforged/.github/blob/main/primers/1.21.6/index.md)).

**Inference.** Minecraft validates section-local meshes and layer separation, but it does not prove that copying its exact face emitter, vertex format, or baked-light strategy is optimal for VibeCraft. Bedrock and Java are also distinct renderers; the GDC facts must not be presented as Java internals.

### Clones, engines, and libraries

#### Luanti (formerly Minetest)

- Luanti's fundamental client render unit is a 16×16×16 `MapBlock` with one client-side mesh ([engine data-structure documentation](https://docs.luanti.org/for-engine-devs/basic-data-structures/)). Its mesh input explicitly includes at least a one-node “onion layer,” which is direct evidence for neighbor padding at seams ([`MeshMakeData` source](https://github.com/luanti-org/luanti/blob/master/src/client/mapblock_mesh.h)).
- The source has separate transparent buffers, camera-relative transparent-buffer updates, smooth-light sampling, and a solid-side bitset ([generated source reference](https://doxy.minetest.net/mapblock__mesh_8h_source.html)). These are signs that transparency, culling, and neighbor lighting become first-class mesh concerns rather than post-hoc flags.
- Luanti handles many node draw types rather than forcing every block through a full-cube algorithm ([`content_mapblock.cpp` source](https://github.com/luanti-org/luanti/blob/master/src/client/content_mapblock.cpp)).

#### Voxel Tools for Godot

- Voxel Tools converts voxels to chunked polygon meshes, supports infinite paging, and has distinct blocky and smooth meshers ([project source/readme](https://github.com/Zylann/godot_voxel)). `VoxelMesherBlocky` batches block-type meshes, while the terrain system remeshes only edited regions ([official overview](https://voxel-tools.readthedocs.io/en/latest/overview/)).
- Its roadmap still lists blocky-voxel LOD while Transvoxel LOD is available for smooth terrain. This is important negative evidence: smooth isosurface LOD is not automatically a solution for a Minecraft-shaped world ([project readme](https://github.com/Zylann/godot_voxel)).
- Voxel Tools documents 16 or 32 mesh blocks as a draw-call-versus-edit-cost choice; larger blocks reduce object count but make edits more expensive ([official performance notes](https://voxel-tools.readthedocs.io/en/latest/performance/)).

#### Sodium (Minecraft Java rendering replacement)

- Sodium keeps section compilation, render-layer buffers, world snapshots/slices, and region rendering as separate subsystems in its open source tree ([chunk renderer source tree](https://github.com/CaffeineMC/sodium/tree/dev/common/src/main/java/net/caffeinemc/mods/sodium/client/render/chunk)). Its purpose is explicitly to improve frame rate and micro-stutter ([project readme](https://github.com/CaffeineMC/sodium)).
- Sodium is evidence for compact section-oriented data and specialized hot paths, but not for greedy meshing specifically. Minecraft's general block-model contract and mod compatibility constrain what it can merge.

#### Cubyz and algorithm references

- Cubyz is a block sandbox with 3D chunks and multiple LoD scales; its renderer source is a useful example of a more custom, GPU-oriented endpoint ([chunk data](https://github.com/PixelGuys/Cubyz/blob/master/src/chunk.zig), [chunk renderer](https://github.com/PixelGuys/Cubyz/blob/master/src/renderer/chunk_meshing.zig)). It demonstrates that large-distance block rendering can evolve beyond one Godot `MeshInstance3D` per section, but adopting that endpoint at v1 would import substantial engine complexity.
- Mikola Lysenko's original technical comparison explains axis-sweep greedy meshing and the need to include voxel type/orientation in the face comparison ([meshing article](https://0fps.net/2012/06/30/meshing-in-a-minecraft-game/)). The follow-up shows that greedy faces can preserve voxel AO when vertices along a merged edge agree ([AO article](https://0fps.net/2013/07/03/ambient-occlusion-for-minecraft-like-worlds/)). These are technical articles, not production postmortems.
- The open binary-greedy demo includes reproducible Criterion benchmarks and source, but its numbers cover its Rust/Bevy layout and scenes, not C#/Godot or custom models ([demo repository](https://github.com/TanTanDev/binary_greedy_mesher_demo)).

### Evidence-based conclusions versus inference

**Directly supported:** small 3D render units, neighbor padding, worker meshing, layer separation, and main/render-thread-aware upload are recurring in Minecraft, Luanti, Sodium, and Voxel Tools.

**Informed inference:** a conventional greedy cube fast path is the best first implementation for VibeCraft because it is much simpler than a GPU-driven renderer and does not prevent one later. No source establishes a universal best section size or merge key for VibeCraft.

**Unknown until measured:** C# meshing throughput, Godot's practical per-surface upload cost, vertex memory after Godot conversion, and whether greedy reduction matters more than draw-call count on target hardware.

## Proposed design

### 1. Render unit and input snapshot

- Mesh one cubic `RenderSection` at a time. Start the prototype at 16³ blocks; keep the size supplied by `WORLD-01`, not hard-coded into public interfaces.
- The worker receives a dense immutable snapshot of the section plus a one-block halo on all six sides. For a 16³ section this is 18³ samples. A sample contains render-definition ID, canonical block state, and the lighting/AO inputs selected by `RENDER-04/05`.
- Coordinates in emitted vertices are local to the render section. The instance transform carries the world origin, avoiding large-world float precision loss in vertex data.
- Missing-neighbor cells are represented as `Unknown`, not silently conflated with air. The v1 policy renders a frontier face as exposed, then invalidates both sections when the neighbor arrives. Fog hides most frontier churn; correctness tests verify the eventual mesh.

### 2. Immutable render definitions

Resource packs/mods compile on the client main/loading thread into immutable data-only definitions. Meshing workers never call pack scripts, Godot resources, or arbitrary mod callbacks.

```csharp
public enum GeometryClass : byte
{
    Empty,
    FullCube,
    Template,
    DynamicInstance
}

public enum RenderLayer : byte
{
    Opaque,
    AlphaTest,
    Translucent,
    Fluid
}

public sealed record BlockRenderDefinition(
    uint DefinitionId,
    GeometryClass GeometryClass,
    OcclusionMask Occlusion,
    ImmutableArray<CompiledQuad> TemplateQuads,
    MaterialBinding Material);
```

- `FullCube`: hidden-face culling plus greedy merge.
- `Template`: copy prevalidated model quads; cull only faces whose compiled coverage mask proves full occlusion. No greedy merging in v1.
- `DynamicInstance`: chests, animated machinery, block entities, and models requiring per-instance state are excluded from the static chunk mesh and submitted to a separate instance system.
- A template that exceeds configured quad/material limits fails pack validation before any worker sees it.

### 3. Full-cube algorithm

For each of the six face directions:

1. Compare each voxel with its neighbor and emit a mask cell only when the face is visible under the two definitions' occlusion rules.
2. Build a `FaceMergeKey` for the visible face.
3. Scan each 2D slice in deterministic row-major order, growing maximal width and then height rectangles with identical keys.
4. Emit one indexed quad per rectangle with winding chosen by face direction.

The merge key is semantic, not merely a block ID:

```text
FaceMergeKey =
  renderLayer
  materialBinding + textureVariant + UV rotation
  faceDirection
  tint/biome class
  connected-texture state (if greenlit)
  four-corner baked-light/AO values (only if baked)
  shader/animation flags that alter interpolation
```

Two faces merge only when the resulting pixels are intended to match. If later lighting is sampled from a world-space texture, its samples leave the merge key; if light is baked per vertex, the four corner values remain. Never use approximate equality in this key.

Texture coordinates use face-local repeating coordinates plus a material/tile identifier. The shader performs atlas-safe or texture-array sampling. Stretching one atlas tile across a giant greedy quad is a bug; the exact material binding is owned by `ASSET-03/RENDER-06`.

### 4. Output surfaces and vertex semantics

Each result contains at most one indexed surface per populated render layer, not one surface per block material. Materials are selected through an atlas/array binding so pack variety does not multiply draw calls.

```csharp
public readonly record struct MeshVertex(
    Vector3 Position,
    Vector3 Normal,
    Vector2 FaceUv,
    uint MaterialId,
    uint ShadingData);

public sealed record CpuSectionMesh(
    SectionMeshKey Key,
    ImmutableArray<CpuSurface> Surfaces,
    Aabb LocalBounds,
    MeshStatistics Statistics);
```

`MeshVertex` is the logical contract. The prototype must select and document a Godot-compatible packed representation; do not expose physical byte offsets to packs or gameplay code. `ShadingData` is versioned and reserved for values such as sky light, block light, AO, or tint index after the lighting decisions settle.

Use indexed triangles (four vertices and six indices per quad). The diagonal is selected consistently from corner AO/light values to avoid visible interpolation flips. Empty layers allocate no surface.

### 5. Transparency policy

- `AlphaTest` (leaves, grass-like cutouts) participates in ordinary depth writing and can use the cube/template paths.
- `Translucent` and `Fluid` are separate surfaces. V1 may cull hidden faces and merge only identical coplanar full-cube faces, but it does not promise globally correct intersecting transparency.
- V1 accepts documented ordering artifacts and limits resource-pack combinations that require physically correct intersecting refraction. Per-triangle resorting, weighted blended OIT, or another OIT method must be decided in `RENDER-06` after a Godot prototype.
- Never force opaque and blended geometry into one surface merely to save a draw call.

### 6. Staged implementation

**Stage A — correctness oracle:** hidden-face culler for full cubes, one immutable template emitter, and a mesh-to-visible-face test oracle.

**Stage B — v1 optimization:** greedy rectangle pass, per-layer surfaces, face AO/light merge keys, telemetry, and worker-safe pooled buffers.

**Stage C — only after profiling:** bitset visibility masks, SIMD/binary greedy scan, packed custom vertex buffers, mega-buffer allocation, or indirect draws. The public input/output contracts remain unchanged.

GPU meshing and mesh shaders are not on the v1 path. Godot's procedural geometry documentation says its built-in procedural methods run on CPU and do not generate geometry on GPU ([official docs](https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/index.html)); a GPU backend would therefore be a custom RenderingDevice/engine integration, not a small substitution.

### 7. Failure behavior

- Invalid model/material definitions fail pack compilation with the pack, namespace, model, and offending limit; they become a visible missing-model cube, not a worker exception loop.
- A meshing exception keeps the previous mesh, records the section/revision/definition IDs, and allows one rate-limited retry. Initial failure renders a debug placeholder only in development builds.
- Integer overflow, index-width overflow, and vertex-count limits are checked before upload. A pathological template surface is split deterministically or rejected at validation.
- Mesh output is deterministic for a snapshot and registry revision so it can be hash-tested and replayed.

## Greenlight criteria

- The cube culler and greedy mesher produce exactly the same set of oriented exposed unit faces across randomized sections, including all six boundaries and missing/arriving neighbors.
- Full-cube, template, alpha-test, translucent, and fluid fixtures render without cross-layer contamination; unsupported transparency artifacts are documented rather than hidden.
- On the agreed reference desktop in an exported release build, a 16³ section meshes in **≤2.0 ms p95** for representative terrain and **≤5.0 ms p95** for the adversarial checkerboard/material fixture on one worker; measure allocations separately.
- Repeated meshing reaches steady state with **≤8 KiB managed allocation per job** after pools warm up, excluding the retained result arrays. No unbounded per-block objects or LINQ allocations occur in the hot loop.
- A flat 16³ solid section emits 6 quads; greedy output never has more quads than the hidden-face oracle for the same supported cube inputs.
- Godot renders the logical vertex fields correctly on the supported Forward+ backend, including tiled 64×64 textures, AO/light discontinuities, UV rotations, and large local-to-world offsets.

## Prototype or benchmark

Required: yes

Smallest useful experiment:

1. Implement a library-only hidden-face oracle and greedy mesher over 16³ snapshots plus halo.
2. Add fixtures: empty, solid, flat layers, cave, checkerboard solid/air, alternating materials, every boundary case, water/glass, and one stair/fence/template model.
3. Property-test 10,000 seeded random sections by expanding greedy quads back into oriented unit faces and comparing them with the oracle.
4. Benchmark release C# with pooled and unpooled outputs; record p50/p95/max time, quads, vertices, bytes, and allocations.
5. Upload 1,024 generated section meshes to Godot through the pipeline in `RENDER-02` and measure upload/frame cost separately from meshing.

Success metrics: all greenlight criteria above, no seam mismatch after neighbor arrival, and no material/light interpolation across unequal merge keys. A failure means retain hidden-face culling for the first playable build and optimize from profiles; it does not justify jumping directly to GPU meshing.

## Risks and open questions

- `WORLD-01` may choose a section size other than 16³. The benchmark must test the chosen size; 32³ reduces object/draw count but increases edit rebuild work by 8× in voxel count.
- Baked per-vertex light couples every light propagation change to remeshing. Shader-sampled light can improve mergeability and update cost but needs a separate texture/page pipeline (`RENDER-04/05`).
- Godot's standard `ArrayMesh` representation may consume more vertex memory than a custom packed backend. Optimize the backend without exposing it in the block definition API.
- Large numbers of unique translucent surfaces can dominate sorting and fill rate even when opaque geometry is excellent.
- Greedy quads can expose texture derivative/mipmap artifacts at atlas boundaries. Material sampling needs dedicated tests.
- Connected textures and biome blending expand the merge key and pack contract; defer unless required for the first art pack.

## Dependencies

- Requires: `WORLD-01` section dimensions/data layout; `ASSET-03` compiled block/model/material definitions; provisional light samples from `RENDER-04/05`.
- Blocks: `RENDER-02` mesh scheduling/upload contract; near/far handoff in `RENDER-03`; parts of `RENDER-06` material batching.

## Rejected or deferred alternatives

- **One node/mesh per block:** rejected; it multiplies scene objects and draw work and ignores the core advantage of chunked voxel rendering.
- **One mesh for an entire unbounded-height column:** rejected; rebuild and culling granularity become pathological.
- **Greedy merge every model:** rejected; arbitrary models do not form a regular face mask and may have different occlusion/material semantics.
- **Transvoxel/marching cubes for the base world:** rejected; it changes the blocky silhouette and targets sampled isosurfaces, not exact cube faces.
- **Binary greedy as the first implementation:** deferred; keep it as a compatible internal fast path if the conventional pass misses measured budgets.
- **GPU meshing/mesh shaders/ray casting:** deferred until a profiling report shows CPU mesh generation or draw submission is the dominant limiter and a Godot backend prototype proves portability.
- **Perfect transparency in this decision:** deferred to `RENDER-06`; v1 separates layers and states its correctness limit.
