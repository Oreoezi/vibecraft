# ARCH-03 Godot client and voxel-renderer boundary

Status: Proposed

## Decision

Recommended choice: Use Godot's scene tree for application flow, UI, camera, audio, and a manageable number of dynamic visuals; render terrain through a dedicated C# voxel-view subsystem using low-level `RenderingServer` instances and section meshes, with worker-produced plain buffers and main/render-thread publication.

One-sentence rationale: This preserves Godot's productive high-level features without representing blocks—or every streamed section operation—as scene objects and without coupling simulation to the renderer.

## Context and constraints

- The client uses Godot with C#.
- Godot was chosen primarily for its open-source licensing, and the team has limited
  Godot experience. Godot-specific APIs and workflow assumptions therefore remain
  benchmark/prototype-gated rather than becoming architecture folklore.
- Terrain is dynamic, streamed, and potentially thousands of visible sections.
- CPU generation/lighting/meshing should be parallel; Godot APIs have thread restrictions.
- Far terrain, advanced materials, and a possible native hot-path implementation must remain future options.
- Singleplayer server code must run headlessly without loading client rendering types.

## Options considered

| Option | Strengths | Costs/risks | Fit |
| --- | --- | --- | --- |
| Node per block | Intuitive editor hierarchy | Catastrophic object/node overhead | Reject |
| MeshInstance node per section | Simple and debuggable | Scene-tree overhead and main-thread churn at high counts | Good prototype baseline |
| Direct RenderingServer section instances | Lower overhead, explicit lifetime | Manual RID/resource management and tooling | Recommended production boundary |
| Custom GDExtension renderer from day one | Maximum control/performance | Native complexity before requirements/benchmarks | Keep replaceable, defer implementation |
| MultiMesh cubes for terrain | Very low draw submission for repeated cubes | Poor per-instance culling and excessive hidden geometry | Use for repeated props, not general terrain |

## Evidence

