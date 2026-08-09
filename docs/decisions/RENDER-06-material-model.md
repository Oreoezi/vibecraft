# RENDER-06 Terrain material model and render tiers

Status: Proposed

Owner: Rendering research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Compile resource-pack materials into one fixed, data-driven terrain material bank shared by all sections. Use at most four terrain passes—opaque, alpha-test, translucent, and fluid—with per-vertex material IDs indexing shared 64×64 albedo, normal, and MERS texture arrays plus a compact metadata/animation table. Emission, PBR reflectivity, and flipbook animation are attributes inside a pass; true transparency and refraction are constrained higher-cost tiers with explicit fallbacks.

One-sentence rationale: Shared shaders and texture banks preserve section batching while exposing useful PBR controls, whereas a Godot material/shader per block type would multiply draw calls, pipeline state, resource lifetime, and untrusted extension surface.

V1 does not promise physically correct nested transparency, arbitrary resource-pack shaders, ray-traced reflections, or full PBR at far LoD. It promises deterministic material compilation, predictable fallbacks, and measurable quality tiers.

## Context and constraints

- Terrain meshes are section-local and should have no surface per block material.
- Greedy full-cube faces and custom template models must use the same material binding contract.
- The default terrain art target is 64×64 per block face, with mipmaps and animated sequences.
- Emissive pixels must be distinct from gameplay light emission.
- Reflection and refraction techniques available in Godot have renderer and visibility limits.
- Alpha blending is both a correctness and fill-rate problem; voxel scenes can expose many overlapping water/glass faces.
- Resource packs are data, potentially untrusted, and must not inject arbitrary GPU code or unbounded material variants.
- Far LoD must use reduced materials from `RENDER-03`; it cannot carry every near material feature to kilometers of terrain.

## Options considered

| Option | Strengths | Costs and failure modes | Fit for VibeCraft |
| --- | --- | --- | --- |
| One Godot material/surface per block type | Easy author mapping | Draw/surface explosion; pack reload/resource churn | Reject |
| One shared atlas and a few shaders | Good batching, broad compatibility | Mipmap padding/bleed and manual animation updates | Viable fallback |
| Shared `Texture2DArray` maps + material table | Isolated mip chains, simple material ID, shader-selected animation | Equal-size layers, layer limits, custom shader/backend validation | **Recommended prototype/default** |
| Multiple texture banks selected per section | Raises pack capacity | Multiplies surfaces/draws and complicates greedy output | Defer; reject for v1 baseline |
| Bindless textures/materials | Flexible and scalable | Godot/backend portability and descriptor complexity | Research after profiling |
| Resource-pack-authored shaders | Maximum creativity | Security, portability, validation, compilation, and batching become uncontrolled | Reject for untrusted packs |
| Sorted alpha transparency | Conventional and available | Section/triangle ordering errors and CPU resort cost | Restricted v1 path |
| Approximate order-independent transparency | Handles intersecting layers without sorting | Extra buffers/passes and approximation artifacts | Enhanced experiment |
| Full ray/path-traced material pipeline | Excellent reflections/refraction/GI | Hardware-specific and expensive | Optional future backend |

## Evidence

### Minecraft

**Sourced facts.** Minecraft Bedrock's traditional terrain renderer uses a mega-texture for block-face color, separate terrain layers such as opaque, foliage, water, and blended, and updates animated block faces as atlas subregions. Vibrant Visuals adds albedo, normal, metalness, emissive, roughness, and subsurface inputs, with attributes either uniform per face or texture-painted ([official GDC slides, pp. 14, 20, and 22](https://media.gdcvault.com/gdc2026/Slides/Fairfield_AJ_ModernizingTheRenderingOfMinecraft.pdf)).

