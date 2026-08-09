# RENDER-05 Lighting resolution and GPU representation

Status: Proposed

Owner: Rendering research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Interpret “different light levels at 1/64 of a block” as per-fragment shading of 64×64 material detail, **not** as persistent 64×64×64 world-light samples. Store two logical 0–15 light values per block, interpolate them across visible surfaces in the fragment shader, and prototype an 18³ RG8 page (16³ interior plus one-cell halo) per resident near section.

One-sentence rationale: A 64³ field makes a single 16³ section consume 2 GiB for only two 8-bit channels, whereas a haloed block-resolution page is 11.39 KiB and still shades every rendered pixel independently.

The 64³ interpretation is rejected. The shader-page path remains conditional on the prototype; vertex-baked block light from `RENDER-04` is the defined fallback.

## Resolve the specification ambiguity

The current wording combines three unrelated resolutions:

1. **Asset resolution:** a block face uses a 64×64 color/normal/material texture.
2. **Shading frequency:** the GPU evaluates a fragment shader for visible screen fragments. A close block can produce far more than 4,096 fragments; a distant block can produce far fewer.
3. **World-light storage:** persistent samples representing sky exposure and propagated block light in 3D space.

Only the first was clearly requested. The second is already how rasterized PBR works. Matching the third to texture texels is neither required nor generally meaningful: UVs can rotate, tile across greedy quads, map onto custom models, or animate, while world light remains fixed in world space.

VibeCraft therefore defines the requirement as:

> A 64×64 terrain texture may vary normal, roughness, metalness, albedo, opacity, and emissive response per texel, and visible fragments are lit independently. Broad sky/block illumination is sampled from a lower-resolution world-space field and smoothly reconstructed. No promise is made that 262,144 independent light values exist inside each block.

## Quantitative cost analysis

All numbers below are raw logical bytes before allocator metadata, alignment, staging copies, compression, page tables, or driver duplication. `KiB`, `MiB`, `GiB`, and `TiB` use powers of two.

### Volumetric interpretation

A 16³ section contains 4,096 blocks. At 64 samples per block edge:

```text
(16 blocks × 64 samples/block)³
= 1,024³
= 1,073,741,824 samples per section
```

That is exactly 1 Gi sample. Therefore one section costs:

| Encoding | Bytes per sample | Bytes per 16³ section |
| --- | ---: | ---: |
| One scalar channel | 1 | 1 GiB |
| Sky + block (`RG8`) | 2 | 2 GiB |
| RGB + auxiliary (`RGBA8`) | 4 | 4 GiB |
| RGB half-float | 6 | 6 GiB |

One sky/block section would already exceed the entire proposed **64 MiB** near-light GPU budget by 32×. A modest 4,096-section residency would require **8 TiB** for RG8, before any terrain meshes or material textures.

The scaling is cubic. For two 8-bit channels and no halo:

| Samples per block edge | Samples per 16³ section | RG8 per section | RG8 for 4,096 sections |
| ---: | ---: | ---: | ---: |
| 1 | 4,096 | 8 KiB | 32 MiB |
| 2 | 32,768 | 64 KiB | 256 MiB |
| 4 | 262,144 | 512 KiB | 2 GiB |
| 8 | 2,097,152 | 4 MiB | 16 GiB |
| 16 | 16,777,216 | 32 MiB | 128 GiB |
| 64 | 1,073,741,824 | 2 GiB | 8 TiB |

Even 2 samples per block edge multiplies world-light memory by 8. It is not a harmless quality slider.

### Surface-lightmap interpretation

If the sentence means one stored light value per 64×64 face texel rather than a volume:

- one face has 4,096 samples;
- six faces have 24,576 samples, or 48 KiB/block for sky+block RG8;
- the six exterior faces of one completely solid 16³ section form six 1,024×1,024 maps: 6,291,456 samples, or **12 MiB RG8**;
- a 3D checkerboard with 2,048 isolated solid blocks exposes 12,288 faces: 50,331,648 samples, or **96 MiB RG8** for one section;
- reserving all six face maps for all 4,096 blocks would be **192 MiB RG8** per section.

