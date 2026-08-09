# ASSET-04 Animation asset and runtime contract

Status: Proposed

## Decision

Recommended choice: Make `ASSET-03` the sole normative geometry/clip/animation-graph contract. Retain this brief only for texture-animation runtime semantics and the general separation of clip data, presentation selection, and authoritative gameplay timing; its earlier glTF subset, marker, missing-clip, and material clauses are superseded by `ASSET-03`.

One-sentence rationale: Keyframe samples, presentation state selection, texture animation, and authoritative gameplay timing are different problems, but two competing GLB profiles would be worse than one strict compiled contract.

## Context and constraints

- Packs need custom entity/block-entity models and keyframe animation.
- Textures may use frame sequences or generated material motion.
- Godot can import glTF, but runtime-loaded external packs should not depend on editor-only import behavior or deserialize arbitrary Godot resources.
- The server must validate gameplay actions independently from client frame position.
- Pack overrides need stable clip and state identifiers.

## Options considered

| Option | Strengths | Costs/risks | Fit |
| --- | --- | --- | --- |
| Store Godot scenes/AnimationPlayer resources | Native playback/editor tooling | Engine-version coupling; unsafe/unfriendly external pack boundary | Reject as pack format |
| Invent complete model/skeleton/keyframe format | Exact control | Large tooling/import/validation burden | Reject for v1 |
| Load glTF directly at runtime | Standard tooling and semantics | Parse/import cost, extension variability, runtime error surface | Good authoring input, not final cache |
| Validate/compile glTF into a native runtime cache | Standard authoring plus controlled runtime | Requires pack compiler | Recommended |

## Evidence

The Khronos glTF 2.0 specification is runtime-neutral and defines nodes, skins, PBR materials, morph targets, and animation channels/samplers. Animation targets translation, rotation, scale, or morph weights and supports step, linear, and cubic-spline interpolation ([glTF 2.0 specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html)). This covers the model-space keyframe problem without defining game state machines or authoritative actions.

Godot imports glTF scenes and can import animations as an `AnimationLibrary` ([Godot scene-import documentation](https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/importing_3d_scenes.html)). External packs still need a pinned validated pipeline because editor imports, arbitrary Godot scene content, and engine-version metadata are inappropriate as a portable untrusted contract.

## Proposed design

### Three separate layers

1. **Clip data**: authored skeleton/node transforms and optional morph weights over seconds.
2. **Presentation graph**: selects/blends clips from semantic inputs such as grounded, speed, stance, action, held-item class, and damage reaction.
3. **Gameplay timeline**: server-authoritative attack windows, cooldowns, item-use duration, movement constraints, and emitted actions.

The presentation graph may anticipate an action locally and reconcile to server events, but clip frames never determine damage, reach, invulnerability, item consumption, or block mutation.

### Import pipeline

This first-wave subsection is superseded by `ASSET-03`. V1 accepts GLB only, one embedded BIN, no URIs/images, material names as slot labels only, `STEP`/`LINEAR` TRS animation initially, and no morphs/CUBICSPLINE until capability-gated fixtures pass. Original package GLB is compiled into owned plain tables and is never handed to broad Godot scene generation.

Accepted source for v1:

- `.glb` only; textual `.gltf` and every URI are rejected;
- glTF core 2.0 plus an allowlist of explicitly supported extensions;
- node hierarchy, mesh primitives, skinning, named clips, and step/linear animation required;
- cubic splines, morph targets, and material extensions are rejected in the initial profile and require explicit future capability gates rather than silent approximation.

The pack compiler validates sizes/counts, hierarchy cycles, finite values, normalized rotations, skin/joint limits, key ordering, clip duration, texture references, and path containment. It converts coordinates/material bindings and emits a cache keyed by source hashes, importer version, target renderer profile, and asset-contract version.

Runtime representation:

```text
Rig {
  joints[] { id, parent, bind_transform }
}

Clip {
  id
  duration
  loop_mode
  tracks[] { target_id, property, interpolation, times[], values[] }
  compiled_markers[] { time, host_cosmetic_operation_id } // authored in ASSET-03 graph
}
```

Track and marker counts are bounded. Runtime IDs may be compact, but source/persistent references use namespaced strings. Markers may trigger particles/sounds only; gameplay events originate from authoritative semantic actions.

### Presentation state graph

Use a constrained declarative graph with named states, transitions, blend duration, priority, and conditions over an allowlisted presentation parameter set. No general expressions, loops, filesystem access, or scripts in v1.

Core entity graph inputs:

- horizontal/vertical speed;
- grounded/swimming/flying/stance;
- locomotion direction relative to facing;
- semantic action ID plus phase/progress from authoritative/predicted state;
- damage/death flag;
- equipped visual classes.

Required clip references fail compilation in v1. A fallback chain is deferred until `ASSET-03` defines a concrete schema; the renderer never invents one implicitly.

### Texture animation

Define a small independent descriptor:

```text
TextureAnimation {
  source
  frame_width
  frame_height
  frames[] { index, duration_ms }
  interpolation: step | linear
  loop: repeat | once | ping_pong
  phase: synchronized | per_instance | deterministic_random
}
```

Frames are packed/imported into an array/atlas appropriate to the renderer. The shader selects/blends frames without rebuilding chunk meshes. Time is presentation time unless gameplay explicitly publishes a semantic phase.

### Block entities and articulated blocks

Doors, chests, and other articulated blocks use named parts and clips but are instantiated/batched according to renderer policy. A block's logical open/progress state is server state; the client animates between confirmed/predicted values. Ordinary animated textures remain batched terrain materials.

## Greenlight criteria

- The same source GLB compiles deterministically on all supported build platforms.
- Runtime loading performs no external URI/network access and instantiates no arbitrary Godot scene/script.
- Unsupported glTF extensions fail with explicit diagnostics.
- Gameplay results are identical when clips, playback speed, or presentation graph are changed.
- Texture animations do not trigger section remeshing and remain batchable.
- Missing required clips fail compilation with an asset-origin diagnostic; any future fallback is schema-defined by `ASSET-03`.

## Prototype or benchmark

Required: yes.

Use `ASSET-03`'s model/clip importer prototype. This brief adds one animated texture, frame-timing catch-up/drop tests, material-bank integration, and missing-required-clip diagnostics. Morph/CUBICSPLINE remain rejection fixtures until separately greenlit.

Measure import time, cache size, per-instance animation CPU, skinning/draw cost, and 1,000 simple animated entities. Verify that changing attack clip timing does not change server hit timing.

## Risks and open questions

- Runtime skinning strategy and crowd batching depend on actual entity counts.
- `ASSET-03` ignores GLB PBR values and binds unique slot labels to VibeCraft materials; this brief must not reintroduce approximation behavior.
- User-authored state graphs can become combinatorially complex even without scripting; visualization/debug tooling is important later.

## Dependencies

- Requires: `ASSET-01`, `ASSET-02`, `ASSET-03`, `RENDER-06`, `NET-01`.
- Blocks: custom animated entities and block entities.

## Rejected or deferred alternatives

- Godot `.tscn`/`.tres` as the public pack contract: rejected.
- Gameplay hit events embedded in client animation markers: rejected.
- Arbitrary scripted animation graphs: deferred to the sandboxed mod API, not the asset format.
