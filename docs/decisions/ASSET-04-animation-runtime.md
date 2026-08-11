# ASSET-04 Animation asset and runtime contract

Status: Proposed

## Decision

Recommended choice: Make `ASSET-03` the sole owner of the future model/rig and
presentation contract. Retain this brief for texture-animation runtime semantics and
the separation of profile clip selection, presentation state, and authoritative
gameplay timing; its earlier glTF subset, marker, missing-clip, and material clauses
are historical research, not a selected format.

One-sentence rationale: Presentation state selection, texture animation, and
authoritative gameplay timing are different problems, and neither should force a
premature public model-source format.

### Owner decision — 2026-08-10

V1 model replacements reuse built-in `RigProfile` clips from `ASSET-03`; user-authored
skeletal clips/graphs are deferred. GLB/glTF remains possible offline research input,
not the native pack contract.

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
| Define a VibeCraft voxel model/rig source format now | Exact control | Large tooling/import/validation burden | Bounded format spike required before selection |
| Load glTF directly at runtime | Standard tooling and semantics | Parse/import cost, extension variability, runtime error surface | Historical authoring candidate only |
| Validate/compile glTF into a native runtime cache | Standard authoring plus controlled runtime | Requires pack compiler | Candidate only; no native source format is selected |

## Evidence

The Khronos glTF 2.0 specification is runtime-neutral and defines nodes, skins, PBR materials, morph targets, and animation channels/samplers. Animation targets translation, rotation, scale, or morph weights and supports step, linear, and cubic-spline interpolation ([glTF 2.0 specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html)). This covers the model-space keyframe problem without defining game state machines or authoritative actions.

Godot imports glTF scenes and can import animations as an `AnimationLibrary` ([Godot scene-import documentation](https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/importing_3d_scenes.html)). External packs still need a pinned validated pipeline because editor imports, arbitrary Godot scene content, and engine-version metadata are inappropriate as a portable untrusted contract.

## Proposed design

### Three separate layers

1. **Clip data**: authored skeleton/node transforms and optional morph weights over seconds.
2. **Presentation graph**: selects/blends clips from semantic inputs such as grounded, speed, stance, action, held-item class, and damage reaction.
3. **Gameplay timeline**: server-authoritative attack windows, cooldowns, item-use duration, movement constraints, and emitted actions.

The presentation graph may anticipate an action locally and reconcile to server events, but clip frames never determine damage, reach, invulnerability, item consumption, or block mutation.

### Historical glTF candidate: import pipeline

This first-wave subsection is historical research only. `ASSET-03` now requires a
format/tooling spike before any model source is accepted. No original package model
payload may be handed to broad Godot scene generation.

No GLB/glTF source form is accepted as a public pack requirement before the format
spike selects a native contract. Any optional offline importer must have a strict
allowlist, bounded compilation, explicit diagnostics, and no broad Godot import.

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

- The selected model/rig source compiles deterministically on all supported build
  platforms.
- Runtime loading performs no external URI/network access and instantiates no arbitrary Godot scene/script.
- Unsupported model-source features fail with explicit diagnostics.
- Gameplay results are identical when clips, playback speed, or presentation graph are changed.
- Texture animations do not trigger section remeshing and remain batchable.
- Missing required clips fail compilation with an asset-origin diagnostic; any future fallback is schema-defined by `ASSET-03`.

## Prototype or benchmark

Required: yes.

Use `ASSET-03`'s model/clip importer prototype. This brief adds one animated texture, frame-timing catch-up/drop tests, material-bank integration, and missing-required-clip diagnostics. Morph/CUBICSPLINE remain rejection fixtures until separately greenlit.

Measure import time, cache size, per-instance animation CPU, skinning/draw cost, and 1,000 simple animated entities. Verify that changing attack clip timing does not change server hit timing.

## Risks and open questions

- Runtime skinning strategy and crowd batching depend on actual entity counts.
- `ASSET-03` owns profile/model material-slot mapping; this brief must not reintroduce
  an implicit shader or source-format approximation path.
- User-authored state graphs can become combinatorially complex even without scripting; visualization/debug tooling is important later.

## Dependencies

- Requires: `ASSET-01`, `ASSET-02`, `ASSET-03`, `RENDER-06`, `NET-01`.
- Blocks: custom animated entities and block entities.

## Rejected or deferred alternatives

- Godot `.tscn`/`.tres` as the public pack contract: rejected.
- Gameplay hit events embedded in client animation markers: rejected.
- Arbitrary scripted animation graphs: deferred to the sandboxed mod API, not the asset format.