Surface-only allocation is topology-dependent, complicates edits/custom models/greedy quads, and still loses by three to four orders of magnitude against block-scale storage. It also bakes lighting into UV space and requires regeneration when geometry or light changes.

### Recommended block-scale pages

Logical storage uses two 4-bit values per block. Packed CPU storage can therefore use one byte per cell:

```text
16³ × 1 byte = 4 KiB per section
4,096 sections = 16 MiB
```

An unpacked CPU working representation or no-halo RG8 GPU representation uses 8 KiB/section and 32 MiB for 4,096 sections.

The proposed shader page adds one neighbor cell on every side for interpolation and seam sampling:

```text
18³ × 2 bytes = 11,664 bytes = 11.39 KiB/page
4,096 pages = 47,775,744 bytes = 45.56 MiB
```

The halo is 42.4% overhead over a 16³ RG8 interior, but it prevents cross-page filtering and avoids shader fetches from six separately bound neighbor pages. A **64 MiB** cap leaves about 18.4 MiB for slot maps, alignment, staging, and backend overhead; actual driver allocation must be measured.

For comparison, a two-sample-per-block page with the same one-sample halo is 34³×2 = 78,608 bytes (76.77 KiB); 4,096 pages are about **307 MiB** before overhead. This is why the default cannot silently become 2×.

### Material texture cost is separate

A 64×64 RGBA8 image is 16 KiB at its base level; a complete mip chain approaches 21.33 KiB. A common albedo RGBA8 + normal RG8 + MERS RGBA8 set is 40 KiB at base or about **53.33 KiB with mips per frame**. A 64×64 asset has 16× as many texels as a 16×16 asset—not 4×—but that texture memory should not be multiplied through the 3D world-light field. `RENDER-06` owns material-frame budgets.

## Options considered

| Representation | Visual behavior | Update coupling | Raw near memory | Decision |
| --- | --- | --- | --- | --- |
| Four-corner light baked in mesh | Smooth over each quad, cheap shader | Light update remeshes | Vertex payload only | Required fallback |
| One sky/block sample per block, nearest shader sample | Compact, visibly stepped | Page upload only | Low | Debug/reference mode |
| One sample/block with exterior-cell trilinear reconstruction | Smooth world-space gradient; per-fragment | Page upload only | 45.56 MiB/4,096 haloed pages | **Recommended prototype** |
| Prefiltered corner/lattice light | Better face continuity in some scenes | Extra preprocessing/schema | Similar to low multiples | Prototype competitor |
| 2³ samples per block | More local gradient | 8× memory/solve cost | ~307 MiB/4,096 haloed pages | Reject unless later evidence is overwhelming |
| 64² stored surface lightmaps | Texture-aligned detail | Rebuild/repack on geometry/light | 12–96+ MiB per representative section | Reject |
| 64³ samples per block | True volumetric sub-block field | Extreme solve/upload | 2 GiB per section RG8 | Reject |

## Evidence

### Minecraft

**Sourced facts.** Bedrock's documented terrain baseline stores sky exposure and block-light intensity as values from 0–15, propagates them on the CPU, and bakes normalized values per vertex. Its 64/128-style texture and PBR work does not create matching world-light voxels; Vibrant Visuals adds normal, metalness, emissive, roughness, and subsurface texture attributes while retaining vanilla light as a semantic input ([official GDC slides, pp. 14–22 and 48–50](https://media.gdcvault.com/gdc2026/Slides/Fairfield_AJ_ModernizingTheRenderingOfMinecraft.pdf)).

The same presentation says Bedrock's existing animated block faces are texture-atlas subregion updates, another example of texture resolution remaining independent from terrain geometry/light storage.

**Lesson.** High-frequency material response can be layered over low-frequency semantic light. Minecraft's exact vertex-baked delivery is a baseline, not proof that VibeCraft should retain remesh coupling.

### Luanti

