# ASSET-03 Model, material, block-visual, and animation contract

Status: Proposed

Owner: Asset-contract research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Use small, strict VibeCraft JSON descriptors for visual meaning and
references; use native cuboid elements for chunk-baked block geometry; and define
first-party `RigProfile` contracts for replaceable animated models. The engine-neutral
native model/rig source format is intentionally unselected until a focused format and
tooling spike; every accepted source format compiles into engine-private render tables.

One-sentence rationale: Voxel culling, semantic rigs, material slots, playback policy,
gameplay authority, and safe state-machine expressions are VibeCraft contracts; the
source container must serve those contracts rather than define them.

### Owner decision — 2026-08-10

GLB/glTF is not the native public-pack model format. The retained GLB material below is
research evidence for a possible offline authoring importer only and must not be
implemented as the pack contract. Custom model replacement reuses VibeCraft rig
profiles—not Minecraft bone layouts—and user-authored skeletal clips are deferred.

Resource packs never define authoritative collision, selection/reach shapes, light values used by simulation, block/entity state, movement, hit timing, inventory events, or root motion. Those belong to server-visible data/registries. A model or animation can react to replicated presentation facts; it cannot create those facts.

## Context and constraints

- Static block geometry must be suitable for section meshing and batching; a Godot node or draw call per block is outside the accepted client architecture.
- Entities and stateful/animated block entities need arbitrary geometry, bones, sockets,
  reusable first-party clips, and deterministic playback state.
- Materials need stable texture slots so a texture/material overlay does not require
  editing a model payload.
- The visual contract should support 64×64 and larger textures without assuming “one pixel equals one light sample.” GPU fragment shading is separate from world-light storage.
- Transparent, emissive, reflective, and refractive goals depend on `RENDER-06`. The asset schema may expose only capabilities with implemented, tested renderer semantics.
- The client is Godot/C#, but packs must not contain Godot scenes/resources or rely on editor import metadata.
- All references resolve through the immutable `ASSET-02` snapshot and must be validated before Godot resource publication.
- Minecraft import is an offline translation problem. The native format is not required to preserve undocumented bugs or third-party pack extensions.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| Minecraft Java/Bedrock model JSON natively | Familiar voxel tooling; cuboid/blockstate precedent | Two different evolving formats; weak material model; edition quirks and extension debt | Converter input only |
| Godot `.tscn/.tres`, `AnimationPlayer`, and `AnimationTree` as pack format | Direct engine integration and rich features | Engine-version coupling; project-path replacement; arbitrary node/property/method tracks; poor headless validation/isolation | Rejected as portable/untrusted contract |
| glTF/GLB for every semantic | Industry tooling; meshes, skins, morphs, PBR, keyframes | Does not meet the desired native voxel/rig contract; import/validation surface is larger than wanted | Offline authoring candidate only |
| Custom JSON geometry and animation for everything | Exact voxel semantics and easy parsing | Rebuilds DCC ecosystem, skinning, morph, tangent, interpolation, and exporter tooling | Use only for compact cuboid blocks/controllers |
| **VibeCraft descriptors + cuboids + rig profiles + format spike** | Explicit voxel/application semantics and reusable built-in animation | Requires a focused source-format/tooling decision | **Owner direction** |

## Evidence

### Minecraft