Godot documents that its scene system can be bypassed with low-level servers when node overhead becomes a bottleneck, while warning that calls which read data back from asynchronous servers can cause stalls ([optimization using servers](https://docs.godotengine.org/en/stable/tutorials/performance/using_servers.html), [RenderingServer API](https://docs.godotengine.org/en/stable/classes/class_renderingserver.html)). It recommends `MultiMesh` for large repeated-instance counts but notes all-or-none culling at the MultiMesh level ([MultiMesh optimization](https://docs.godotengine.org/en/stable/tutorials/performance/using_multimesh.html)).

Godot's thread-safety documentation says the whole engine is not thread-safe and recommends server APIs rather than touching the active scene tree from workers; resource/GPU operations can synchronize and stall ([thread-safe APIs](https://docs.godotengine.org/en/stable/tutorials/performance/thread_safe_apis.html)). Its C# guide notes native interop for `GodotObject` properties and comparatively expensive raw-array/string marshalling ([C# basics](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html)).

Voxel Tools for Godot implements editable paging, block meshing, storage/generator streams, collision, and LOD as a specialized predominantly C++ subsystem rather than a tree of voxel nodes ([godot_voxel](https://github.com/Zylann/godot_voxel)). This is evidence for the subsystem boundary, not necessarily for adopting that dependency.

## Proposed design

### Project boundaries

```text
VibeCraft.Core       pure types, coordinates, registries, math, commands
VibeCraft.Protocol   transport-independent messages and negotiation
VibeCraft.Server     authoritative simulation and persistence
VibeCraft.Client     prediction, snapshots, presentation models
VibeCraft.Godot      scenes, input, UI, audio, renderer adapters
```

Only `VibeCraft.Godot` references Godot assemblies. Server and core tests run under normal .NET without Godot.

### Terrain renderer

One `VoxelWorldView` owns:

- section visibility/LoD selection;
- a map from section render key to mesh/instance RIDs;
- pooled CPU mesh buffers and upload commands;
- material/render-layer tables;
- a bounded upload/destruction queue;
- floating render-origin transforms;
- renderer metrics and debug overlays.

Each visible section produces one logical mesh with a small fixed number of surfaces/render layers (initially opaque, cutout, transparent; emissive is a material flag or separate surface only if measurement requires it). The renderer creates one spatially cullable instance per section/LoD, not per block or texture.

`MultiMesh` is reserved for repeated independent props such as plants, particles, dropped-item visuals, or decorations grouped into bounded spatial cells. Terrain uses culled surface meshes because emitting hidden cube faces defeats the primary voxel optimization.

### Worker/publication contract

1. Worker captures an immutable section snapshot plus neighbor-border snapshots and revision numbers.
2. Worker emits plain managed/native vertex/index buffers and bounds; it calls no scene-tree API.
3. Main/render publication validates revisions and visibility, then creates/replaces the mesh RID under a per-frame byte/time budget.
4. Superseded outputs are discarded; old RIDs are freed only after replacement is installed.
5. Section unload cancels queued work and eventually frees all owned RIDs.

The CPU side never queries buffers back from `RenderingServer`; it keeps its own metadata. The exact upload API (`ArrayMesh` first, lower-level surface buffers later) is an adapter implementation detail selected by benchmark.

### Dynamic visuals

Players, a modest number of mobs, cameras, UI, audio emitters, and complex animated models may use scene nodes. Client presentation objects subscribe to immutable/interpolated state and never become authoritative simulation objects. Large crowds or repeated props graduate to batched rendering based on profiling.

### Physics and collision

Player movement authority uses the server's voxel collision model, not Godot rigid-body results. The client uses the same deterministic/queryable block collision routines for prediction. Godot physics shapes are presentation/interaction aids for non-authoritative visuals and should be generated/published separately from render meshes with their own budget.

### Coordinates

World positions remain integer block/section coordinates plus local double/fixed state in core. Godot transforms are relative to a movable client render origin to avoid large-coordinate float precision loss. Rebase only presentation transforms; never rewrite authoritative world coordinates.

## Greenlight criteria

- No block is represented by a Godot node/object.
- Server/core assemblies compile and test without Godot.
- Worker meshing touches no live scene object and stale results cannot replace newer meshes.
- RID ownership has deterministic cleanup verified across load/unload/reconnect.
- Render-origin rebasing causes no visible section/entity discontinuity.
- A node-based section adapter can be swapped for direct server RIDs behind one interface for benchmarking/debugging.

## Prototype or benchmark

Required: yes.

Generate a synthetic moving-camera scene with editable sections and compare:

1. `MeshInstance3D` per section using `ArrayMesh`;
2. direct `RenderingServer` instance per section;
3. batched repeated props with spatially partitioned `MultiMesh`.

Measure frame CPU, upload spikes, draw calls, culling, memory, C# allocations/interop, and unload cleanup at increasing view distances. Inject rapid repeated edits to verify stale-job rejection and bounded uploads. Move the render origin across a threshold while edits and entity interpolation continue.

The spike must also validate the actual developer workflow: editor/project setup,
headless test execution, C# debugging/profiling, asset publication, and exported
Windows/Linux builds. The open-source licensing rationale is accepted; this workflow
evidence decides whether the selected Godot adapter is pleasant enough to retain.

## Risks and open questions

- Godot rendering APIs and optimal buffer paths vary by engine version; pin a supported Godot minor before implementation.
- Transparent geometry sorting across/within sections needs a dedicated rendering decision.
- Native GDExtension migration requires a stable buffer ABI and careful ownership; keep it optional until profiling proves need.

## Dependencies

- Requires: `RENDER-01`, `RENDER-02`, `WORLD-01`.
- Blocks: client project layout, rendering prototypes, LoD and material implementation.

## Rejected or deferred alternatives

- Godot scene tree as authoritative simulation: rejected.
- Node per section as an immutable final architecture: deferred to benchmark; acceptable as a debugging adapter.
- Native renderer rewrite before a measured C# baseline: rejected.