Bedrock's official pack documentation describes PBR texture sets and MERS data, uses image-based lighting and SSR for reflections, and states that off-screen reflections and most transparent SSR are unavailable. It also separates PBR emissive appearance from block `light_emission`, which is used by gameplay ([official PBR pack guide](https://learn.microsoft.com/en-us/minecraft/creator/documents/vibrantvisuals/vvresourcepacks?view=minecraft-bedrock-stable), [official light-source guide](https://learn.microsoft.com/en-us/minecraft/creator/documents/vibrantvisuals/lightingcustomization?view=minecraft-bedrock-stable)). Bedrock flipbooks define frame order and tick duration independently from mesh geometry ([official animated-block tutorial](https://learn.microsoft.com/en-us/minecraft/creator/documents/createanimatedblocktexture?view=minecraft-bedrock-stable)).

**What worked.** A constrained data-driven PBR model lets thousands of existing blocks share one renderer and preserves legacy color assets as albedo. Animation remains texture behavior, not geometry rebuild behavior.

**Visible cost/debt.** Mojang reports creating more than 3,000 new PBR textures and used presets/masks plus automated generation to make that workload tractable. Official Bedrock texture budgets count uncompressed texels at four bytes each and recommend lower-resolution/subpack choices by hardware tier ([official texture-budget guidance](https://learn.microsoft.com/en-us/minecraft/creator/documents/texturebudgets?view=minecraft-bedrock-stable)). Rich materials are an asset-pipeline and memory problem, not only a shader feature.

Minecraft Java 26.3 Snapshot 2 states that back-to-front translucent sorting is expensive and cannot correctly handle intersecting triangles even with perfect triangle order. Its new OIT mode is approximate, has minor color artifacts through multiple layers, and is expected to cost more than its prior transparency path ([official snapshot notes](https://feedback.minecraft.net/hc/en-us/articles/47030118645389-Minecraft-Java-Edition-26-3-Snapshot-2)).

### Voxel Tools for Godot

**Sourced facts.** Voxel Tools recommends reusing a texture atlas/material to reduce draw calls and describes a typical blocky setup with three materials: opaque, alpha clip, and transparent. It warns that alpha blending has limitations across multiple transparent surfaces, supplies transparency indices for face-culling semantics, and gives fluids their own procedural model/material path ([official blocky-terrain guide](https://voxel-tools.readthedocs.io/en/latest/blocky_terrain/)).

**Lesson.** A small fixed pass set and a dedicated fluid class fit Godot voxel rendering. The number of block definitions should not determine the number of section surfaces.

### Veloren

**Sourced facts.** Veloren's lighting/material overhaul introduced physically based response, a semi-accurate index of refraction, water attenuation, and shadow mapping. Its development report says the combined lighting/LoD/shadow changes significantly degraded performance and required feature-specific quality settings and shader optimizations ([official development report](https://veloren.net/blog/devblog-81/)).

**Lesson.** PBR/refraction can support a stylized voxel world, but expensive terms need independent capability tiers and cannot be mandatory pack semantics.

### Godot

**Sourced facts.** Godot's standard material documentation says opaque rendering is fastest; alpha blending is significantly slower when layers overlap, has sorting issues, cannot cast shadows in the standard path, and is absent from screen-space reflections. Alpha scissor avoids sorting issues and can cast shadows. Godot refraction is screen-space: it cannot refract itself/other transparent materials or off-screen objects and can produce incorrect edges ([official material documentation](https://docs.godotengine.org/en/stable/tutorials/3d/standard_material_3d.html)).

Godot's SSR/SSIL/SDFGI are Forward+-only, and SSR only reflects opaque depth-writing geometry ([official renderer comparison](https://docs.godotengine.org/en/stable/tutorials/rendering/renderers.html), [official environment documentation](https://docs.godotengine.org/en/stable/tutorials/3d/environment_and_post_processing.html)). `Texture2DArray` requires equal dimensions and mipmap counts for every layer, but keeps layers/mip chains separate and exposes array sampling to shaders ([official `Texture2DArray` documentation](https://docs.godotengine.org/en/stable/classes/class_texture2darray.html), [official array shader node](https://docs.godotengine.org/en/stable/classes/class_visualshadernodetexture2darray.html)).

### Evidence, inference, and unknowns

**Directly supported:** fixed terrain layers, shared texture resources, data-driven PBR maps, flipbook animation, alpha-test preference, and constrained/reflection fallbacks are established patterns. Transparency remains approximate or expensive even in Minecraft and Godot.

**Informed inference:** one texture-array bank and one shared shader per render layer is the cleanest VibeCraft v1 contract because all baseline face assets are 64×64. Sources validate the pieces, not Godot/C# performance or a 2,048-layer cap.

**Unknown until measured:** array-layer/descriptor limits on target GPUs, custom vertex material-ID cost, material-table lookup cost, imported pack compression, practical transparent overdraw, and whether a custom OIT compositor integrates cleanly with Godot fog/SSR/post-processing.

## Proposed design

### 1. Fixed render classes

Retain the `RenderLayer` contract from `RENDER-01`:

| Layer | Depth/blend behavior | Allowed material features | V1 guarantee |
| --- | --- | --- | --- |
| `Opaque` | Depth write, no blend | Albedo, normal, MERS, emission, animation | Full |
| `AlphaTest` | Depth write + alpha scissor/hash | Same as opaque; two-sided only when declared | Full with hard/dithered edges |
| `Translucent` | Separate blended pass, section-level ordering | Thin color transmission, optional screen refraction | Restricted; artifacts documented |
| `Fluid` | Separate blended/depth-aware pass | Animated normals, absorption, surface waves, optional refraction/caustic approximation | Water/lava-oriented restricted path |

Emission, reflectivity, and animation are **not** extra layers. A reflective metal block remains opaque; an emissive lamp remains opaque/cutout unless its alpha behavior independently requires another layer.

Each section has at most one populated surface per layer in v1. All surfaces bind renderer-global shader/material resources. A material ID in the vertex selects texture layers and scalar metadata, so one section containing 500 block materials still submits at most four terrain surfaces.

### 2. Compiled material contract

Resource-pack declarations compile to immutable engine-owned records:

```csharp
public enum MaterialFeatureTier : byte
{
    Baseline,
    Enhanced,
    Experimental
}

public sealed record CompiledTerrainMaterial(
    uint MaterialId,
    RenderLayer Layer,
    TextureFrameSetId Frames,
    MaterialFeatureTier MinimumTier,
    Vector4 BaseColorFactor,
    Vector3 EmissionTint,
    float EmissionStrength,
    float AlphaCutoff,
    float IndexOfRefraction,
    Vector3 AbsorptionColor,
    float AbsorptionDistance,
    MaterialFlags Flags,
    uint FallbackMaterialId,
    uint FarMaterialId);

public sealed record TextureFrame(
    ushort AlbedoLayer,
    ushort NormalLayer,
    ushort MersLayer,
    ushort DurationTicks);
```

`MaterialId` is a compact active-registry ID, not a persistent world identifier. Saves and network messages use namespaced block/material identifiers and negotiated mappings.

The pack compiler validates values and emits one fallback chain before workers can reference the registry. Runtime terrain shaders receive no filenames, JSON, scripts, or pack-controlled shader text.

### 3. Texture bank and PBR channels

The default v1 terrain bank contains:

- `AlbedoArray`: RGBA8; RGB color and A opacity.
- `NormalArray`: RG8 tangent-space X/Y; the shader reconstructs Z. Missing maps use the flat-normal layer.
- `MersArray`: RGBA8 with R metalness, G emissive mask, B roughness, and A subsurface mask. Missing maps use a neutral nonmetal/rough/nonemissive/nonsubsurface layer.
- `MaterialTable`: scalar flags/factors and animation descriptors in a measured shader-readable table/texture/buffer.
- `FrameTable`: frame triples and durations. Different albedo/normal/MERS images may deduplicate independently even when one animation frame changes only albedo.

All terrain source images compile to a 64×64 baseline layer and complete mip chain. The pack marks source sampling as `pixel` or `smooth`; deterministic import uses nearest-neighbor for pixel art and a high-quality filtered/renormalized path for smooth color/normal maps. Sources above 64×64 require a future enhanced-resolution profile and are rejected by the v1 compiler rather than silently increasing terrain banks.

Godot arrays require equal dimensions/mip counts within each array, which makes this canonical compile step an explicit asset requirement rather than an accidental runtime conversion.

### 4. Quantitative bank budget

At 64×64:

```text
albedo RGBA8       16 KiB base
normal RG8          8 KiB base
MERS RGBA8         16 KiB base
combined           40 KiB base per worst-case frame
full mip chains    54,610 bytes = 53.33 KiB per worst-case frame
```

With 2,048 unique layers in all three arrays, the worst-case uncompressed mipmapped payload is about **106.7 MiB**. At 4,096 it is about **213.3 MiB**. Actual imported VRAM compression can reduce this, but capability checks use measured physical allocation and retain the uncompressed estimate so compression is not required for correctness.

V1 limits:

- at most **65,535 compiled material records**;
- at most **2,048 frame records** and **2,048 unique layers per array**, including generated/procedural output and defaults;
- one active terrain texture bank, so texture capacity does not split section surfaces;
- **≤256 MiB measured GPU allocation** for the active near-terrain bank, tables, and required staging/metadata;
- one material may contain at most **256 frames**, but the global 2,048-frame limit remains decisive;
- a pack may provide lower-memory subpacks/fallbacks; activation fails cleanly if no compatible variant meets the client's capabilities.

These are v1 compiler limits, not persistent save-format limits. Raise them only with a measured multi-bank/bindless design that preserves section batching.

### 5. Feature tiers

#### Baseline: batched terrain essentials

- Opaque and alpha-test albedo, normal, MERS, tint, mipmapping, and flipbook animation.
- PBR metalness/roughness against sky/environment image-based lighting.
- Emission contributes to the HDR surface result and optional glow. It does not illuminate neighboring blocks by itself.
- Geometry AO from `RENDER-01` and sky/block field from `RENDER-04/05`.
- One global sun/moon directional light with the baseline shadow policy.
- Translucent/fluid materials use their declared non-refractive fallback if enhanced screen features are unavailable.

#### Enhanced: bounded local/screen-space effects

- Forward+ SSR where valid, always with sky/environment fallback for off-screen or transparent misses.
- SSIL/SSAO according to the renderer budget, independent of material compatibility.
- Up to the selected bounded local lights from `RENDER-04` for specular highlights.
- Thin screen-space refraction for declared translucent materials and a dedicated depth/absorption water shader.
- Optional subsurface response for foliage; it must not require another section surface.

Reflectivity is never a binary “mirror” promise. Metalness and roughness describe response; the available reflection source can be sky IBL, an authored probe, SSR, or a future backend. First-person mirrors and exact off-screen reflections are out of scope.

#### Experimental: opt-in expensive techniques

- Approximate OIT compositor/passes.
- SDFGI/advanced reflection backend.
- Higher terrain texture resolution or multiple banks.
- True colored propagated light and volumetric/refractive upgrades.

Experimental capability is never a pack's only fallback for a block required to render in gameplay.

### 6. Emissive behavior

`Mers.G × EmissionTint × EmissionStrength` produces visual HDR emission. Glow is a camera/environment feature, not a second terrain pass. The value is clamped to a documented finite range during compilation to prevent exposure-breaking or non-finite shader input.

Gameplay illumination comes solely from authoritative `BlockLightProperties.GameplayEmission` in `RENDER-04`. A pack can recolor an emissive mask but cannot create safe mob-spawn light, reveal server state, or increase propagation radius. A visible emissive surface without gameplay emission may glow without lighting its surroundings; validation warns but permits this for decorative art.

Candidate enhanced point-light color may come from `EmissionTint`, but count/intensity are selected and capped by the renderer. Losing selection removes the local specular light, not the emissive surface or broad block light.

### 7. Reflective behavior

- Every opaque/alpha-test surface can use metalness and roughness in the same shader/pass.
- Sky/environment IBL is the universal reflection fallback.
- SSR is a quality feature, not a material guarantee. Fade invalid/off-screen rays into IBL rather than black.
- Reflection probes are reserved for bounded authored scenes/special interiors. Do not attach or continuously update one per chunk; Godot documents `UPDATE_ALWAYS` as significantly costly and recommends at most one such probe per scene ([official `ReflectionProbe` API](https://docs.godotengine.org/en/stable/classes/class_reflectionprobe.html)).
- Alpha-blended geometry is not promised in SSR and does not force a duplicate opaque representation merely to appear in reflections.

### 8. Transparency and refraction

Prefer, in order:

1. opaque;
2. alpha scissor for hard foliage/fence holes;
3. alpha hash/dither for soft coverage that can tolerate noise;
4. true alpha blend only when intermediate transmission is essential;
5. screen-space refraction only for explicitly tagged thin/glass/fluid materials.

V1 behavior:

- The mesher removes internal faces only when compiled occlusion/transparency classes prove that doing so is valid; glass-vs-glass, water-vs-water, and glass-vs-water rules are explicit material-pair metadata.
- Translucent surfaces are sorted at section/object granularity. No claim is made that intersecting triangles or many nested layers are correct.
- Refractive thin surfaces sample the opaque screen/depth result once. They cannot refract themselves, another transparent surface, or off-screen geometry.
- Fluids have a separate shader using depth-derived thickness, absorption color/distance, animated normal/wave input, and the same screen-space limitation. Gameplay fluid shape remains geometry/state, not shader displacement.
- `IndexOfRefraction` is constrained to 1.0–2.5 and drives Fresnel/distortion approximation; it does not turn the path into physical multi-bounce transport.
- If refraction is unsupported or exceeds budget, use the same albedo/alpha/roughness as ordinary translucency. If blending is unsupported, use the declared alpha-hash/cutout fallback.

Prototype OIT as an Enhanced/Experimental alternative. Adopt it only if its buffers composite correctly with fog, particles, entities, water, and post-processing and if the measured artifact/performance tradeoff wins. Minecraft's continued OIT changes are evidence that this is a subsystem, not a checkbox.

### 9. Animated materials

- A compiled `TextureFrameSet` contains an ordered frame table, integer tick durations, and `Loop`, `Once`, `PingPong`, or `RandomDeterministic` mode.
- The shader chooses a frame from a client presentation clock and material metadata. Ordinary animation causes **zero mesh rebuilds and zero per-frame texture uploads**.
- `RandomDeterministic` phase derives from material ID and block/world position; it never consumes simulation RNG.
- State-driven appearance—powered lamp, crop age, furnace lit state—selects a server-authored block/material variant. It is not inferred from an unsynchronized visual clock.
- Albedo, normal, and MERS frame selection stays coherent. Missing animated maps reuse a static layer.
- Procedurally generated pack assets compile into ordinary immutable layers/frames before activation and count against identical limits; arbitrary procedural shader code is not allowed.

This intentionally differs from Minecraft's documented atlas-subregion update implementation: preloaded shader-selected frames spend more VRAM but avoid upload traffic and allow independent phases. The prototype must compare that tradeoff with atlas updates before greenlight.

### 10. Compiler, hot reload, and data flow

```text
pack declarations/images
  -> validate dimensions/channels/features/fallbacks/limits
  -> deterministic resize + mip generation + normal renormalization
  -> deduplicate layers and compile material/frame tables
  -> create immutable MaterialBank + RenderRegistry revision
  -> mesh worker writes MaterialId into vertices
  -> one shared shader per RenderLayer indexes bank at fragment time
```

- Pack loading occurs off the render hot path; final GPU resource creation/publication is budgeted and atomic.
- A failed pack compile/activation leaves the previous bank active and emits paths, material IDs, limits, and fallback diagnostics.
- Hot reload publishes a new bank/registry epoch. Old meshes keep the old bank until remeshed or rebound through a proven stable ID map; old resources are released only after no resident mesh references them.
- The loader checks the temporary old+new bank memory requirement before activation. If it cannot fit the transient cap, it asks for a world-view reload or retains the prior pack; it does not partially swap random materials.
- Unknown material IDs render a shared magenta/black missing-material fallback in development and a neutral pack-declared fallback in release.

### 11. Far-material reduction

Every near material compiles a required `FarMaterialId` or explicit `Omit` result for `RENDER-03`:

- Opaque/cutout become a representative albedo/roughness/coverage material.
- Emission becomes one bounded emissive flag/intensity.
- Fluids use a stable non-refractive far-fluid approximation.
- True transparency, normal detail, screen refraction, SSR, subsurface, and high-frequency animation are disabled or collapsed to a representative frame.

Pack activation fails if a near feature has no valid far fallback while far rendering is enabled. Far failure falls back to omission/fog, never a near shader over the full horizon.

### 12. Failure behavior

- Invalid/non-finite scalar, missing layer, unsupported feature, or fallback cycle: reject the material/pack at compile time with a deterministic diagnostic.
- Texture/bank budget exceeded: refuse activation and retain the old/default pack; never evict arbitrary layers from an active bank.
- Unsupported renderer capability: follow the compiled fallback chain and report the downgrade once, not per section/frame.
- Shader compilation failure: retain the previous known-good shader/bank; baseline missing-material shader remains built into the client.
- Transparent sort/refraction miss: render the documented approximate/fallback result; never expose uninitialized screen/depth samples.
- Animation metadata error: display the declared fallback/static first valid frame.
- Device loss: recreate the shared bank before republishing section instances; no worker owns Godot material/texture objects.

## Required telemetry and budgets

Record physical bank allocations by map/mip, unique/deduplicated layers, frame/material counts, surfaces/draws by layer, material-table fetch cost, overdraw/transparent screen coverage, shader variants, SSR/refraction misses, animation CPU/uploads, and fallback activations.

Initial exported-build gates on the agreed reference desktop at 1080p/60 Hz:

- Opaque+alpha-test albedo/normal/MERS/emission costs **≤1.5 ms GPU p95** over an albedo-only shared-shader baseline in the canonical near-terrain scene.
- Baseline through Enhanced material/reflection/refraction features fit within **≤4.0 ms GPU p95 incremental total**; each optional term has a separately captured delta and switch.
- A typical water/glass scene adds **≤1.5 ms GPU p95** and no **>4.0 ms p99** transparent spike; the torture scene is reported separately and may trigger fallback/quality reduction rather than a false pass.
- Ordinary texture animation has **≤0.1 ms client CPU p95**, performs **zero per-frame texture uploads**, and causes zero mesh invalidations.
- A section has at most four terrain surfaces and no draw count change when opaque materials rise from 8 to 1,024 IDs.
- Active near bank allocation is **≤256 MiB measured**, with frame/layer limits above; one-hour pack/animation soak has stable descriptors, RIDs, and managed memory.
- Hot reload either completes atomically inside an explicit transient cap or leaves the old pack intact; no mixed epoch appears in a committed section.

## Greenlight criteria

- One section containing every supported material feature still emits no more than one surface per populated render layer.
- Material-table/array sampling renders correct albedo, mip, normal, MERS, tint, UV rotation/tiling, and animation for greedy and template meshes on the pinned Godot renderer.
- Emissive visual changes cannot alter any server gameplay-light golden result.
- Reflective surfaces fade from valid SSR to environment fallback without black/missing regions; unsupported SSR yields the same baseline material semantics.
- Alpha-test foliage, connected/internal glass faces, nested glass, water through glass, particles behind water, and a refractive screen edge have documented automated screenshots across quality tiers.
- Animation and pack reload cause no geometry rebuild except when compiled render layer, occlusion, model, or material-ID mapping actually changes.
- Invalid/adversarial packs cannot create shaders, unlimited variants/layers, non-finite values, fallback cycles, or unbounded runtime generation.
- All memory/GPU/draw/upload budgets pass on recorded reference hardware; capability downgrades preserve readable block identity.

## Prototype or benchmark

Required: yes.

Smallest useful experiment:

1. Compile a synthetic 64×64 bank with 2,048 frame records, sparse/deduplicated normal/MERS layers, missing-map defaults, and deliberately invalid pack fixtures. Compare measured VRAM with the 106.7 MiB worst-case calculation.
2. Render 1,024 section meshes using 8, 256, and 1,024 opaque material IDs through one shared shader; verify constant surface/draw count and measure material-table/array cost.
3. Animate 512 visible materials with mixed durations/phases for ten minutes. Assert zero texture uploads/remeshes and compare memory/GPU cost against an atlas-subregion-update implementation.
4. Build a transparency torture grid: alpha-test leaves, stacked stained glass, intersecting translucent templates, water behind glass, underwater particles, section boundaries, and first-person objects. Capture sorted baseline and candidate OIT at 1/2/4/8 layers.
5. Add screen-space refraction, depth absorption, SSR, environment fallback, and selected local lights independently. Move objects on/off screen and disable Forward+ features to validate fallbacks.
6. Run pack hot reload while flying/editing: color-only, animation-only, render-layer change, material removal, budget overflow, shader failure, and device teardown. Assert epoch consistency and resource cleanup.
7. Render near/far handoff for emissive, reflective, fluid, transparent, and animated landmarks; verify the declared far reduction rather than accidental near-PBR execution.

Success metrics: all greenlight criteria and budgets above. If arrays/table lookups fail on target hardware, retain the same compiled material contract and test a padded atlas backend. If transparency/OIT fails, constrain v1 to alpha test plus dedicated approximate water/glass; do not split every block into its own material.

## Risks and open questions

- Target platforms, reference GPU, Godot version, and renderer are not yet fixed; a 2,048-layer bank is a prototype cap, not assumed universal hardware support.
- A fixed 64×64 bank simplifies batching but constrains HD packs. Multi-resolution banks require a separate draw-call/memory decision.
- Uncompressed estimates do not guarantee physical VRAM. Imported runtime packs need a platform-aware compilation/cache story from `ASSET-01/02`.
- Material table access from custom vertex data may require a lower-level `ArrayMesh`/RenderingServer packing path.
- Transparent section sorting can remain visibly wrong even when performance passes. Correctness acceptance must be explicit.
- Screen-space refraction/SSR can reveal edges, omit off-screen objects, and disagree with fog; pack authors need preview diagnostics.
- Shader-selected animation trades upload bandwidth for retained frame memory. The prototype decides whether that trade is acceptable.
- Subsurface/caustic features can expand shader variants if implemented as compile-time switches. Prefer bounded runtime flags and measured common paths.

## Dependencies

- Requires: `RENDER-01` geometry/layer/material vertex contract, `RENDER-02` registry epochs and commits, `RENDER-04/05` light/emission split and page sampling, `ARCH-03` renderer ownership, and `ASSET-03/04/05` model/animation/procedural compilation.
- Requires for greenlight: target hardware/platforms, pinned Godot version/renderer, native pack manifest/override rules from `ASSET-01/02`, and reference base-pack art fixtures.
- Blocks: final terrain shaders, material section of the resource-pack format, far-material compilation, transparency implementation, and texture/runtime memory budgets.

## Rejected or deferred alternatives

- **Godot material or mesh surface per block material:** rejected; it converts content variety directly into draws/resources.
- **Emissive as a mandatory second terrain pass:** rejected; emission is a map/term in the shared shader.
- **Visual emissive value controls gameplay light:** rejected; this would let cosmetic packs alter multiplayer rules.
- **Perfect mirror/refraction guarantee:** rejected; available reflection/screen data cannot provide it universally.
- **Globally correct sorted translucency in v1:** rejected; even perfect triangle sorting cannot solve intersecting transparency, as Minecraft's own notes explain.
- **Mandatory OIT:** deferred; approximation, integration, and buffer cost require the prototype.
- **Arbitrary pack shaders:** rejected for the untrusted native pack format; consider a separately reviewed trusted developer extension later.
- **Multiple texture banks in v1:** rejected because they multiply section surfaces unless a bindless/virtual-texture backend is proven.
- **Runtime procedural material code:** rejected; procedural sources compile to bounded ordinary textures/frames.
- **Full near PBR at far LoD:** rejected; far rendering uses explicit reduced materials.