**Sourced facts.** Luanti stores one `u8 param1[nodecount]` for light in each 16³ MapBlock—4 KiB raw—and presents optional smooth lighting/AO without increasing the stored voxel resolution ([official data-structure documentation](https://docs.luanti.org/for-engine-devs/basic-data-structures/), [official configuration source](https://github.com/luanti-org/luanti/blob/master/minetest.conf.example)).

**Lesson.** The exact packing differs, but its magnitude independently validates per-block rather than per-texture-texel world light.

### Voxel Tools for Godot

**Sourced facts.** Voxel Tools bakes block-neighbor ambient occlusion into vertex color and recommends material reuse/atlasing to reduce draw calls ([official block mesher API](https://voxel-tools.readthedocs.io/en/latest/api/VoxelMesherBlocky/), [official blocky-terrain guide](https://voxel-tools.readthedocs.io/en/latest/blocky_terrain/)). It does not make texture resolution determine voxel-light resolution.

**Lesson.** The closest Godot-specific voxel implementation obtains important high-frequency depth cues from geometry-local interpolation rather than dense world-light volumes.

### Veloren

**Sourced facts.** Veloren's development report describes richer PBR, water attenuation/refraction, and shadow maps, but also significant performance degradation and the addition of configurable quality/optimized shaders ([official development report](https://veloren.net/blog/devblog-81/)).

**Lesson.** Renderer quality must be decomposed into scalable terms; a cubic canonical “quality resolution” would make graceful degradation impossible.

### Godot storage and sampling facts

Godot defines `RG8` as two 8-bit components and supports 3D textures for custom shaders ([official `Image` formats](https://docs.godotengine.org/en/stable/classes/class_image.html), [official `ImageTexture3D` API](https://docs.godotengine.org/en/stable/classes/class_imagetexture3d.html)). `Texture2DArray` requires equal dimensions and mip counts across layers and keeps each layer's mip chain separate, which is useful for materials but is not automatically a partial-update light-page system ([official `Texture2DArray` documentation](https://docs.godotengine.org/en/stable/classes/class_texture2darray.html)).

### Evidence, inference, and unknowns

**Directly supported:** production voxel games use block-scale light and interpolate/bake presentation; Godot has formats capable of storing compact RG data; 64³ memory arithmetic is deterministic.

**Informed inference:** 18³ RG8 pages are a good logical target because they keep neighbor filtering local. Whether they should be physically represented as a 3D atlas, 2D-array slices, or another `RenderingDevice` allocation is not yet known.

**Unknown until measured:** actual VRAM alignment, Godot upload stalls, descriptor limits, texture-cache behavior of trilinear 3D sampling, and which surface reconstruction avoids light leaks with the least shader cost.

## Proposed design

### 1. Logical resolution contract

- Near canonical light: one `LightCell` per block, two values constrained to 0–15.
- CPU persistence/cache: packed sky/block nibbles when cold; unpacked bytes are allowed only in bounded solve buffers.
- GPU page interior: 16³ RG8 values for a 16³ render section.
- GPU halo: one cell on all six sides, producing 18³ logical texels.
- Filtering: no mipmaps; clamp sampling to the assigned page including halo so adjacent atlas slots never bleed.
- Far LoD: use the coarse sky/block summary in `FarCell` from `RENDER-03`, baked into far vertices or a far-specific representation. Do not spend near-page memory on far tiles.
- Quality settings change shadow resolution, effects, material maps, and page radius—not the authoritative light lattice.

Section size remains supplied by `WORLD-01`. For interior edge `N`, page dimensions are `(N+2)³`, and all budgets must be recalculated if `N` changes.

### 2. Surface reconstruction candidates

Prototype and choose exactly one production mode:

1. **Nearest exterior cell:** sample the air/light cell just outside the rendered face. It is the no-leak oracle but visibly steps.
2. **Trilinear exterior cells:** offset the world position slightly along the face normal, convert to page coordinates, and trilinearly interpolate RG values. It should provide smooth gradients across a greedy quad without adding light to the mesh merge key.
3. **Prefiltered corner field:** during page preparation, derive corner samples from valid neighboring non-occluding cells, then interpolate. It can match Minecraft-like smooth lighting more closely but adds preparation rules and storage/layout risk.

Sampling must use world/section-local position, not UV, so texture rotation, animation, atlas layout, and greedy tiling do not move the light. Test face directions and custom-model normals separately; template models may use nearest or explicitly authored sample anchors if their surface lies inside a cell.

### 3. Physical page backend

Keep one renderer-global binding and a slot table:

```csharp
public readonly record struct LightPageLayout(
    byte InteriorEdge,
    byte Halo,
    LightSampleFormat Format);

public interface IGpuLightPageBackend : IDisposable
{
    LightPageCapacity Capacity { get; }
    LightPageSlot Allocate(RenderSectionKey key, uint lifetime);
    void QueueFullPageUpload(LightPageSlot slot, ReadOnlyMemory<byte> rg8, LightRevision revision);
    void Release(LightPageSlot slot, uint lifetime);
}
```

Compare three implementations behind this contract:

- a 3D texture atlas with manually assigned brick coordinates;
- a 2D-array slice atlas where each page owns 18 slices and the shader performs Z interpolation;
- a lower-level tiled/buffer representation if the pinned Godot `RenderingDevice` cannot update the first two without whole-resource stalls.

Do not create one texture resource or material per section. `ImageTexture3D.update()` replaces the full texture's layer data, so it is suitable for a prototype page or full atlas only if measured; it is not evidence of cheap subregion updates.

Start with whole-page uploads. At 11.39 KiB/page, 100 changed pages are about 1.11 MiB; subregion complexity is justified only if full-page telemetry misses the upload budget.

### 4. Residency and fallback

- Default capacity: 4,096 near pages under a 64 MiB logical/metadata budget.
- Residency priority: camera-containing section, visible near sections, recently visible sections, then prefetch shell. Far sections never displace required near pages.
- A section is sampleable only when its exact `ContentRevision`, `LightRevision`, registry revision, and lifetime match.
- If capacity is exhausted, evict the farthest non-visible page. A visible section without a page uses its vertex-baked Stage A values; if unavailable during first load, use conservative dimension ambient.
- Keep CPU packed light independently of GPU residency. Page eviction is visual-cache loss, not world-state loss.
- Device loss discards all slots and repopulates by priority from CPU snapshots.

### 5. Upload/data flow

```text
LightSolveResult (16³ packed logical cells)
  -> page-preparation worker copies interior + six neighbor halos
  -> OwnedLightPageUpload (18³ RG8, exact revisions)
  -> bounded main/render-thread commit
  -> slot revision becomes resident atomically
  -> shader page table exposes slot on next frame
```

Missing halo input does not become full light. Retain the previous valid halo or use a conservative zero/opaque boundary and mark the page dependent on neighbor arrival. Page preparation and upload follow the coalescing/stale-result rules in `RENDER-02`.

### 6. Limits and budgets

- Logical light levels: 0–15 sky and 0–15 block; no pack-defined precision.
- Default pages: 4,096; hard allocation cap determined from measured physical VRAM, not an unbounded dictionary.
- Logical near-page allocation including tables/staging: **≤64 MiB GPU-resident target** and **≤32 MiB retained CPU light target** for 4,096 sections. Temporary solve/upload owners are separately bounded and must plateau.
- Full-page upload: **≤2 MiB/frame** and **≤0.5 ms main-thread p95/frame** at 60 Hz.
- Shader cost: Stage B world-light sampling adds **≤0.75 ms GPU p95 at 1080p** and **≤1.25 ms at 1440p** versus identical Stage A material/shadow settings.
- No lighting-only section remeshes after Stage B is active.
- No user-facing 2×/4×/64× world-light resolution setting in v1. Experimental builds may compare 2× only with explicit VRAM telemetry.

## Greenlight criteria

- Documentation/UI consistently distinguishes texture resolution, fragment shading, and world-light storage.
- Arithmetic and runtime telemetry agree within 10% for allocated CPU/GPU page bytes; hidden driver duplication is reported rather than omitted.
- The selected page backend supports 4,096 logical pages without per-section textures/materials and remains inside the memory/upload/GPU budgets.
- Trilinear or corner reconstruction wins a blinded screenshot comparison over nearest and Stage A while producing zero light pixels through the canonical one-block opaque-wall fixture.
- All six face orientations, greedy quads, section seams, signed coordinates, render-origin rebases, missing halos, and page eviction/reuse pass automated tests.
- Light-only edits update pages without changing mesh revisions or quad counts.
- Page slot reuse cannot expose another section's old light for even one committed frame under randomized unload/reload/device-loss tests.
- A 30-minute exploration/edit soak reaches stable residency, managed memory, staging memory, and descriptor counts.

## Prototype or benchmark

Required: yes.

Smallest useful experiment:

1. Build a logical page generator for 16³ interiors and halos; fill deterministic ramps, hard walls, point sources, and neighbor-seam patterns.
2. Implement Stage A plus the three reconstruction candidates in one terrain shader fixture. Capture numerical light buffers and screenshots at 64×64, 1080p, and 1440p views.
3. Implement the 3D-atlas and 2D-array-slice backends. Upload 1, 10, 100, and 1,000 randomly changed pages per frame; measure conversion, render-thread stall, bandwidth, VRAM, and sampling GPU time.
4. Cycle 16,384 section identities through 4,096 slots while teleporting and editing. Assert exact revision/lifetime binding and poison freed slots to expose stale reads.
5. Render cave, overhang, thin wall, glass, water, torch gradient, giant greedy wall, custom stair/fence model, and section-corner fixtures. Include rotating sun and material normal maps to show that 64×64 detail is independently shaded.
6. Run the same scene with 16×16 and 64×64 material assets while keeping light pages identical; report material memory/bandwidth separately from world-light memory.
7. Optional evidence-only test: allocate a single 2× page pool and compare quality/cost. Do not attempt a 64× allocation; validate its rejection with checked arithmetic/unit tests.

Success metrics: all greenlight criteria and budgets above. If both page backends stall or leak, retain Stage A vertex-baked lighting and preserve the page interface for a future custom backend. Do not compromise by storing surface 64² lightmaps.

## Risks and open questions

- The target Godot version and renderer are not pinned; texture-update APIs and physical allocation can change.
- 4,096 pages may be too many or too few for the selected section size/view distance. Capacity is a budget outcome, not gameplay authority.
- Trilinear interpolation of discrete flood-fill levels can look smooth but non-Minecraft-like. A LUT/quantization option can preserve style without changing storage.
- Custom templates whose visible surfaces sit inside a block need explicit sample-anchor rules to avoid sampling the block's own opaque cell.
- A page atlas can suffer precision/bleed at slot boundaries. Halos and coordinate clamps require GPU regression images, not only CPU tests.
- CPU nibble packing saves memory but complicates SIMD/queue updates; solvers may use bounded unpacked work buffers.
- Colored light may need more than RG8. It must use a separate versioned visual representation and pass a new memory study rather than silently widening every page.

## Dependencies

- Requires: `WORLD-01` final section edge, `RENDER-04` logical solver and authority split, `RENDER-01/02` vertex/page revision hooks, and `ARCH-03` renderer ownership.
- Requires for greenlight: pinned Godot renderer/version, reference hardware, view distance/resident-section target, and a partial-update feasibility result.
- Blocks: final `MeshVertex.ShadingData`, light-page shaders/backend, lighting-related material semantics, and near/far light handoff.

## Rejected or deferred alternatives

- **64³ persistent light samples per block:** rejected at 2 GiB per 16³ section for RG8.
- **64² persistent light samples for every visible face:** rejected due topology-dependent 12–96+ MiB representative section costs and edit/repack coupling.
- **Tie world light to texture UVs:** rejected; world light must remain stable under animation, rotation, custom models, and pack changes.
- **Increase canonical light resolution with graphics quality:** rejected; simulation/memory behavior must not change by a cubic factor between clients.
- **One texture/material per section:** rejected; it destroys batching and multiplies Godot resource/descriptor churn.
- **2× block light by default:** rejected at roughly 307 MiB for 4,096 haloed RG8 pages; retain only as a measured research comparator.
- **Assume compression will rescue dense light:** rejected; dynamic 3D data, update granularity, platform formats, and random-access sampling make that an unproven dependency.
- **Make page sampling mandatory for first playable:** deferred behind Stage A until the Godot benchmark passes.