**Bedrock Edition official documentation and samples.** Bedrock geometry uses JSON bones, cube lists, pivots, locators, and unique animation-facing bone names; resource-pack entity definitions separately bind geometry, textures, materials, and render controllers ([geometry reference](https://learn.microsoft.com/en-us/minecraft/creator/reference/content/visualreference/geometry.v1.12.0), [entity modeling and animation](https://learn.microsoft.com/en-us/minecraft/creator/documents/entitymodelingandanimation), [official samples](https://github.com/Mojang/bedrock-samples)). Its animation controllers separately decide which raw animations play and when, using state transitions and expressions ([animation-controller reference](https://learn.microsoft.com/en-us/minecraft/creator/documents/animations/animationcontroller), [animations versus controllers](https://learn.microsoft.com/en-us/minecraft/creator/documents/animationsvscontrollers)).

This separation—geometry, bindings, clips, controller—is sound. The caution is Bedrock's broad Molang/render-controller semantics and its behavior/resource split. VibeCraft should use typed, allowlisted presentation inputs and keep gameplay collision/state on the authoritative side.

Official custom-block documentation shows geometry and material instances cooperating with behavior-side block state, while collision and selection boxes are separate components ([custom block tutorial](https://learn.microsoft.com/en-us/minecraft/creator/documents/addcustomdieblock), [multi-block example](https://learn.microsoft.com/en-us/minecraft/creator/documents/multi-blocks)). VibeCraft adopts the separation but makes visual packs incapable of changing server collision.

**Java Edition official release notes.** Java resource-pack model semantics continue to change with pack versions—for example, snapshot 25w16a removed the old 22.5-degree granularity restriction while retaining bounded element rotation ([25w16a notes](https://feedback.minecraft.net/hc/en-us/articles/35891577995277-Minecraft-Java-Edition-Snapshot-25w16a)). This is another reason for version-pinned conversion rather than direct runtime interpretation.

### Luanti

Luanti supports GLB/glTF media and multiple animation tracks, recommends GLB, and explicitly documents a supported subset: at the cited version, materials, morph animation, cubic-spline interpolation, embedded images, and external URI resources have limitations or are unsupported. It warns creators not to depend on unsupported features being ignored because later support could change rendering ([Luanti mod/media API](https://api.luanti.org/mods/)).

The lesson is that “supports glTF” is not a sufficient contract. VibeCraft needs a versioned allowlist, rejection of unknown required features, conformance fixtures, and an explicit material binding layer.

### Terasology

Terasology separates block definitions, block tile textures, shape assets, and behavior prefabs. Its shape format splits center and six side meshes, marks a side as full/partial for neighbor occlusion, and can contain AABB colliders ([block definition](https://metaterasology.github.io/docs/developing/blocks/blockDefinition.html), [block shapes](https://metaterasology.github.io/docs/developing/blocks/blockShapes.html)). It also maps block states/placement to shapes and rotations through block definitions ([block attributes](https://metaterasology.github.io/docs/developing/blocks/blockAttributes.html)).

The useful renderer lesson is that voxel culling metadata is not recoverable from an arbitrary mesh cheaply or robustly at runtime. VibeCraft keeps conservative culling declarations with visual geometry, but unlike the old Terasology example it does not let a client resource pack author authoritative collision.

### glTF and Godot

Khronos glTF 2.0 defines a right-handed, +Y-up, meter-based format with meshes, primitives/material bindings, skins, morph targets, and keyframe channels for node translation/rotation/scale and morph weights. It supports `LINEAR`, `STEP`, and `CUBICSPLINE` interpolation, but explicitly does not define which animation plays, looping, transition order, or reset behavior. Object names are application labels and are not guaranteed unique; URIs may be embedded data or relative resources and implementations may support additional schemes ([glTF 2.0 specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html)).

Khronos maintains a validator that checks JSON/GLB structure, references, accessors, invalid floats/quaternions, animation inputs/outputs, images, and supported extensions ([glTF Validator](https://github.com/KhronosGroup/glTF-Validator)). VibeCraft should run that validator in content-tool CI and maintain an independent bounded runtime importer; conformance does not supply application resource limits.

Godot imports glTF and can import animation-only libraries, and `AnimationTree` controls playback from animations held by `AnimationPlayer` ([scene importer](https://docs.godotengine.org/en/stable/classes/class_resourceimporterscene.html), [animation-library import](https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/importing_3d_scenes/import_configuration.html), [AnimationTree](https://docs.godotengine.org/en/stable/tutorials/animation/animation_tree.html)). These are useful backend facilities. Their serialized scene/resource graphs are not the public pack ABI; VibeCraft compiles its own descriptors into whichever Godot resources the supported engine version needs.

### Sourced conclusions versus inference

Directly sourced:

- Minecraft Bedrock and Godot separate stored clips from playback/controller behavior.
- glTF core animation targets only transforms/morph weights and does not define playback state.
- glTF names need not be unique and its URI/extension surface is broader than VibeCraft requires.
- Terasology carries voxel-side visibility/culling information alongside block shapes.
- Luanti's documented glTF subset differs from the full specification and changes over time.

VibeCraft engineering inference:

- Static blocks need a precompiled `BlockRenderTemplate`; accepting arbitrary scenes per block would violate the chosen section renderer.
- Material names inside GLB should be treated as replaceable slot labels, not authoritative shader definitions.
- A constrained animation graph with typed presentation parameters is safer, more portable, and more testable than arbitrary expressions or Godot property/method tracks.
- Renderer and authoritative collision shapes must have separate package classes/contracts even when an authoring tool previews them together.

## Proposed design

### Coordinate, naming, and reference conventions

VibeCraft world/model convention:

- right-handed coordinates;
- +X east, +Y up, +Z south;
- entity/model forward is -Z (north);
- one VibeCraft model unit equals one full block edge;
- block-local bounds are normally `[0, 1]` on each axis, with block center `(0.5, 0.5, 0.5)`;
- UV origin and tangent/color-space handling are defined by each texture/material schema, not inferred from authoring software.

The selected source-format adapter must document any authoring-basis conversion and
verify it with asymmetric labeled-axis fixtures. Packs never compensate by applying
ad-hoc per-bone rotations.

Asset references use typed `namespace:path` names from `ASSET-01`. JSON enum/slot,
rig-joint, clip, and controller names use lowercase ASCII `[a-z0-9_]+`, 1–64
characters. Every source format accepted by the future spike must reject ambiguous
referenced names before compilation.

### Asset graph

```text
Block registry state (authoritative data; GAME-01)
    -> block_visual
         -> one or more model descriptors
              -> cuboid elements OR format-spike model payload
              -> material slot bindings -> material -> textures

Entity/block-entity presentation type
    -> entity_visual
         -> model descriptor -> geometry/materials/sockets/RigProfile
         -> presentation graph -> profile clip IDs + typed presentation inputs
```

Descriptors never use host-relative paths. Every cross-file reference is an `AssetName` in a typed field and resolves through one staged `ASSET-02` snapshot. The compiler detects missing references and graph cycles and records the effective package/path origin of every edge.

### Rig profiles and reusable built-in animation

VibeCraft does not promise Minecraft-compatible skeletons. It publishes named,
versioned first-party rig profiles such as `vibecraft:biped/1` and
`vibecraft:quadruped/1`. A profile declares required joint IDs, parent hierarchy,
bind-pose/orientation conventions, named sockets, and the built-in clip catalog that
the client may apply.

```text
RigProfile
  id + revision
  required_joints[] { id, parent, bind-pose convention }
  optional_sockets[]
  built_in_clips[] { id, semantic meaning, loop/one-shot policy }
```

An entity or animated block-entity model declares one profile and provides every
required joint. A compatible replacement can therefore reuse VibeCraft's idle, walk,
hurt, open, and similar profile clips without needing any Minecraft bone names. A
model that does not meet its declared profile fails validation; it does not silently
retarget or invent joints. User-authored skeletal clips, arbitrary retargeting, and
custom animation graphs are deferred until two first-party profiles prove what the
public contract actually needs.

### Model descriptor

A `.model.json` is a renderable geometry plus named material slots, sockets, and clip aliases. It has one of two geometry forms.

#### Cuboid model

```json
{
  "schema": "vibecraft.model/1",
  "geometry": {
    "kind": "cuboids",
    "elements": [
      {
        "name": "body",
        "from": [0.0, 0.0, 0.0],
        "to": [1.0, 1.0, 1.0],
        "rotation": {
          "axis": "y",
          "degrees": 0.0,
          "origin": [0.5, 0.5, 0.5]
        },
        "faces": {
          "north": {
            "material_slot": "side",
            "uv": [0.0, 0.0, 1.0, 1.0],
            "uv_rotation": 0,
            "cull_against": "north"
          },
          "south": {
            "material_slot": "side",
            "uv": [0.0, 0.0, 1.0, 1.0],
            "uv_rotation": 0,
            "cull_against": "south"
          },
          "up": {
            "material_slot": "top",
            "uv": [0.0, 0.0, 1.0, 1.0],
            "uv_rotation": 0,
            "cull_against": "up"
          }
        }
      }
    ]
  },
  "materials": {
    "side": "example:block/stone_side",
    "top": "example:block/stone_top"
  },
  "block_template": {
    "mode": "chunk_static"
  }
}
```

Rules:

- `from` is componentwise less than or equal to `to`; zero-area faces are omitted.
- Element rotation is a finite degree value and is applied around its local origin. V1 authoring tools may expose arbitrary degrees, but a face can declare `cull_against` only when the transformed face lies exactly on the named block boundary and fills the complete boundary square within the compiler epsilon.
- `uv` uses normalized texture coordinates. `uv_rotation` is `0`, `90`, `180`, or `270` clockwise as viewed from outside the face.
- Omitted faces are not emitted. Unknown material slots are compile errors.
- `cull_against` is an optimization assertion validated by geometry, not author authority. Invalid claims reject the model rather than creating holes.
- `chunk_static` geometry, after block-visual rotation, must remain inside `[0,1]^3`, have no skin/morph/clip, and compile to immutable vertices grouped by renderer layer/material-atlas class.
- Cuboid source is preferred for ordinary cubes, slabs, stairs, crossed plants, fences, panes, dust, and similar models because it preserves explicit face/culling structure and converts cleanly from Java-style models.

#### Historical GLB-backed candidate — not a public-pack contract

The following GLB profile is retained only as research for a possible offline authoring
converter. It is **not** VibeCraft v1 format selection, is not required of pack
authors, and must not be used to implement the runtime loader until a future owner
decision explicitly reopens it.

```json
{
  "schema": "vibecraft.model/1",
  "geometry": {
    "kind": "gltf",
    "asset": "example:entity/boar",
    "scene": 0
  },
  "materials": {
    "body": "example:entity/boar",
    "eyes": "example:entity/boar_eyes"
  },
  "sockets": {
    "head": "head_socket",
    "right_hand": "right_hand_socket"
  },
  "clips": {
    "idle": { "animation": "idle" },
    "walk": { "animation": "walk" },
    "hurt": { "animation": "hurt" }
  }
}
```

Former proposed GLB profile:

- GLB 2.0 only, exactly one embedded BIN payload; textual `.gltf`, external/data/file/http URIs, embedded/external images, cameras, lights, audio, scripts, and unknown required extensions are rejected.
- Initial supported core data: triangle primitives, indexed or non-indexed positions, normals, tangents, `TEXCOORD_0`, `COLOR_0`, one skin with up to four normalized influences per vertex, and node TRS animation with `STEP` or `LINEAR`. `CUBICSPLINE`, morph targets/weights, sparse accessors, matrices outside the accepted TRS profile, and each optional extension are rejected until their own conformance/budget fixtures pass and the asset API capability is revised.
- Material objects are allowed only as uniquely named **slot labels**. Their shaders/textures/factors are ignored by the native contract; images/textures are disallowed. Every rendered primitive names a slot that the model descriptor binds to a VibeCraft material. The importer rejects unknown GLB chunks, unsupported optional extensions/features, and ambiguous referenced names rather than passing them through.
- Node matrices are decomposed at import or rejected when they cannot be represented safely; an animated node must use TRS, matching glTF's animation targets.
- The initial required-extension allowlist is empty. Each later glTF extension needs a named VibeCraft capability, importer/backend fixtures, limits, and fallback policy. An extension listed in `extensionsRequired` without that capability rejects the asset; it is never silently ignored.
- The compiler runs Khronos validation in authoring/CI tooling and independently checks the VibeCraft profile and limits. Runtime does not shell out to the validator.
- A GLB used by an ordinary block visual must be rigid, unanimated, unskinned, unmorphed, fully within block bounds, and is baked into the section mesh with **no neighbor-face culling** in v1. Ordinary static blocks should use cuboids when culling matters. Animated, skinned, morphed, or oversized GLB geometry is permitted only for a sparse entity/block-entity presentation and uses `dynamic_instance`; it cannot be selected by an ordinary dense block visual.

`dynamic_instance` means one presentation instance for an entity or server-recognized sparse block entity, never one for every ordinary terrain block. This is a schema rule, not a content-author performance hint.

### Material descriptor

V1 defines a batching-aware material rather than exposing Godot shaders:

```json
{
  "schema": "vibecraft.material/1",
  "surface": "cutout",
  "alpha_cutoff": 0.5,
  "textures": {
    "base_color": "example:block/oak_leaves",
    "normal": "example:block/oak_leaves_normal",
    "orm": "example:block/oak_leaves_orm",
    "emissive": "example:block/oak_leaves_emissive"
  },
  "factors": {
    "base_color": [1.0, 1.0, 1.0, 1.0],
    "metallic": 0.0,
    "roughness": 0.9,
    "emissive_rgb": [0.0, 0.0, 0.0]
  },
  "sampler": {
    "filter": "nearest_mipmap",
    "wrap": "repeat"
  },
  "tint_input": "foliage"
}
```

Rules:

- `surface` is `opaque`, `cutout`, or `blend` in baseline v1. It selects a renderer layer; it is not arbitrary shader source.
- Base-color/emissive textures are interpreted as sRGB inputs; normal and occlusion/roughness/metallic (`orm`: R/O, G/R, B/M) are linear data. The asset compiler owns platform conversion/mips.
- Missing optional maps use specified neutral constants. Referenced maps must have compatible dimensions/UV policy for atlas/array compilation or be placed in an explicit non-atlased material class.
- `tint_input` is `none` or an engine-defined presentation tint channel such as foliage/water. Resource packs do not calculate biome/gameplay state.
- Emissive material appearance does not define authoritative block light. Gameplay light emission is a server-visible block property.
- Transmission, index of refraction, volume, reflection probes, custom blend modes, and shader graphs are absent until `RENDER-06` defines a capability and batching/fallback contract. A pack cannot smuggle them through GLB materials or Godot shader resources.
- Animated texture sequences/procedural textures use dedicated descriptor kinds from `ASSET-04`/`ASSET-05` and ultimately bind as a texture asset; they do not add code to this material.

### Block visual selection

A `.block-visual.json` maps authoritative registry states from `GAME-01` to render models. It does not define valid block state or behavior.

```json
{
  "schema": "vibecraft.block_visual/1",
  "block": "example:lamp",
  "cases": [
    {
      "when": { "facing": "north", "lit": true },
      "apply": [
        { "model": "example:block/lamp_on", "rotation_y": 0, "weight": 1 }
      ]
    },
    {
      "when": { "facing": ["east", "west"], "lit": true },
      "apply": [
        { "model": "example:block/lamp_on", "rotation_y": 90, "weight": 1 }
      ]
    }
  ],
  "fallback": [
    { "model": "example:block/lamp_off", "rotation_y": 0, "weight": 1 }
  ],
  "parts": [
    {
      "when": { "powered": true },
      "apply": [
        { "model": "example:block/power_indicator", "rotation_y": 0, "weight": 1 }
      ]
    }
  ]
}
```

`when` is an AND of exact property matches; an array means OR among listed values for that property. No expressions, arbitrary queries, regex, filesystem access, or script callbacks are allowed. The compiler obtains the finite valid-state set from the registry and checks:

- every property/value exists and has the correct scalar type;
- every valid state matches exactly one `cases` entry or the fallback;
- matching more than one case is an ambiguity error, so list order is not semantics;
- all matching `parts` are added in source order after the base case;
- X/Y/Z placement rotations are multiples of 90 degrees in v1;
- weighted alternatives have positive integer weights and choose deterministically from `(world visual seed, block position, full block state, block-visual asset revision)`, so remeshing does not flicker and no server gameplay RNG is consumed.

The compiler flattens every valid state to a `BlockRenderTemplate` before chunk meshing. Runtime meshing does not parse conditions or traverse JSON.

### Entity and animated block-entity visual

A future `.entity-visual.json` binds a profile-compatible model and presentation graph
to one presentation type. For the first profile slice, packs may replace compatible
geometry/material bindings but select only the profile's built-in clip IDs; they do not
ship arbitrary skeletal clips or a custom graph.

```json
{
  "schema": "vibecraft.entity_visual/1",
  "presentation_type": "example:boar",
  "model": "example:entity/boar",
  "rig_profile": "vibecraft:quadruped/1",
  "clip_set": "built_in",
  "parameters": {
    "speed": "locomotion.horizontal_speed",
    "grounded": "locomotion.grounded",
    "hurt": "events.hurt",
    "dead": "state.dead"
  }
}
```

The engine publishes a versioned catalog of typed read-only presentation inputs. Unknown source paths reject the descriptor. Inputs are snapshots/edge-triggered event IDs derived from authoritative replicated state or local cosmetic state; they are not reflective access to entity components, Godot nodes, arbitrary properties, mods, or network messages.

Animated chests/doors that are ordinary authoritative blocks use a sparse block-entity presentation instance driven by replicated open/progress state. Dense, purely periodic visual effects should remain chunk/material animation where possible.

### Presentation graph — deferred public authoring

The selected future model format may store clips, but public clip/graph authoring is
deferred. The retained graph below is a bounded future design sketch for first-party
use; it is not a v1 pack requirement. V1 maps replicated presentation facts to the
fixed clip catalog of the declared `RigProfile`.

```json
{
  "schema": "vibecraft.animation_graph/1",
  "model": "example:entity/boar",
  "initial": "idle",
  "parameter_types": {
    "speed": "float",
    "grounded": "bool",
    "hurt": "trigger",
    "dead": "bool"
  },
  "states": {
    "idle": { "clip": "idle", "loop": true, "speed": 1.0 },
    "walk": { "clip": "walk", "loop": true, "speed": 1.0 },
    "hurt": { "clip": "hurt", "loop": false, "speed": 1.0 }
  },
  "transitions": [
    {
      "from": "idle",
      "to": "walk",
      "when": { "all": [
        { "parameter": "grounded", "op": "==", "value": true },
        { "parameter": "speed", "op": ">", "value": 0.05 }
      ]},
      "blend_seconds": 0.12
    },
    {
      "from": "*",
      "to": "hurt",
      "when": { "parameter": "hurt", "op": "triggered" },
      "blend_seconds": 0.04,
      "priority": 100
    }
  ],
  "markers": {
    "walk": [
      { "time_seconds": 0.25, "event": "vibecraft:footstep_left" }
    ]
  }
}
```

V1 graph semantics:

- parameter types are `bool`, finite `float`, bounded integer/enum, and edge `trigger`;
- conditions are a typed AST containing bounded `all`, `any`, `not`, and comparisons—never source-code strings or a general expression language;
- transitions are evaluated at one presentation update point in descending explicit priority, then source order; at most one transition starts per graph per update;
- wildcard transitions are allowed for interrupts; an exact transition wins only through explicit higher priority, not undocumented ordering;
- `blend_seconds` is finite and bounded. A zero duration is a cut;
- loop/once, playback speed, transition/reset, and completion semantics are in this schema because glTF intentionally does not define them;
- clip time advances from presentation delta time. Reconciliation may correct presentation parameters but animation does not write simulation state;
- root-node translation/rotation is visual only and root motion is ignored in v1. Authoritative movement drives the model transform;
- markers are authored only in the VibeCraft animation graph and compile into clip-time tables. In v1 they select a fixed host-owned cosmetic operation (sound/particle/local visual preset) under per-instance/global quotas and deduplication. They cannot invoke a generic mod event, deal damage, move items, mutate blocks, or call methods/scripts;
- gameplay actions trigger animations through authoritative presentation events; an animation marker is never proof that an attack/open/use occurred;
- graph state is reset/rebound explicitly on model/snapshot revision changes. Hot reload cannot leave old node/clip handles attached to a new model.

Layered/additive animation, inverse kinematics, retargeting, procedural bone code, arbitrary material-property animation, and network-synchronized cinematic timelines are deferred to later schema capabilities. V1 can ship early-Minecraft-style locomotion, attack/hurt, chest open/close, and simple block-entity motion without them.

### Validation and limits

In addition to `ASSET-01` archive/parser limits, v1 defaults are:

| Resource | V1 limit |
| --- | ---: |
| Cuboid elements in one model | 256 |
| Emitted faces in one cuboid model | 1,536 |
| Nodes / primitives in one future model payload | 2,048 / 2,048 |
| Vertices / triangles in one future model payload | 1,000,000 / 2,000,000 |
| Joints per skin / influences per vertex | 256 / 4 |
| Morph targets per primitive | 8 |
| Named clips / total animation key values | 128 / 1,000,000 |
| Sockets in one model | 128 |
| Block cases + multipart rules | 1,024 total |
| Animation states / transitions / condition depth | 128 / 512 / 16 |
| Marker events in one graph | 4,096 |

All counts and byte products use checked arithmetic before allocation. Float values,
quaternions, indices, animation times, and dimensions must be finite/valid under the
eventual selected format. No source payload is passed directly to Godot.

The asset compiler also enforces aggregate profile budgets and emits cost estimates:

```text
model vertices/triangles, skin matrices, morph bytes, clip key bytes,
material layers/atlas eligibility, static-template vertices,
potential block-state template count, animation-graph state cost
```

A valid but over-budget package fails before GPU allocation with the expensive asset and measured cost named. Server-required resource profiles use the published default limits rather than privately raising them.

### Compilation and runtime interfaces

```csharp
public interface IAssetCompiler
{
    CompiledAssetSet Compile(IAssetSnapshot snapshot, AssetCapabilitySet capabilities,
                             AssetBudget budget, CancellationToken cancellationToken);
}

public sealed record CompiledModel(
    AssetKey Key,
    ImmutableArray<RenderPrimitive> Primitives,
    ImmutableDictionary<string, MaterialHandle> MaterialSlots,
    ImmutableDictionary<string, NodeHandle> Sockets,
    ImmutableDictionary<string, ClipHandle> Clips,
    ModelBounds Bounds);

public sealed record BlockRenderTemplate(
    AssetKey Source,
    ImmutableArray<StaticSurfaceTemplate> Surfaces,
    FaceOcclusionMask Occlusion,
    ModelBounds Bounds);

public interface IAnimationGraphInstance
{
    void Set(AnimationParameter key, PresentationValue value);
    AnimationPose Evaluate(float presentationDeltaSeconds, CosmeticEventSink events);
}
```

Compilation order:

```text
parse strict descriptors
  -> resolve final asset graph and origins
  -> validate cuboids/future model payloads/materials/graphs against capabilities and budgets
  -> flatten block states and compile CPU render/animation tables
  -> cache by source/effective snapshot digests
  -> publish Godot textures/meshes/materials/animation backend objects
  -> atomically expose new immutable handles
```

Only the final publication adapter references Godot. Headless tools/server-side content agreement can validate IDs, manifests, block-visual state coverage, and authoritative-vs-visual boundaries without loading Godot or creating GPU resources.

### Minecraft conversion

The `ASSET-02` offline converter translates rather than embeds semantics:

- Java cuboid elements and face UV/cull data become native cuboid models after unit/axis conversion.
- Java blockstate variants/multipart rules are expanded against the VibeCraft block registry mapping and compiled into unambiguous native cases/parts. Parent chains are resolved during conversion; native runtime models do not open Minecraft parents.
- Java textures/material metadata map only to supported native textures/materials. Core shaders and third-party conventions are reported unsupported unless a separately versioned converter plugin explicitly owns them.
- Bedrock bones/cubes/locators/keyframes are unsupported until the selected native
  model/rig format has a converter. A report names them rather than pretending to
  preserve animation semantics. Molang/render-controller expressions remain unsupported
  unless a later converter owns equivalent typed presentation inputs.
- Minecraft collision/selection/behavior definitions never enter a resource pack as authority. If a future content/data converter maps gameplay, it emits a separately reviewed server-visible package.

Converted output is subjected to the same unique-name, bounds, material-slot, state-coverage, capability, and cost validation as hand-authored native content. No runtime code branches on source provenance.

## Greenlight criteria

- The selected model/rig format spike produces deterministic cross-platform fixtures
  for direction, block scale, profile bind pose, required joints, sockets, and built-in
  clip binding without relying on Godot import products.
- The importer rejects external/data/network/file URIs, images, duplicate referenced names, unsupported required extensions, invalid floats/accessors, over-limit graphs/geometry, and missing material slots before Godot/GPU publication.
- Every valid block registry state compiles to exactly one base template plus deterministic multipart additions; invalid properties, overlapping cases, missing fallback coverage, and nondeterministic weighted choices fail tests.
- Full-cube/slab/stair/cross-plant/fence fixtures produce correct neighbor culling and no holes; falsified `cull_against` assertions are rejected.
- Changing only a render model/resource pack cannot change server collision, selection/reach, block state, light used by simulation, movement, damage, item timing, or save/network schema.
- Animation transitions, interrupts, looping, completion, marker deduplication, snapshot replacement, and root-motion ignore behavior have golden deterministic tests independent of Godot frame rate.
- The backend can render one representative section containing all static block templates without per-block Godot nodes and can animate 1,000 representative simple entity instances at 60 Hz within a measured 2 ms animation-update CPU budget on the eventual minimum-spec desktop; failure means simplify/batch the graph backend, not weaken authority boundaries.
- Java and Bedrock converter fixtures generate only supported native descriptors and
  produce explicit unsupported-feature reports; the runtime result is independent of
  Minecraft source edition.

## Prototype or benchmark

Required: yes.

Smallest useful experiment:

1. Author cube, slab, stair, fence, crossed plant, chest, and skinned quadruped fixtures.
2. Compile cuboids into section mesh templates with face culling and render all block-state rotations.
3. Run a focused model/rig spike comparing a purpose-built voxel-oriented source format
   with offline import/conversion candidates; require a profile-compatible chest and
   quadruped fixture, sockets, and built-in idle/walk/hurt/open clip binding.
4. Drive profile clip selection from synthetic presentation snapshots.
5. Feed adversarial model payloads/descriptors from the selected format's corpus plus
   VibeCraft-specific over-limit/name/reference cases.
6. Convert one Java cuboid/blockstate pack and one Bedrock bone/controller pack with deliberate unsupported constructs.

Success metrics:

- Headless compilation is deterministic across Windows/Linux for asset inventory, state expansion, origin graph, cost report, and cache keys.
- Golden mesh images/index buffers show exact culling for all neighboring fixture combinations and rotations.
- No invalid fixture reaches a Godot resource constructor; peak allocation remains within the declared asset budget.
- 10,000 repeated graph runs over the same timestamped parameter stream emit identical state/marker traces.
- Snapshot hot-swap drains all old model/animation handles and either atomically shows old or new content—never mixed slot/clip tables.
- The 1,000-entity and mixed-section benchmark records CPU frame time, allocations, draw/surface counts, GPU upload bytes, and memory on the chosen minimum-spec machine.

## Risks and open questions

- `RENDER-06` may require changes to material grouping/atlas eligibility before transmission/refraction can be exposed. Those features are not pre-approved by this schema.
- Full arbitrary model payloads in dense terrain can inflate section vertices and
  defeat culling/atlasing. V1 should ship cuboid-first authoring guidance and explicit
  compiler cost warnings.
- 1,000 animated scene-node rigs may not meet budget; the contract permits a later batched/skinning backend because packs do not serialize Godot nodes.
- Rig retargeting and additional profile families would improve reuse but create a
  substantial public authoring ABI. Defer them until two real first-party profiles
  demonstrate requirements.
- Resource-only hot reload can change model bounds enough to cause visual popping, but must never alter prediction/server collision. Debug UI should expose both shapes when diagnosing mismatches.
- Texture animation/procedural generation, localization/font shaping, and audio event semantics need their own decisions.

## Dependencies

- Requires: `ASSET-01`, `ASSET-02`, `ARCH-03`, `ARCH-01`, `GAME-01` block-state registry contract.
- Blocks: `ASSET-04`, `RENDER-02` template publication details, `RENDER-06`, entity/block-entity presentation implementation, Minecraft model converters.

## Rejected or deferred alternatives

- Minecraft model/controller formats as native runtime formats: rejected.
- Godot scenes/resources, shader code, method tracks, and arbitrary property paths in untrusted packs: rejected.
- Client resource models defining authoritative collision or root motion: rejected.
- General-purpose expression language/Molang compatibility in native animation graphs: rejected.
- Treating GLB/glTF as the native public-pack format: rejected pending a future owner
  decision; any offline importer must reject unsupported source features explicitly.
- Per-block animated/dynamic Godot instances for ordinary terrain: rejected.
- Generic model inheritance and JSON merge across packages: rejected for v1; conversion resolves source inheritance.
- Additive layers, IK, retargeting, procedural bone scripts, and advanced cinematic timelines: deferred until measured requirements exist.
