# RENDER-04 World-light and shading architecture

Status: Proposed

Owner: Rendering research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Split lighting into (1) a deterministic, block-resolution sky/block light field used for gameplay cues and broad indirect illumination, and (2) client-only per-fragment PBR shading using that field plus a shadowed sun, material maps, and optional bounded screen-space effects. Begin with vertex-baked access to the same light field as a correctness fallback, then adopt shader-sampled light pages only after the `RENDER-05` prototype passes.

One-sentence rationale: Block-scale propagation preserves readable Minecraft-like light behavior at a bounded cost, while per-fragment shading—not a 64³ light lattice—provides detailed response across 64×64 material textures.

This does **not** greenlight “fully client-side lighting” as an authority rule. The client owns visual shading, but the server owns every gameplay decision involving light. Both sides may run the same deterministic low-resolution solver; a resource pack may change appearance but never whether mobs spawn, crops grow, or a block counts as lit.

### Owner decision — 2026-08-10

Gameplay light remains Minecraft-like discrete server state, but propagation is not a
synchronous side effect of every block edit. Authoritative edits enqueue coalesced,
revisioned light invalidations; the world scheduler processes bounded work at a named
phase. A piston/redstone spam may create visible/gameplay-light backlog, but it may not
turn one edit into unbounded server work. Exact cadence, caps, and regional escalation
remain a benchmark gate.

## Context and constraints

- The world is editable, streamed in sparse cubic sections, and has no small fixed vertical ceiling.
- The first game should retain simple, legible Minecraft-like cave, torch, day/night, and block-update behavior.
- Lighting updates must not force synchronous chunk remeshing or mutate Godot resources from workers.
- The renderer eventually needs emissive and reflective surfaces, water/glass, fog, LoD, and 64×64 terrain textures.
- Missing vertical data is significant: a client cannot infer whether a location sees the sky in an unbounded-height world merely from the nearby sections it has loaded.
- Visual effects must degrade by hardware capability without changing multiplayer simulation.
- `RENDER-01` already reserves light/AO vertex data and distinct opaque, alpha-test, translucent, and fluid layers; `RENDER-02` supplies revisioned worker/commit semantics.

## Options considered

| Option | Strengths | Costs and failure modes | Fit for VibeCraft |
| --- | --- | --- | --- |
| Flat ambient + sun only | Tiny state and implementation | Caves, local emitters, gameplay light cues, and edits are wrong | Reject |
| Minecraft-like CPU sky/block flood fill, baked into vertices | Proven, compact, simple shader and fallback | Light changes dirty meshes; gradients are tied to vertices | Correctness baseline |
| CPU sky/block field sampled from GPU light pages | Light changes upload small pages without remeshing; per-fragment interpolation | Custom page allocator/upload path; boundary and light-leak risks | **Recommended production near-field path after prototype** |
| One Godot light node per emitting block | PBR highlights and shadows are automatic | Object count, clustered-light cost, and shadow maps scale with torches | Reject; allow a bounded selected subset |
| Godot SDFGI/VoxelGI as canonical world lighting | Rich indirect light with less custom propagation code | Renderer/platform limits, high cost, update lag, and no gameplay contract | Optional experimental visual layer only |
| GPU flood fill / voxel cone tracing | Potentially rich dynamic GI | Custom renderer, synchronization, memory, portability, debugging | Research after a shipped baseline |
| Path tracing | Highest-quality reference | Hardware-limited and incompatible with the v1 frame/portability goals | Offline/reference mode only |

## Evidence

### Minecraft

**Sourced facts.** Mojang/Microsoft's 2026 Bedrock rendering presentation describes separate sky and block values from 0–15. Both are propagated on the CPU in breadth-first flood-fill rounds, with linear one-level-per-block attenuation; Bedrock then bakes normalized results into terrain vertices. The same presentation calls sky light a measure of sky exposure, uses the two values as a lookup into a brightness texture, and treats vertex-neighbor ambient occlusion separately ([official GDC slides, pp. 14–20](https://media.gdcvault.com/gdc2026/Slides/Fairfield_AJ_ModernizingTheRenderingOfMinecraft.pdf)).

