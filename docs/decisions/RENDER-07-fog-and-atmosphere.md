# RENDER-07 Fog, atmosphere, and streaming-frontier concealment

Status: Proposed

Owner: Rendering research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Treat atmosphere as a layered, data-driven rendering system. The mandatory baseline is inexpensive depth/height fog whose color and density are coupled to the dimension sky, weather, camera medium, and the *currently safe rendered radius*. Forward+ desktop may add bounded local volumetric fog as a quality tier, but volumetrics are never responsible for hiding unloaded terrain.

One-sentence rationale: Conventional fog is portable, predictable, and long-ranged enough to conceal a moving voxel-streaming frontier, while Godot's volumetric fog is finite-range, Forward+-only, temporally unstable around fast changes, and too costly to make part of correctness.

## Context and constraints

- VibeCraft must hide missing or not-yet-meshed terrain without pretending that a configured view distance is always resident.
- Weather, day/night, caves, water/lava, and three dimensions need different atmosphere without separate renderer implementations.
- Fog is visual only. It cannot change visibility tests used by gameplay, networking interest, AI, collision, or anti-cheat.
- Transparent blocks, fluids, particles, sky, and far LoD need one coherent distance convention or visible seams result.
- Renderer capability varies. Godot supports depth/height fog in Compatibility, Mobile, and Forward+, while volumetric fog is supported only by Forward+ ([renderer feature table](https://docs.godotengine.org/en/stable/tutorials/rendering/renderers.html)).
- A teleport, rapid flight, server stall, or mesh backlog can temporarily shrink the radius that is safe to expose. Atmosphere must react smoothly without revealing void or snapping every frame.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| Fixed linear fog at configured render distance | Very cheap and simple | Reveals holes when residency lags; abrupt cutoff; ignores weather/height | Reject |
| Exponential depth plus height fog | Portable; long-ranged; sky coupling; predictable budget | Does not create local shafts or dense 3D banks | **Required baseline** |
| Godot global volumetric fog everywhere | Reacts to lights and supports local FogVolumes | Forward+-only; finite range; froxel cost; reprojection ghosting | Optional high tier only |
| Screen-space postprocess fog | Full visual control and easy frontier masks | Depth/transparency/sky integration becomes custom renderer work | Defer unless built-in baseline cannot meet art direction |
| Geometry cards for every fog bank | Portable and controllable | Sorting/overdraw and camera-intersection artifacts | Limited authored effects only |
| Fog as the only LoD seam solution | Can obscure defects quickly | Masks rather than solves data, cracks, or excessive draw cost | Reject |

## Evidence

### Godot

- Godot lists exponential depth fog, exponential height fog, sky-dependent fog color, sun scattering, and separate controls for how traditional and volumetric fog affect the sky ([official feature list](https://docs.godotengine.org/en/latest/about/list_of_features.html)). These are sufficient primitives for the baseline profile; using the stable-version API must still be pinned at implementation time.
- Godot's renderer matrix supports ordinary depth/height fog in all three renderer families, but volumetric fog only in Forward+ ([official renderer overview](https://docs.godotengine.org/en/stable/tutorials/rendering/renderers.html)). A pack or dimension therefore cannot require volumetrics if compatibility is a product goal.
- Godot documents that volumetric fog has a finite range and recommends ordinary fog as well when distant terrain must be hidden. It also documents temporal-reprojection ghost trails from moving lights/FogVolumes and suggests zeroing volumetric energy for brief dynamic lights ([official volumetric fog guide](https://docs.godotengine.org/en/stable/tutorials/3d/volumetric_fog.html)).
- Godot notes that billboarded fog quads can be cheaper and work with all rendering methods, but can sort incorrectly and look wrong when entered ([same official guide](https://docs.godotengine.org/en/stable/tutorials/3d/volumetric_fog.html#faking-volumetric-fog-using-quads)). They are an effect tool, not the world fog foundation.

### Voxel implementations

- Luanti exposes fog start as a fraction of visible distance and can make fog/sky colors depend on time of day and view direction ([official example configuration](https://github.com/luanti-org/luanti/blob/master/minetest.conf.example)). This is useful precedent for coupling fog to effective visibility and sky rather than treating it as an unrelated post effect.
- Distant Horizons documents multiple seam-hiding and overdraw experiments, including removal of a failed seamless-overdraw mode and cave-culling fixes ([official 2.1.0 release](https://gitlab.com/distant-horizons-team/distant-horizons/-/releases/2.1.0a)). The inference for VibeCraft is that fog can soften a transition but must not be the acceptance test for correct LoD topology.

### Inference and unknowns

The evidence establishes capabilities and failure modes, not final art values. The exact density curve, reaction time, sky matching, underwater behavior, and volumetric quality budget must be measured in the VibeCraft section streamer. Values below are contracts and starting constraints, not approved tuning constants.

## Proposed design

### 1. One authoritative atmosphere state per viewport

Simulation publishes descriptive state; the client renderer derives final parameters:

```csharp
public readonly record struct AtmosphereState(
    NamespacedId DimensionProfile,
    NamespacedId WeatherProfile,
    MediumKind CameraMedium,
    float WeatherIntensity,
    float DayFraction,
    long ServerTimeRevision);

public readonly record struct VisibilityState(
    float ConfiguredFarDistanceBlocks,
    float FullyResidentDistanceBlocks,
    float TransitionMarginBlocks,
    bool FarLodAvailable);
```

The server replicates dimension, weather, and coarse time state. It does **not** send shader parameters per frame. Client profile assets convert that state to fog/sky values and interpolate them at render rate. `FullyResidentDistanceBlocks` is local renderer truth from `RENDER-02/03`, not a server promise.

### 2. Baseline fog curve

- Use Godot Environment depth fog plus optional height fog on every renderer tier.
- Define an inner clear distance and an outer opacity target. The outer target tracks a conservative safe radius:

```text
safe_outer = min(configured_far, fully_resident + transition_margin)
fog_end    = max(min_playable_visibility, safe_outer)
fog_start  = fog_end * profile.start_fraction
```

- Start testing with `start_fraction` in `0.55..0.75`; choose by captures and playtests, not preference. The curve must reach an opaque-enough sky match before geometry can disappear.
- Low-pass filter decreases and increases separately. Shrink quickly enough to conceal a streaming failure; expand only after the larger shell is continuously resident for a hold interval. Quantize updates or use a damped transition so one section completing does not pulse the horizon.
- Clamp fog to a documented minimum playable visibility. If safe residency falls below that minimum, display an explicit loading/connection-recovery state rather than creating blind gameplay.
- Fog distance must use camera-relative distance, not global floating-point world coordinates.

### 3. Sky and color coupling

- Every dimension declares an `AtmosphereProfile`: sky provider, baseline extinction color, sun-scatter tint, height falloff, weather multipliers, medium overrides, and permitted quality features.
- Derive horizon fog color from the same time-interpolated sky state used for ambient lighting. Avoid separately authored keyframes that can expose a color band at the horizon.
- Use linear-light color interpolation and expose artistic controls over physically suggestive defaults. “Plausible” is useful; a physically complete atmosphere is not a v1 requirement.
- Enclosed dimensions may use constant/emissive skies and near-uniform fog. The End-like dimension may deliberately retain long visibility, but it still needs a missing-data frontier rule.
- Underground detection must not depend on one terrain sample. Start with ordinary depth/height fog; add local cave atmosphere only from explicit biome/volume data or a stable multi-sample exposure signal to avoid flicker under leaves and overhangs.

### 4. Weather and camera media

- Weather changes target profile parameters over a server-timestamped transition. Clients may interpolate visuals, but the server owns gameplay effects and the weather identity.
- Rain/snow particles have a bounded near-camera volume and separate density budget. They do not replace distance fog.
- Underwater/lava/powder-like media override extinction, color, visibility, post-processing, and audio as one camera-medium transition. Enter/exit uses hysteresis around the eye point so wave boundaries do not toggle every frame.
- A transparent fluid surface must use the same medium transition state and fog parameters on both sides. Validate refraction and depth writes in `RENDER-06`; ordinary opaque-terrain fog alone does not guarantee correct transparent composition.

### 5. Optional volumetric tier

- Enable global/local volumetric fog only in Forward+ and only after the baseline is already hiding the frontier.
- Cap volumetric length to the near/mid field. Let ordinary fog carry the horizon because Godot's volume is finite-range.
- Prefer static or slowly changing FogVolumes. Exclude muzzle flashes, rapidly moving emissive entities, and other brief dynamic lights from volumetric contribution when reprojection produces trails, following Godot's documented guidance.
- Packs may request named local fog effects but cannot select froxel resolution, allocate arbitrary 3D density textures, or force volumetrics. The renderer validates quotas and maps unsupported requests to baseline fog or a bounded billboard effect.
- Quality presets own froxel size/depth, temporal reprojection, shadow interaction, maximum local volumes, and maximum affected lights. Profiles own appearance within those caps.

### 6. LoD and streamer integration

- `RENDER-03` publishes the nearest distance beyond which coverage is not guaranteed. RENDER-07 consumes it; it never reaches into chunk queues or invents residency.
- Near terrain and far LoD overlap beneath fog, but each pipeline must still pass no-fog debug views for holes, duplicate faces, and transition cracks.
- Teleport sequence: hold/fade the old view or a loading overlay, move the render origin, establish a minimum resident shell, then reveal through the target atmosphere. Never expose an empty horizon while waiting for the first shell.
- If a server supplies no far-LoD capability, the same atmosphere profile shortens to the near full-detail radius. No special world behavior is required.

### 7. Asset and mod contract

An atmosphere definition is data, namespaced and validated by `ASSET-01/02`:

```json
{
  "format": 1,
  "id": "vibecraft:overworld_clear",
  "sky": "vibecraft:overworld_sky",
  "depth_fog": { "start_fraction": 0.65, "density": 0.003 },
  "height_fog": { "enabled": true, "falloff": 0.08 },
  "volumetric": { "allowed": true, "preset": "subtle" }
}
```

The illustrative schema is not frozen. Validation rejects NaN/infinite values, negative ranges, unknown resources, and values beyond engine caps. Server plugins can select registered profiles/weather and intensity; untrusted code cannot inject shaders or mutate `RenderingServer` state directly.

### 8. Failure and accessibility behavior

- Missing/invalid profile: use a built-in dimension-safe fallback and emit one diagnostic, not a black or transparent horizon.
- Streaming overload: shorten safe distance within the rate limit; below minimum visibility enter recovery UI.
- Unsupported volumetrics: preserve ordinary fog appearance and omit only local volumetric detail.
- Temporal artifacts: quality fallback disables reprojection-dependent local effects before reducing baseline correctness.
- Expose user controls for weather-particle density and optional volumetric effects. Do not expose a client setting that can remove server-relevant concealment assumptions; gameplay visibility must never rely on cosmetic fog for authority.

## Greenlight criteria

- With artificial generation/mesh delays, no unrendered frontier is visible during normal movement, sprinting, flight at the supported speed, or a tested teleport sequence.
- Ordinary fog works in every supported Godot renderer and costs no more than **0.3 ms GPU p95 at 1080p** on the corresponding reference hardware after warm-up.
- High-tier volumetrics add no more than **1.5 ms GPU p95 at 1080p** in the atmosphere benchmark and can be disabled without exposing unloaded terrain.
- Day/night, clear/rain, dimension, and camera-medium transitions produce no one-frame color flash and converge within their profile's bounded transition time.
- Automated capture tests cover horizon/sky continuity, underwater boundary, alpha-tested foliage, transparent fluids, emissives, teleport recovery, and missing far LoD.
- A no-fog diagnostic run independently passes terrain/LoD coverage tests; approval cannot be obtained by hiding cracks.

## Prototype or benchmark

Required: yes

Smallest useful experiment: Extend the `P3` streaming/rendering prototype with a flat horizon, mountain silhouette, water plane, alpha-tested foliage, one local fog volume, controlled section stalls, teleport, day/night, rain, and three quality presets. Capture identical camera paths in Compatibility and Forward+.

Success metrics:

- GPU timestamp delta for baseline and optional volumetric tiers at 1080p and one higher target resolution.
- Frame captures with safe radius oscillating by two sections and with a 500 ms mesh stall.
- No horizon color seam exceeding an agreed image-difference threshold outside intentionally visible sun scattering.
- No persistent volumetric trail from the standardized moving-light scene; otherwise that light class is excluded.
- Visibility remains above the gameplay minimum until the renderer explicitly enters recovery UI.

## Risks and open questions

- Supported platforms and renderer families are not decided; this controls whether Forward+ volumetrics are a product feature or only a desktop enhancement.
- The project needs target movement/flight/teleport speeds to size the residency margin and fog response.
- Godot's built-in atmosphere may not expose every custom-material integration point needed by voxel fluids and refraction; the prototype must inspect render order, depth, and fog application.
- Very low fog can feel like an artificial wall. Improving streaming or adding far LoD is preferable to permanently hiding a small view radius.
- Server-selected atmosphere can communicate gameplay state, but clients remain untrusted. The server must never infer what a player could see from their visual settings.

## Dependencies

- Requires: `ARCH-03`, `RENDER-02`, `RENDER-03`, `RENDER-06`, `ASSET-01`, `ASSET-02`
- Blocks: final dimension/weather presentation, far-LoD reveal policy, renderer quality presets

## Rejected or deferred alternatives

- Do not require volumetric fog for gameplay or frontier concealment.
- Do not run arbitrary pack-provided fog shaders in the base asset tier.
- Do not let clients generate unauthorized distant terrain merely to give fog something to cover.
- Defer physically complete aerial perspective, volumetric clouds, and custom compositor effects until the baseline and advanced-material pipelines are measured.
