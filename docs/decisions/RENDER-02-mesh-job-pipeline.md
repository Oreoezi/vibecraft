# RENDER-02 Mesh rebuild and upload pipeline

Status: Proposed

Owner: Rendering research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Use a bounded, revisioned, priority-driven CPU job pipeline: coalesce invalidations into one desired revision per render section, copy a padded snapshot only when a worker begins, build engine-neutral mesh arrays off-thread, discard stale results, and commit complete meshes on the Godot main thread under a frame-time and memory budget.

One-sentence rationale: Versioned jobs and a budgeted commit point provide responsive edits and streaming without letting stale workers overwrite newer world state or letting GPU upload/destruction spikes dominate frames.

The old resident mesh remains visible until its replacement is fully uploaded. Mesh building is asynchronous even for nearby edits; priority, not synchronous main-thread work, is the responsiveness mechanism.

### Owner review status and plain-language behavior — 2026-08-13

The owner has **not greenlit the detailed mechanism yet** because the first research
pass was too implementation-heavy. The recommendation means this in ordinary play:

1. A block/light/pack change marks the affected section's desired mesh revision dirty.
2. Repeated changes collapse into that newest desired revision instead of spawning an
   unlimited task per block.
3. When a bounded worker is ready, it takes an immutable padded copy of the latest
   section data and builds plain vertex/index arrays without touching Godot.
4. If the world changes again before completion, that old result is discarded by
   revision; it can never overwrite newer terrain.
5. The Godot main thread uploads only a bounded amount each frame. The old correct
   mesh remains visible until replacement, so delayed work produces temporary visual
   staleness rather than holes, corruption, or simulation stalls.
6. Near edits and collision-visible terrain outrank distant/far work; aging prevents
   lower-priority sections from starving forever.

The owner does not need to choose worker count, snapshot/copy strategy, `ArrayMesh`
versus a lower-level buffer backend, or upload milliseconds from prose. The prototype
measures those. The product discussion still needed is the acceptable visible delay
for nearby edits and how aggressively visuals may degrade during teleports/edit
storms.

## Context and constraints