Vibrant Visuals did not replace that coarse field with a sub-block light volume. It uses vanilla sky light as an indirect diffuse exposure term and extends block lighting to colors while preserving the original gameplay cues ([official GDC slides, pp. 48–50](https://media.gdcvault.com/gdc2026/Slides/Fairfield_AJ_ModernizingTheRenderingOfMinecraft.pdf)). Its material/lighting documentation explicitly distinguishes a block's gameplay `light_emission` from a texture's visual emissive value, warns that point lights are considerably more expensive than static light, and uses image-based lighting plus screen-space reflections with known off-screen and transparent-geometry limitations ([official Bedrock light-source documentation](https://learn.microsoft.com/en-us/minecraft/creator/documents/vibrantvisuals/lightingcustomization?view=minecraft-bedrock-stable)).

**What worked.** Two compact semantic channels make darkness and light reach predictable, work around corners, and remain useful input to a much richer PBR renderer. The visual pipeline can be optional without changing gameplay.

**Accumulated cost.** Bedrock's own slides note that heightmap sky exposure is not directional and can look wrong under overhangs at oblique sun angles; vertex-baked values also couple light propagation to terrain assembly. Colored propagation is not “just RGB”: Bedrock reports propagating distinct source types to avoid hue shifting, using packed-lane operations to control cost.

### Luanti

**Sourced facts.** Luanti stores a byte of light data in `param1` for every node in a 16³ MapBlock, and the map subsystem owns lighting updates ([official engine data-structure documentation](https://docs.luanti.org/for-engine-devs/basic-data-structures/)). Its public client API exposes node light as an integer from 0–15, while node definitions distinguish emitted light and whether sunlight propagates ([official client API](https://github.com/luanti-org/luanti/blob/master/doc/client_lua_api.md)). Its client setting describes smooth lighting as vertex-edge smoothing plus simple ambient occlusion rather than higher-resolution world storage ([official configuration source](https://github.com/luanti-org/luanti/blob/master/minetest.conf.example)).

**Lesson.** Compact per-voxel light and interpolated presentation are durable even in a broadly moddable voxel engine. A single byte per block is orders of magnitude more tractable than sub-block volumes.

### Voxel Tools for Godot

**Sourced facts.** Voxel Tools' block mesher can bake edge ambient occlusion into vertex colors and can emit special chunk-covering occluder quads for directional shadows ([official `VoxelMesherBlocky` documentation](https://voxel-tools.readthedocs.io/en/latest/api/VoxelMesherBlocky/)). The project treats those facilities as meshing/material concerns rather than a general dynamic GI system.

**Lesson.** Godot can render convincing block depth from cheap local AO and ordinary directional shadows. This is evidence for a staged baseline, not proof that baked AO alone satisfies VibeCraft's lighting goal.

### Veloren

**Sourced facts.** Veloren's lighting overhaul added physically based material behavior, water attenuation/refraction, and point/directional shadow maps. Its own development report says those lighting, shadow, and LoD features caused significant performance degradation, leading to configurable quality options and shader/meshing optimizations ([official development report](https://veloren.net/blog/devblog-81/)).

**Lesson.** Rich visual terms are feasible in a voxel game, but they must be individually scalable and cannot all be treated as a free default.

### Godot renderer constraints

**Sourced facts.** Godot exposes SSR, SSIL, SDFGI, and volumetric fog only in Forward+, while renderer support for other features varies ([official renderer comparison](https://docs.godotengine.org/en/stable/tutorials/rendering/renderers.html)). Its documentation calls SDFGI one of Godot's most demanding GI techniques and notes stale results around moved static/dynamic geometry until the camera moves away ([official SDFGI documentation](https://docs.godotengine.org/en/4.3/tutorials/3d/global_illumination/using_sdfgi.html)). Screen-space effects cannot see off-screen or occluded geometry, and SSR only reflects opaque depth-writing geometry ([official environment documentation](https://docs.godotengine.org/en/stable/tutorials/3d/environment_and_post_processing.html)).

### Evidence, inference, and unknowns

**Directly supported:** compact block-scale sky/block fields, local AO, section render layers, and optional/scalable PBR effects are established implementation patterns. Minecraft explicitly separates gameplay emission from visual emission.

**Informed inference:** a shader-sampled RG light-page cache is the best eventual fit for VibeCraft because it decouples light updates from geometry and lets the GPU interpolate at visible fragments. None of the sources proves that Godot's optimal page layout or upload API is the one proposed here.

**Unknown until measured:** the best light-page backend in the pinned Godot version, practical page-update bandwidth, thin-wall interpolation leakage, useful local-light count, and whether optional SDFGI remains stable with continuous section replacement.

## Proposed design

### 1. Separate authoritative light semantics from presentation

Define two contracts:

- `GameplayLight`: deterministic sky and block levels used by server rules. It is derived only from authoritative block definitions, dimension policy, time-independent occlusion, and world state.
- `VisualLighting`: client-only shading derived from gameplay-compatible levels plus resource-pack colors, normals, PBR values, sun/moon direction, fog, exposure, and quality settings.

The server never accepts light levels, “safe to spawn” claims, or visibility from a client. A visual pack may make a torch blue or a cave brighter, but it cannot change `GameplayEmission`, `SkyOpacity`, or server light thresholds.

```csharp
public readonly record struct LightCell(byte SkyLevel, byte BlockLevel)
{
    // Both logical values are constrained to 0..15.
}

public interface IGameplayLightQuery
{
    LightCell GetCell(DimensionId dimension, BlockPosition position);
    bool MeetsThreshold(DimensionId dimension, BlockPosition position, LightRuleId rule);
}

public sealed record BlockLightProperties(
    byte GameplayEmission,
    byte BlockAttenuation,
    bool PropagatesSky,
    bool IsOpaqueToSky);
```

`BlockLightProperties` belongs to the authoritative block registry, not the resource pack. The exact packed storage is private; public APIs expose values, revisions, and bounded queries rather than mutable arrays.

### 2. Shared deterministic world-light solver

Use a Godot-independent `IWorldLightSolver` implementation in `VibeCraft.Core` on both server and client:

1. Capture immutable block/opacity/emission data for a section plus the needed boundary cells.
2. Seed block sources from `GameplayEmission` and sky sources from dimension-specific ingress metadata.
3. Propagate sky and block terms separately over six axis neighbors with integer rules and deterministic queue order.
4. Publish complete section light results with `ContentRevision`, `LightRevision`, `RegistryRevision`, and `Lifetime`.
5. Return changed bounds/sections so gameplay caches, mesh fallback data, and visual page uploads invalidate precisely.

```csharp
public sealed record LightSolveRequest(
    SectionKey Center,
    SectionRevision ContentRevision,
    RegistryRevision RegistryRevision,
    DimensionLightPolicy DimensionPolicy,
    SkyIngressSnapshot SkyIngress,
    IReadOnlyList<LightBoundarySnapshot> Boundaries);

public sealed record LightSolveResult(
    SectionKey Center,
    LightRevision Revision,
    OwnedLightCells Cells,
    IntBox ChangedBounds,
    ImmutableArray<SectionKey> BoundaryDependents);
```

This API states ownership and dependencies, not the final flood-fill optimization. Start with a bucketed multi-source queue over levels 15→1; add incremental removal/addition queues only after randomized equivalence tests against full recomputation.

On the server, `LightSolveRequest` enters a deterministic `GameplayLight` work queue
keyed by affected section/region and target content revision. Repeated edits coalesce
to the newest revision before solve; a stale solve cannot publish. Each `WorldTick`
reserves a bounded cell/work budget for light after accepted block changes and before
rules that consume the resulting revision. Large or churn-heavy regions promote to one
bounded regional recomputation rather than one flood fill per edit. Until a result
commits, light-dependent rules read the last committed light revision and diagnostics
report the debt; they never trust a client or invent prospective light.

### 3. Solve unbounded-height skylight explicitly

Each dimension declares one of:

- `NoSky`: Nether-like enclosed dimensions; no sky seeds.
- `DirectionalSky`: Overworld-like sky; sparse column metadata identifies the highest sky-occluding block or supplies section-entry masks.
- `UniformAmbient`: special dimensions with no geometry-derived sky exposure.

For `DirectionalSky`, the authoritative world index maintains a 16×16 `SkyOccluderY` map per horizontal section column (or an equivalent versioned top-occluder structure selected by `WORLD-01/06`). The server streams this near-world geometry metadata with canonical section data. The client still performs visual propagation locally, but it does not invent “open sky” above missing chunks.

Unknown/missing data is opaque to propagation until a valid boundary or sky-ingress revision arrives. Treating absent sections as air would flash caves bright and disclose unstreamed terrain.

### 4. Staged renderer integration

#### Stage A — correctness fallback

- Store logical 0–15 sky/block values at one sample per block.
- Sample the four neighboring exterior light cells at each terrain vertex and pack sky, block, and geometry AO into `MeshVertex.ShadingData`.
- A light revision dirties the affected mesh through `RENDER-02`; retain the old mesh until replacement.
- Use a small 16×16 brightness LUT or equivalent shader function to map sky/block levels to linear HDR intensity. Time of day changes uniforms/LUTs, not the propagated field.

This mirrors a proven Minecraft/Luanti-class baseline and is the fallback if light pages miss their budget.

#### Stage B — production near-field target

- Keep the same solver and logical values.
- Upload resident section light data to bounded shader-readable pages with a one-cell halo, as specified in `RENDER-05`.
- Terrain vertices carry world/local position and page binding, not baked dynamic light values. The terrain fragment shader samples the face-exterior light field and interpolates it across visible fragments.
- A light update increments `LightPageRevision` and uploads a page/subregion; it does **not** remesh geometry.
- Geometry AO remains a mesh attribute because it changes with occlusion topology, not with day/night or torch intensity.

The page backend is hidden behind:

```csharp
public interface IVisualLightPageCache
{
    LightPageLease? TryAcquire(RenderSectionKey key, LightRevision minimumRevision);
    void QueueUpload(LightSectionSnapshot snapshot, LightUploadPriority priority);
    void Release(RenderSectionKey key, uint lifetime);
}

public readonly record struct LightPageBinding(
    uint Slot,
    LightRevision Revision,
    Vector3I InteriorOrigin,
    byte InteriorSize);
```

No section owns a unique Godot material or Godot light texture. One renderer-owned atlas/page set is bound globally; section instances receive compact page coordinates. `ImageTexture3D.update()` replaces full texture data in Godot's high-level API, so partial-update feasibility must be proven using the pinned `RenderingDevice`/server path rather than assumed ([official `ImageTexture3D` API](https://docs.godotengine.org/en/stable/classes/class_imagetexture3d.html)).

#### Stage C — scalable enhanced shading

- Add one shadowed directional sun/moon path near the camera. Sky light remains broad indirect exposure; a shadow map represents directional occlusion.
- Apply fixed-shader PBR material maps, emissive glow, reflection fallback, and water terms from `RENDER-06`.
- Offer SSAO/SSIL/SSR as independently measurable Forward+ quality features. Screen-space failure fades to environment/probe/roughness fallback, never black or stale data.
- Select a bounded set of nearby decorative point lights for specular highlights. Start with 8 unshadowed and 0 shadowed point lights; test a high preset of up to 4 shadowed lights. Do not create one light object per emissive block.

#### Stage D — experimental GI

Prototype SDFGI in a moving, editing section scene. It may enrich the high preset, but the block light field remains the fallback and gameplay source of truth. VoxelGI is not the default because Godot documents it for small/medium scenes, whereas VibeCraft continuously streams a large world ([official VoxelGI documentation](https://docs.godotengine.org/en/stable/tutorials/3d/global_illumination/using_voxel_gi.html)).

### 5. Light-source and material boundary

A compiled block has two independent pieces:

```text
authoritative BlockLightProperties
  -> propagation and server gameplay

client MaterialEmission
  -> emissive pixels, bloom, optional selected local-light color
```

The client derives candidate visual local lights from visible section block data and compiled material metadata, then a camera-relative selector ranks them by projected influence, distance, and pack priority. Selection is capped and hysteretic. Dropping a candidate removes only its PBR highlight/shadow; the broad block-light field remains, so light does not visibly switch off.

V1 block propagation is monochrome with a pack/theme-selected warm block-light color. Colored propagation is a later prototype comparing source-class accumulation (as described by Bedrock), RGB channels, and a small fixed color palette. Do not freeze RGB fields into save/network formats now.

### 6. Scheduling and data flow

```text
authoritative block edit
  -> server block state + server gameplay-light invalidation
  -> streamed block/sky-ingress revision
  -> client immutable light solve job
  -> complete LightSolveResult
       -> Stage A: mesh light invalidation
       -> Stage B: bounded light-page upload
  -> terrain fragment: world light × AO × directional/PBR terms
```

- Light jobs share the bounded client worker scheduler but have their own quota; a torch edit near the player outranks far meshing and LoD work.
- Queue state is coalesced by section key and desired revision exactly like `RENDER-02`.
- Propagation may cross sections only through versioned boundary snapshots/messages. No job holds multiple mutable section locks while flood filling.
- Large edits switch to bounded regional/full recomputation instead of enqueueing one incremental job per block.
- Page commits occur on the renderer-owned publication path under byte/time caps. Old valid data remains resident until replacement is complete.

### 7. Failure and fallback behavior

- Missing neighbor or sky-ingress data: keep previous valid light; for first load use a conservative dimension ambient and mark the section pending. Never seed missing space as open sky.
- Solver exception or invariant failure: quarantine the revision, retain old light/mesh, emit a structured diagnostic, and retry only after new input or one rate-limited full recompute.
- Stale result: discard by exact content/registry/light/lifetime match; it cannot upload or dirty a newer mesh.
- Page allocation/upload exhaustion: retain or fall back to Stage A baked values for the section, shorten visual radius, and expose the event in telemetry.
- Unsupported Forward+ feature: disable that visual term and use the pack-declared material fallback. Joining a server never depends on SSR, GI, refraction, or shadow quality.
- Device loss: discard GPU pages/effects, preserve CPU light state, and repopulate by near-camera priority.
- Resource-pack reload: changes visual LUTs/materials immediately through a new render-registry revision but cannot alter authoritative light properties.

### 8. Required telemetry and provisional budgets

Track solve queue age, cells visited, full versus incremental solves, cross-section invalidations, page bytes/residency, upload time, stale results, time-to-visible, shadowed/unshadowed selected lights, and GPU timings for every optional term.

Initial 1080p/60 Hz gates on the agreed reference desktop:

- One torch add/remove in resident terrain reaches correct client light in **≤50 ms p95** and **≤100 ms p99**; a 15-block propagation fixture visits no cells outside its mathematically affected region plus boundary bookkeeping.
- Light solving and publication cause **no main-thread task over 1.0 ms p99**; worker time is reported separately.
- Stage B page sampling adds **≤0.75 ms GPU p95** versus Stage A in the canonical terrain scene.
- Light-page commits use **≤0.5 ms main-thread p95/frame** and **≤2 MiB/frame**; queues remain bounded during 1,000 distributed light edits/s.
- Lighting pages and metadata remain within the `RENDER-05` **64 MiB GPU** near-field cap; loss/reload does not leak RIDs or managed owners.
- Baseline sun/sky/block light plus shadows remains **≤2.5 ms GPU p95**; all enhanced effects together must fit a separately reported **4.0 ms GPU p95** incremental budget or be independently disabled.
- No resource pack changes any server light-threshold outcome in golden gameplay fixtures.

These are prototype gates, not shipping promises before reference hardware and view distance are selected.

## Greenlight criteria

- A library-only solver passes randomized add/remove/full-recompute equivalence tests across section faces, edges, corners, missing boundaries, and signed vertical coordinates.
- Server gameplay queries and client visual results use the same authoritative opacity/emission registry while allowing different visual material packs.
- Overworld-like, no-sky, and uniform-ambient dimension policies work without scanning an unbounded vertical column on each update.
- Stage A and Stage B produce equivalent 0–15 world-light intent in screenshot/diagnostic visualizations; Stage B meets memory, upload, leak, and GPU budgets.
- A lighting update cannot commit a stale section/page revision and does not require geometry remeshing in Stage B.
- Thin walls, corners, water absorption, section seams, unloaded neighbors, and sky-column changes have automated image/invariant fixtures.
- Every enhanced term has a deterministic fallback and can be disabled without changing simulation or disconnecting from multiplayer.
- Reference hardware, pinned Godot version/renderer, and selected quality profiles are recorded with benchmark results before status changes to Greenlit.

## Prototype or benchmark

Required: yes.

Smallest useful experiment:

1. Implement a pure C# 0–15 two-channel solver over 16³ sections plus versioned boundaries. Compare every incremental operation with a full recompute over 100,000 seeded edit sequences.
2. Add fixtures for open sky, overhangs, caves, a one-block wall, glass/water attenuation, two adjacent sections, missing-above data, a signed negative-Y section, and a 31³ worst-reach torch volume.
3. Render the same fixtures through Stage A vertex values and three Stage B page candidates: nearest block sampling, trilinear exterior-cell sampling, and prefiltered corner samples. Capture leak pixels and blinded quality scores.
4. Compare a 2D-array slice atlas, 3D atlas, and any supported partial-update `RenderingDevice` path. Measure actual VRAM, descriptor/binding count, update stalls, and C# marshalling—not just logical bytes.
5. Fly through 4,096 resident light sections while adding/removing 1,000 distributed sources/s for ten minutes. Record queue age, uploads, GPU time, GC, stale revisions, and residency plateau.
6. Add the sun shadow, 8 selected unshadowed local lights, SSR, SSIL, and SDFGI one at a time. Record incremental GPU time and screenshots; repeat with each feature unavailable.
7. Run server gameplay-light golden tests under two radically different visual resource packs. Every spawn/growth/redstone-related result must remain identical.

Success metrics: all greenlight criteria and budgets above. If page sampling fails, ship Stage A behind the same solver/interfaces and keep Stage B as a renderer-backend optimization. If SDFGI or local shadows fail, omit them; do not enlarge the canonical light field.

## Risks and open questions

- `SkyOccluderY` maintenance and disclosure semantics depend on `WORLD-01/06` and multiplayer interest policy.
- A one-block-resolution field cannot represent colored caustics or physically correct multi-bounce transport. Those are shading effects, not reasons to multiply canonical storage by 64³.
- Trilinear samples can leak across a one-block wall if sample coordinates/halos are wrong. This is a greenlight test, not a shader-tuning afterthought.
- Stage A remeshing can become unacceptable during large light edits; it exists as a fallback while Stage B is validated.
- Selected Godot lights may pop when the cap changes. Hysteresis and persistent broad block light reduce but do not eliminate this risk.
- Exact gameplay mechanics that consume light are not yet specified. The authoritative query interface must be retained even if v1 uses only mob spawning.
- Colored block light can multiply propagation state by source class. It remains explicitly deferred.

## Dependencies

- Requires: `FOUNDATION-00`, `ARCH-03`, `WORLD-01/02/05`, authoritative block registry from `GAME-01`, and revision/job contracts from `RENDER-01/02`.
- Requires for final implementation: `RENDER-05` page resolution/backend, `RENDER-06` material/light-source split, dimension/sky policy from `WORLD-06`, and target hardware/Godot renderer.
- Blocks: lighting implementation, gameplay light queries, shader vertex format finalization, material emission behavior, fog/GI tuning, and lighting-related resource-pack fields.

## Rejected or deferred alternatives

- **Treat all lighting as client-authoritative:** rejected; light-driven gameplay and anti-cheat must remain server-owned.
- **Let resource packs set gameplay emission/opacity:** rejected; visual packs must not alter multiplayer simulation.
- **Persist 64³ samples per block:** rejected quantitatively in `RENDER-05`.
- **One point light per torch/emissive block:** rejected; the documented cost and unbounded object count do not fit streamed terrain.
- **Bake all final shading into chunk vertices forever:** rejected as the production target because it couples every light update to meshing; retained as a simple fallback.
- **Make SDFGI/VoxelGI mandatory:** rejected; Godot renderer/platform limits and dynamic-world behavior make them optional visual enhancements.
- **Colored RGB propagation in v1:** deferred until the monochrome solver, visual source selection, and source-class/RGB comparison are measured.
- **Custom GPU GI before the first playable:** deferred; it is a replaceable enhancement, not a prerequisite for readable survival gameplay.