- Terrain edits, neighbor arrival, lighting propagation, resource-pack reloads, and LoD changes can all invalidate section output.
- A single edit near a section edge affects faces and AO/light samples in adjacent sections; rebuilding only the section containing the edited block is incorrect.
- C# workers need immutable inputs. Holding world locks while scanning and emitting thousands of faces would couple rendering latency to simulation/network updates.
- Godot's active scene tree is not thread-safe. Godot documents that render/physics server thread safety requires settings, warns that GPU-interacting calls from other threads can stall, and recommends avoiding scene/resource mutation from multiple workers ([official thread-safety documentation](https://docs.godotengine.org/en/stable/tutorials/performance/thread_safe_apis.html)).
- Mesh creation and destruction are not free even after CPU meshing is fast. The Voxel Tools project reports Godot 4 Vulkan main-thread spikes when many small mesh buffers are destroyed during rapid movement ([official Voxel Tools performance notes](https://voxel-tools.readthedocs.io/en/latest/performance/)).
- Singleplayer and multiplayer must use the same client rendering pipeline. Network arrival order cannot become an implicit correctness assumption.
- The server is authoritative for gameplay and collision. A delayed visual mesh must not delay server block state or make client mesh colliders authoritative.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| Synchronous rebuild and upload on edit | Immediate visual result; simple state | Frame spikes from meshing, allocation, upload, and disposal | Reject |
| `Task.Run` per invalidation | Easy prototype | Duplicate work, unbounded queued snapshots, stale commits, thread-pool contention | Reject |
| One FIFO mesh thread | Simple ordering and bounded concurrency | Cannot use cores; distant work can block a nearby edit | Reject except as debug mode |
| Bounded priority workers + revisions | Coalesces bursts, uses cores, explicit staleness and budgets | More state and telemetry; needs deterministic snapshot boundary | **Recommended** |
| Persistent immutable/COW world snapshots | Cheap worker reads after publication | Significant world-storage complexity and memory amplification | Defer to `WORLD-01` evidence |
| Worker directly mutates `ArrayMesh`/scene | Fewer main-thread calls in theory | Godot thread model and GPU synchronization become implicit; hard to test across renderers | Reject for v1 |
| GPU meshing and indirect commit | Potentially avoids CPU upload form | Custom backend, GPU contention, complex lifetime/cancellation | Defer |

## Evidence

### Minecraft

**Sourced facts.** Java Snapshot 21w37a exposed a “Priority Update” setting that controls which chunk sections update synchronously. Mojang states that doing fewer synchronous updates significantly reduces stutter when placing/removing blocks, especially light sources, at the cost of occasional visible delay ([official snapshot notes](https://feedback.minecraft.net/hc/en-us/articles/4409293520269-Minecraft-Java-Edition-Snapshot-21w37a)). This directly demonstrates the latency/frame-time tradeoff; it does not imply VibeCraft should reproduce the same setting.

Mojang's 2025 Java rendering update says the team is extracting game state from the main thread for a dedicated render thread, with chunks identified as the next major subsystem ([official engineering update](https://www.minecraft.net/en-us/article/the-road-to-vibrant-visuals-on-java)). Bedrock's GDC 2026 presentation says 16³ terrain units are queued for assembly and written into preallocated vertex-pool pages with separate index ranges per terrain layer ([official slides, p. 14](https://media.gdcvault.com/gdc2026/Slides/Fairfield_AJ_ModernizingTheRenderingOfMinecraft.pdf)).

**Inference.** The lesson is to separate state extraction, CPU assembly, GPU ownership, and layer ranges. Minecraft's exact executors and pool sizes are not public contracts for VibeCraft, and Bedrock facts should not be relabeled as Java behavior.

### Clones and rendering replacements

#### Sodium

- Sodium's open source renderer separates build tasks/executors from section management and GPU-facing storage. The source contains a bounded worker loop (`ChunkBuilder`), meshing task type, build context, and result collection in the section manager ([chunk compilation source tree](https://github.com/CaffeineMC/sodium/tree/dev/common/src/main/java/net/caffeinemc/mods/sodium/client/render/chunk/compile), [section manager](https://github.com/CaffeineMC/sodium/blob/dev/common/src/main/java/net/caffeinemc/mods/sodium/client/render/chunk/RenderSectionManager.java)).
- Its source is strong evidence for explicit job/result boundaries and reusable worker contexts. It is not evidence that VibeCraft needs Sodium's OpenGL buffer arena or Minecraft compatibility layers.

#### Luanti

- Luanti documents that client mesh generation runs on worker threads and, since 5.7.0, MapBlock rendering can run across several threads ([official FAQ](https://docs.luanti.org/about/faq/)). Its older architecture description already separates a background cache/face-generation step from the render step ([official engine history](https://docs.luanti.org/for-engine-devs/nmpr/)).
- Its `MeshMakeData` includes a padded voxel volume and its client owns a mesh update queue/thread abstraction ([mesh input source](https://github.com/luanti-org/luanti/blob/master/src/client/mapblock_mesh.h), [client mesh source directory](https://github.com/luanti-org/luanti/tree/master/src/client)).

#### Voxel Tools for Godot

- Voxel Tools uses one prioritized worker pool and sorts tasks so near-player mesh work can precede distant generation ([official development documentation](https://voxel-tools.readthedocs.io/en/latest/development/)).
- Its documented evolution is especially relevant: eagerly copying voxel blocks and neighbors on the main thread allowed simple worker access, but queued copies could accumulate until memory exhaustion and copies could be wasted. It changed to copy/access data when the worker task actually runs under appropriate locking ([official multithreading notes](https://voxel-tools.readthedocs.io/en/latest/performance/#access-to-voxels-from-different-threads)).
- It defers expensive collider construction to the main thread over multiple frames and reports mesh-buffer destruction spikes under Vulkan, showing why “meshing is off-thread” alone does not guarantee smooth frames ([official performance notes](https://voxel-tools.readthedocs.io/en/latest/performance/)).

#### Terasology

- Terasology 5.3's release log records a migration of chunk mesh generation to Flux, a split-out chunk-work helper, and an initial `ChunkMeshWorker` reactive test ([5.3 release notes](https://github.com/MovingBlocks/Terasology/releases/tag/v5.3.0)). This supports explicit asynchronous stages, though the release note alone does not establish that architecture as a performance win and its JVM/reactive implementation should not be copied mechanically into C#.

### Godot-specific evidence

- The active scene tree is not thread-safe; `MeshInstance3D` creation/attachment is unsafe by default. RenderingServer access can be thread-safe when configured, but direct GPU interaction from workers may synchronize and stall ([official thread-safe APIs](https://docs.godotengine.org/en/stable/tutorials/performance/thread_safe_apis.html)).
- `ArrayMesh.add_surface_from_arrays` and the equivalent RenderingServer calls accept complete array surfaces ([ArrayMesh API](https://docs.godotengine.org/en/stable/classes/class_arraymesh.html), [RenderingServer API](https://docs.godotengine.org/en/stable/classes/class_renderingserver.html)). This fits a CPU-result/commit boundary.
- Godot says `ArrayMesh` is somewhat faster than `SurfaceTool` for generated static geometry, while `MeshDataTool` is not appropriate when topology introspection is unnecessary ([official procedural geometry guide](https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/index.html)).

### Conclusions and uncertainty

**Directly supported:** prioritized worker meshing, padded snapshots, complete-array output, and staged main-thread/GPU work are established patterns. Eagerly queueing snapshots can be a memory failure mode.

**Informed inference:** VibeCraft should prohibit worker Godot calls in v1 even where a server API can be configured as thread-safe. A single visible commit point is easier to profile, replay, and support across Godot render-thread modes.

**Unknown until measured:** number of useful C# workers, upload/disposal budget, result-memory cap, and whether `ArrayMesh` is sufficient at target view distance.

## Proposed design

### 1. Identity and revision model

Every render product has an identity and a full input revision:

```csharp
public readonly record struct RenderSectionKey(
    SectionKey Section,
    byte Lod);

public readonly record struct LightRevision(ulong Value);
public readonly record struct RenderRegistryRevision(ulong Value);
public readonly record struct LodSourceRevision(ulong Value);
public readonly record struct MaterializationEpoch(ulong Value);

public readonly record struct MeshInputStamp(
    SectionRevision Content,
    LightRevision Lighting,
    RenderRegistryRevision RenderRegistry,
    LodSourceRevision LodSource,
    MaterializationEpoch Lifetime);

public readonly record struct MeshBuildTicket(
    RenderSectionKey Key,
    MeshInputStamp DesiredRevision,
    MeshPriority Priority,
    long EnqueueSequence);
```

- `Content` changes when geometry/block render state in the sampled area changes.
- `Lighting` changes when mesh-baked shading inputs change. If later lighting is shader-sampled, texture revision is committed separately and need not remesh.
- `RenderRegistry` changes when compiled block/material/model definitions used by the section change.
- `LodSource` changes for far summaries or transition policy.
- `Lifetime` increments whenever the section slot is unloaded/reused, preventing an old result from attaching to a new occupant at the same coordinates.

Equality of the entire revision is required at commit. Monotonic “newer than” comparisons are insufficient when independent revisions can advance.

### 2. Per-section state machine

```text
Absent
  -> WaitingForData
  -> Dirty(desired revision)
  -> Queued
  -> Building
  -> ReadyForCommit
  -> Resident
        | edit/light/pack/neighbor change
        +-----------------------------> Dirty

Any loaded state -> Absent (lifetime increments; queued/finished work becomes stale)
Build/commit error -> Failed(old mesh retained, retry backoff)
```

There is at most one active build and one compact pending intent per key. If a section changes while building, update `DesiredRevision`; do not enqueue a second snapshot. The completed old result is discarded and one build for the latest revision is scheduled.

### 3. Invalidation rules

- Batch block changes received during one client update and compute the union of affected render sections.
- A geometry change dirties its own section. If the changed sample lies in the one-block halo of another section—which occurs at a section face, edge, or corner—dirty that neighbor too. This covers face visibility and corner AO, up to 8 sections for a corner edit.
- Lighting propagation returns changed sample bounds/sections; the renderer invalidates all section meshes whose baked-light halo intersects that result. Do not infer light reach from the original edited block.
- Neighbor data arrival/removal invalidates both sides of the boundary because provisional frontier faces may change.
- Resource-pack reload publishes a new immutable render registry. Maintain a reverse index from definition ID to resident sections so a changed model/material does not blindly remesh the world.
- LoD source updates invalidate only the matching coarse keys and dependent parent summaries (`RENDER-03`).

Invalidation changes desired state; it never directly builds or uploads a mesh.

### 4. Queue and priority

Use a client-owned bounded scheduler shared by generation-adjacent rendering jobs, with class quotas so meshing cannot starve network decode, light upload, or far-summary work. Start with `clamp(logicalProcessors - 2, 1, 6)` concurrent mesh workers and make it a profiled setting, not a user-facing “more is always better” slider.

Priority is lexicographic and recomputed when a ticket is popped:

1. Missing resident mesh intersecting the near camera volume.
2. Section changed by the local player's predicted edit.
3. Visible-frustum dirty section, nearest and most centered first.
4. Recently visible/near streaming frontier.
5. Off-screen maintenance and far LoD.

Age promotes work within a tier to prevent starvation. Camera movement may reprioritize compact tickets; it must not clone snapshots. A user edit can supersede a queued streaming build for the same key.

### 5. Snapshot acquisition

The queue stores only tickets. When a worker wins a ticket:

1. Re-read desired revision/lifetime; abandon immediately if superseded or absent.
2. Lease a contiguous snapshot buffer from a bounded pool.
3. Under short, ordered read leases, copy the section plus required halo and capture the immutable render-registry reference. Never wait on missing chunks while holding another chunk lock.
4. Release all world leases before meshing.
5. If required data is temporarily unavailable, return the ticket to `WaitingForData`; do not treat missing data as a build exception.

The snapshot pool holds at most `workerCount + 1` buffers per size class. This applies the lesson from Voxel Tools' queued-copy memory issue.

### 6. Worker result

Workers call the engine-neutral `ISectionMesher` from `RENDER-01`. Scratch builders come from pools; finalized surfaces use explicit owners that remain valid until commit or stale-result disposal:

```csharp
public sealed class MeshBuildResult : IDisposable
{
    public required RenderSectionKey Key { get; init; }
    public required MeshInputStamp BuiltRevision { get; init; }
    public required IReadOnlyList<OwnedCpuSurface> Surfaces { get; init; }
    public required Aabb Bounds { get; init; }
    public required MeshStatistics Statistics { get; init; }
    public required TimeSpan SnapshotTime { get; init; }
    public required TimeSpan BuildTime { get; init; }

    // Returns every surface buffer to its owner/pool exactly once.
    public void Dispose();
}
```

`OwnedCpuSurface` carries read-only vertex/index memory, valid lengths, and exclusive ownership; it cannot be returned to a pool until upload conversion finishes. Every path—successful commit, staleness, unload, shutdown, and error—disposes the result exactly once. No `Node`, `Resource`, `ArrayMesh`, `RID`, `Material`, or live world reference crosses into the worker result. Cancellation is cooperative between face-direction/slice passes; a stale build need not be interrupted in the middle of a tiny section if checking costs more than finishing.

### 7. Commit and GPU lifetime

At the start of a render frame, the main-thread `MeshCommitController` drains completed results subject to all of:

- exact revision/lifetime match before conversion;
- a default **2.0 ms upload budget per 60 Hz frame**;
- a default **8 section commits per frame**;
- a **128 MiB completed-CPU-mesh cap** across queued results.

Budgets are provisional and tuned by the benchmark. Visible missing meshes are selected before replacements; replacements retain the old mesh while waiting.

For one result:

1. Convert each non-empty logical layer into the agreed Godot packed arrays.
2. Build a new `ArrayMesh` with all surfaces and material bindings off to the side.
3. Recheck the exact desired revision and lifetime.
4. Swap the complete mesh reference/RID into the existing section instance in one main-thread operation.
5. Put the old GPU resource in a delayed disposal queue; spread destruction across frames and never free/recreate an unchanged material resource per section.

If an individual commit exceeds the frame budget, record it and stop draining for that frame. Repeated oversize commits fail the prototype and trigger smaller render units, lower vertex payload, or a pooled/mega-buffer backend investigation.

Do not enable Godot's separate rendering thread merely to claim uploads are asynchronous; the documentation notes known issues, and the pipeline must be correct under the project's selected renderer/thread mode.

### 8. Edit responsiveness and visual behavior

- Local edits receive the highest dirty-mesh priority but do not synchronously remesh on the input/main thread.
- Placement may display a short-lived standalone predicted block model until the section revision commits. Breaking can leave old geometry visible for the target latency budget; selection/collision queries use voxel state, never the stale mesh.
- Keep the previous complete mesh during rebuild. Never clear a section at job start, which would turn normal latency into flicker.
- A loading section with no previous mesh remains absent (or uses a development-only placeholder) until its first complete result.

### 9. Backpressure, unloading, and failure

- When completed result bytes reach the cap, workers stop starting lower-priority builds until commits drain memory. They do not drop the only result for a visible mesh.
- On unload, increment lifetime, remove queued intent, detach the resident mesh, and rate-limit GPU disposal. Workers discover cancellation through the revision check.
- One failed build keeps the old mesh and records a structured diagnostic containing key, revision, block definition, stage, and exception. Retry once after a short backoff only if the desired revision is unchanged; repeated failure is quarantined until another invalidation or pack reload.
- Device loss/recreation invalidates GPU residency, not world content; CPU rebuild or retained CPU data policy is a renderer-level recovery choice.
- Collision mesh/collider generation is a separate optional pipeline with its own budget. Visual readiness never implies authoritative collision readiness.

### 10. Required telemetry

Expose rolling histograms/counters in a debug overlay and trace:

- tickets by tier and state; oldest age;
- snapshot wait/copy/build/queue-to-visible time;
- stale-before-build, stale-after-build, and stale-before-commit counts;
- input/output bytes, vertices, indices, surfaces, and allocations;
- upload and disposal time per frame;
- resident mesh count/bytes and completed-result bytes;
- visible missing/dirty sections;
- retries and quarantined failures.

Without these values, changing worker counts, section size, or LoD distance is guesswork.

## Greenlight criteria

- A randomized concurrency test with edits, light revisions, unload/reload, and pack-registry swaps commits **zero stale revisions** in at least 10 million state transitions under a deterministic scheduler.
- Queue cardinality remains proportional to unique dirty section keys, not edit count; 100,000 edits to one section produce at most one active build plus one latest desired revision.
- Snapshot memory is bounded by the configured leases and completed CPU mesh memory never exceeds the configured cap by more than one in-flight maximum-size result.
- On the agreed reference desktop, exported release build at 60 Hz, normal movement at 20 blocks/s through generated terrain keeps **mesh commit + deferred disposal ≤2.0 ms p95/frame and ≤4.0 ms p99/frame** at the v1 view distance.
- A local edit in an already resident near section becomes visible in **≤50 ms p95 and ≤100 ms p99** when lighting data is ready; no synchronous edit frame exceeds 25 ms because of meshing/upload.
- Rapid camera reversal and teleport cancellation produce no wrong-coordinate mesh, empty flicker, use-after-free, or unbounded stale-result accumulation.
- The system runs correctly under the chosen Godot renderer/thread model with validation enabled; no worker touches the live scene tree or shared mutable Godot resource.

## Prototype or benchmark

Required: yes

Smallest useful experiment:

1. Build the state machine around a fake deterministic world and fake commit sink; use a controllable scheduler to permute edit, unload, build-complete, and commit events.
2. Property-test revision safety and queue coalescing before integrating Godot.
3. Integrate the `RENDER-01` mesher and Godot `ArrayMesh` commit path with 1,024 resident sections.
4. Run four workloads for five minutes each in an exported release build: steady walking, 20 blocks/s flight, repeated 180° camera turns at the streaming edge, and 1,000 block changes/s concentrated and distributed.
5. Repeat with 1/2/4/6 workers and 16³ versus the `WORLD-01` candidate size. Capture frame CPU, render thread, upload, destruction, queue age, memory, GC, and visible latency.
6. Force failures: mesher exception, malformed template, missing neighbor, pack epoch change during build, unload during commit queue, and device/scene teardown.

Success metrics: all greenlight criteria above. If CPU meshing passes but upload/disposal fails, retain the job architecture and prototype a lower-level pooled buffer backend; do not hide the problem by increasing workers.

## Risks and open questions

- Godot may copy managed arrays during variant conversion before uploading. The benchmark must include conversion cost and memory, not only `ISectionMesher` time.
- `ArrayMesh` replacement may remain expensive at large view distances even with budgets. A page/arena backend would reduce resource count but requires a custom renderer abstraction.
- Very high lighting churn can continually obsolete baked-light meshes. `RENDER-04/05` should test shader-sampled light pages before optimizing worker count.
- Reverse indexing block definitions to resident sections costs memory; a coarse per-section bloom/set may be enough and should be measured.
- Priority scoring can starve far terrain while the player continuously edits. Aging and class quotas are mandatory, but exact weights are tuning data.
- A transient placed-block overlay can z-fight with an old mesh if the old section already contains geometry there; its render policy needs a small implementation test.
- Reference hardware and v1 view distance are not yet project-wide decisions. The numerical budgets here are defaults to test, then must be recorded alongside final target hardware.

## Dependencies

- Requires: `RENDER-01` engine-neutral mesh result; `WORLD-01` section size, revision and read-lease/snapshot contract; `RENDER-04/05` lighting invalidation or texture-update contract; `ASSET-03` immutable compiled render registry.
- Blocks: `RENDER-03` LoD job classes and near/far commit handoff; client streaming; renderer teardown/device recovery; performance acceptance for the first playable build.

## Rejected or deferred alternatives

- **Synchronous nearby rebuilds:** rejected by default because Minecraft's own option documents the stutter tradeoff. Reconsider only if the asynchronous p99 target cannot be met and a measured tiny fast path is bounded.
- **One unbounded `Task.Run` per edit:** rejected; it duplicates work and converts bursts into memory/GC pressure.
- **Snapshot at enqueue:** rejected; Voxel Tools documents the wasted-copy and unbounded-queue failure mode.
- **Hold world locks while meshing:** rejected; simulation/network latency would depend on mesh complexity.
- **Clear old mesh while rebuilding:** rejected; it creates avoidable holes and flicker.
- **Worker scene-tree or resource mutation:** rejected for v1 despite configurable server thread safety; keep Godot ownership at one explicit commit point.
- **Collider generation in the same completion transaction:** rejected; collider cooking has different costs and authoritative requirements.
- **GPU/compute meshing:** deferred until CPU generation and commit telemetry identify the actual bottleneck.
