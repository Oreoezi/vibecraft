# WORLD-05 Chunk lifecycle, dirty tracking, and memory budgets

Status: Proposed

Owner: VibeCraft architecture research
Date researched: 2026-08-09
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Put every resident world unit under a simulation-thread-owned lifecycle registry, derive its required capability from explicit leases, account its owned bytes against soft and hard budgets, track persistence with monotonically increasing revisions, and require a successful revision-checked save before dirty data can be evicted.

One-sentence rationale: Explicit ownership and accounting make loading, ticking, saving, cancellation, shutdown, and overload one coherent state machine instead of a collection of races around `loaded` and `dirty` booleans.

This recommendation deliberately rejects two tempting shortcuts: garbage-collector pressure is not a world cache policy, and completion of an asynchronous load/save/generation task is not permission to mutate authoritative state.

## Context and constraints

- The Godot-free `ServerCore` is authoritative; `ARCH-04` will select child-loopback or embedded singleplayer hosting by experiment. Neither adapter may bypass lifecycle, protocol semantics, or persistence rules.
- `WORLD-02` workers produce immutable load/generation results. Only the world's simulation thread may publish them, issue save snapshots, transition capabilities, or remove resident data.
- Multiple independent consumers can need one chunk: player simulation, network visibility, cross-chunk generation, a save in flight, plugins, administrative force-load, and shutdown.
- Movement and view-radius changes cause demand to flap at boundaries. Immediate unload/reload wastes I/O and CPU, but unrestricted hysteresis defeats memory bounds.
- A dirty chunk may change again while its earlier revision is being serialized or written. Save completion therefore cannot simply clear one dirty flag.
- Storage errors must preserve the last known authoritative in-memory data and surface backpressure; they must not silently regenerate, discard, or mark it clean.
- The process has other large consumers: runtime/GC overhead, entities, networking, plugin heaps, generator scratch, client assets in singleplayer, and Godot rendering. A chunk budget cannot assume all available process memory.
- The specification's combination of “square chunks,” “no maximum height,” and finite memory is inconsistent if a whole infinite-height X/Z column is one allocation or lifecycle unit. `WORLD-01` must define independently materialized vertical sections or bounded groups of sections. In this brief, **chunk unit** means that finite key chosen by `WORLD-01`, not an unbounded column.
- Save format, atomic replacement, journal/recovery, and world metadata belong to `WORLD-03`/`WORLD-04`; this decision defines when and what the lifecycle asks them to persist.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| A. Distance sets plus `Loaded`/`Dirty` booleans and periodic saves | Easy to prototype; resembles many small voxel demos | Cannot express save pins, cross-chunk dependencies, tiered ticking, stale async completion, or hard memory admission; dirty-during-save races are likely | Reject |
| B. Central lifecycle registry, capability leases, revisioned saves, accounted byte budget | One owner and inspectable state; deterministic transitions; bounded memory; graceful overload; testable failure behavior | Requires lease hygiene, conservative byte charging, and an explicit state machine | **Recommend** |
| C. Let .NET GC pressure and an LRU cache decide residency | Minimal custom accounting | GC observes pressure after allocations, cannot identify safe eviction, and cannot discard dirty state; pooled/native memory complicates the signal | Reject |
| D. Region actors own independent caches and persistence queues | Scales ownership toward parallel region simulation | Cross-region leases, balancing, shutdown, and plugin access become distributed protocols before VibeCraft has measured a need | Defer with the parallel-region model in `WORLD-08` |

## Evidence

### Minecraft and Paper

Minecraft's source is not published as a normal open-source repository. The mapped class inventories below are generated from Mojang binaries and official/community mappings, so they are **secondary implementation evidence**, not official architecture documentation.

- Java 1.21.1's mapped `ChunkHolder` has distinct futures for accessible, block-ticking, and entity-ticking chunks and tracks a save dependency. Java 1.21.1's mapped `DistanceManager` separately maintains tickets, players, simulation distance, and tests for block/entity ticking. This is evidence that modern Minecraft models chunk capability and demand as more than a loaded boolean. ([mapped `ChunkHolder`](https://mappings.dev/1.21.1/net/minecraft/server/level/ChunkHolder.html), [mapped `DistanceManager`](https://mappings.dev/1.21.1/net/minecraft/server/level/DistanceManager.html))
- Java 1.21.8's mapped `ChunkMap` exposes pending unloads, chunks awaiting unload, active chunk writes, and main-thread reconciliation alongside asynchronous generation dispatchers. This is useful evidence for explicit unload/save state and single-owner publication. ([mapped `ChunkMap`](https://mappings.dev/1.21.8/net/minecraft/server/level/ChunkMap.html))
- Paper's official command reference describes four observable chunk states: inactive, full, block ticking, and entity ticking. Its world configuration exposes delayed chunk unload, autosave interval, a maximum number of autosaved chunks per tick, and per-chunk entity save limits. These are practical signs that lifecycle hysteresis and save work need budgets. ([Paper commands](https://docs.papermc.io/paper/reference/commands/), [Paper world configuration](https://docs.papermc.io/paper/reference/world-configuration/))

Inference for VibeCraft: reproduce the small set of capabilities and explicit save/unload gates, not Minecraft's full ticket-level graph or version-specific promotion machinery.

### Luanti

- Luanti's public API distinguishes `unknown`, `emerging`, loaded-but-inactive, and active mapblock states. Its active block modifiers run only on active blocks, while loading block modifiers run when a block is activated. This is direct, primary evidence from the engine's API that residency and simulation activity are separate lifecycle properties. ([core namespace lifecycle status](https://api.luanti.org/core-namespace-reference/), [ABM/LBM definitions](https://api.luanti.org/definition-tables/))
- Luanti's server environment updates an active-block set around players, deactivates distant objects, records timestamps when blocks leave the active set, activates newly included blocks, and steps node timers in active blocks. ([`serverenvironment.cpp`](https://github.com/luanti-org/luanti/blob/master/src/serverenvironment.cpp))
- Luanti's server map saves only modified blocks and clears their modified state after serialization; its unload path explicitly avoids freeing a block immediately when other code may still hold pointers. ([save path](https://github.com/luanti-org/luanti/blob/master/src/servermap.cpp#L3022-L3121), [unload/deferred-delete path](https://github.com/luanti-org/luanti/blob/master/src/servermap.cpp#L3488-L3544))

Lesson: active/resident separation and dirty-only saves are useful; VibeCraft should improve on raw-pointer lifetime and flag-clearing by using generation-stamped handles and revision-aware save acknowledgement.

### Minestom

- Minestom's `InstanceContainer` maintains one future per in-progress coordinate, rechecks the cache during deduplication, and removes the exact future on completion. Unload is a separate operation and asynchronous save calls explicitly gather loaded chunks. ([`InstanceContainer` load/unload](https://github.com/Minestom/Minestom/blob/master/src/main/java/net/minestom/server/instance/InstanceContainer.java#L245-L306), [deduplication and publication](https://github.com/Minestom/Minestom/blob/master/src/main/java/net/minestom/server/instance/InstanceContainer.java#L308-L360))
- Minestom's July 2026 release notes include fixes for sharing concurrent requests for the same chunk and safely removing completed or failed in-progress loads. Even a compact lifecycle registry has non-obvious completion races. ([Minestom releases](https://github.com/Minestom/Minestom/releases))

Lesson: deduplicate asynchronous materialization, but make authoritative commit, epoch validation, save pinning, and eviction explicit rather than implied by future completion.

### Veloren and Terasology

- Veloren's owner manual states that ordinary world edits are not persisted by default and can disappear when chunks unload; persistence is experimental. This is a product-level demonstration of why “infinite generated terrain” and “durable player-built terrain” require different lifecycle guarantees. ([Veloren owner manual](https://book.veloren.net/players/building.html))
- Veloren's persistence merge request describes storing an overlay of modified blocks and loading it per chunk. That can be efficient for edits, but it still requires an unload gate that knows whether the overlay revision reached durable storage. ([Veloren persistence MR](https://gitlab.com/veloren/veloren/-/merge_requests/2662))
- Terasology documents finite `32x32x64` chunks even though the world is coordinate- and memory-limited rather than a preallocated map. A Terasology memory issue records failures when the live chunk/render set does not fit direct memory and discusses load/render distance as the controlling variable. ([Terasology block-world concepts](https://metaterasology.github.io/docs/concepts/blockWorld.html), [Terasology issue #4948](https://github.com/MovingBlocks/Terasology/issues/4948))

Lesson: infinite addressability still requires finite resident units, explicit retention policy, and an admission response when the working set cannot fit.

### .NET and Godot

- .NET exposes `GC.GetGCMemoryInfo()` and `TotalAvailableMemoryBytes`, while runtime configuration can impose a heap hard limit, including container-aware limits. These are useful inputs to a default budget but are not per-chunk ownership accounting. ([`TotalAvailableMemoryBytes`](https://learn.microsoft.com/en-us/dotnet/api/system.gcmemoryinfo.totalavailablememorybytes), [.NET GC configuration](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/garbage-collector))
- Arrays of 85,000 bytes or more normally go to the large object heap, where repeated allocation has different collection costs. Chunk storage and save snapshots therefore need reusable ownership and benchmarked allocation shapes. ([Microsoft large object heap guidance](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/large-object-heap))
- Godot documents that the active scene tree is not thread-safe and that shared resources modified from multiple threads can behave unexpectedly. A client cache may consume immutable chunk snapshots on workers, but Godot nodes, resources, rendering, and physics publication remain on the client main thread. ([Godot thread-safe API guidance](https://docs.godotengine.org/en/4.5/tutorials/performance/thread_safe_apis.html))

## Proposed design

### State, capability, and identity

Maintain one `ChunkLifecycleRecord` per known finite chunk unit:

```text
Absent -> Queued -> Materializing -> Staged -> Resident -> Evicting -> Absent
              |            |
              +----------> MaterializationFaulted

Resident --save failure--> Resident [PersistenceFault flag; eviction blocked]
```

- `Materializing` owns or awaits the deduplicated `WORLD-02` job; it does not expose a mutable chunk.
- `Staged` is an immutable result waiting for simulation-thread commit. Commit validates key, materialization epoch, storage/generator identity, and continuing demand.
- `Resident` owns authoritative mutable data on the simulation thread. Its current capability is derived from leases and may be `Resident`, `BlockTicking`, or `EntityTicking`; network visibility is a per-client interest result, not a reason to let the network thread own a chunk.
- `Evicting` means no ordinary new mutation is allowed, but a save snapshot or persistence acknowledgement may be outstanding. New demand aborts eviction before removal and advances the materialization epoch only if removal actually completed.
- `MaterializationFaulted` records a load or generation failure before publication. A save failure leaves authoritative data `Resident`, sets a persistence-fault flag, retains its dirty revision and persistence lease, and blocks eviction. Existing persisted data that fails to decode is never replaced automatically by generated terrain.

```csharp
public enum ChunkCapability : byte
{
    Resident,
    BlockTicking,
    EntityTicking
}

public enum ChunkLeaseReason : byte
{
    PlayerSimulation,
    PlayerInterest,
    GenerationDependency,
    EntityContinuity,
    Persistence,
    Plugin,
    Administrative
}

public readonly record struct ChunkHandle(SectionKey Key, ulong Epoch);

public interface IChunkLifecycle
{
    IChunkLease Acquire(
        SectionKey key,
        ChunkCapability capability,
        ChunkLeaseReason reason,
        LeaseOwner owner);

    bool TryResolve(ChunkHandle handle, out IReadOnlyChunkView view);
    ChunkLifecycleSnapshot GetMetrics();
}
```

- Leases are acquired and released only by simulation-thread commands. The registry records owner, reason, capability, acquisition tick, and optional expiry tick.
- The effective capability is the maximum currently leased capability. A persistence lease pins bytes without activating simulation.
- Player and internal subsystem leases may be renewed indefinitely. Plugin leases must have a declared owner and a default expiry no greater than 30 seconds; renewal is explicit so an unloaded plugin cannot pin the world forever.
- No public API returns a stable mutable `Chunk` reference. `ChunkHandle` resolution checks the epoch on the simulation thread, preventing an old handle from resolving after unload/reload at the same coordinates.
- Cross-chunk structures and entities acquire dependencies before access and release them at a tick boundary. Code must not wait for a lease while holding another chunk lock; the v1 simulation has no chunk locks because it has one writer.

### Demand, hysteresis, and transitions

1. At the start of each simulation tick, apply sorted player, entity, generation, administrative, and plugin lease changes.
2. Create or update `WORLD-02` demand for unsatisfied leases. Duplicate leases share the same lifecycle and job record.
3. Drain staged materialization completions in stable chunk-key order, validate epoch and current demand, then publish.
4. Derive capability changes. Activation/deactivation takes effect only between ticks; `WORLD-08` never iterates a set while it is being changed.
5. When the last lease disappears, retain the resident as an unleased cache candidate for a configurable 10-second grace period. Paper exposes delayed unload for the same anti-churn purpose; 10 seconds is a provisional VibeCraft default, not a compatibility rule. ([Paper world configuration](https://docs.papermc.io/paper/reference/world-configuration/))
6. Memory pressure may shorten or skip grace for clean chunks. Dirty chunks first enter the save queue and remain charged and resident until persistence succeeds.

Demand is recalculated from authoritative player/entity state; clients cannot request arbitrary coordinates into residency. Teleport admission may wait or fail before moving a player if its required working set cannot be admitted.

### Dirty revisions and save protocol

Every authoritative mutation increments a chunk-local `Revision` and the appropriate change domains:

```csharp
public sealed class ChunkPersistenceState
{
    public long Revision { get; private set; }
    public long PersistedRevision { get; private set; }
    public long SaveInFlightRevision { get; private set; }
    public ChunkChangeMask ChangeMask { get; private set; }
}

public readonly record struct ChunkSaveSnapshot(
    SectionKey Key,
    ulong Epoch,
    long Revision,
    StorageFormatVersion Format,
    ReadOnlyMemory<byte> CanonicalPayload);

public readonly record struct ChunkSaveAck(
    SectionKey Key,
    ulong Epoch,
    long Revision,
    DurableObjectId ObjectId);
```

- `Revision > PersistedRevision` is the definition of dirty. Mesh, replication, lighting, and persistence use separate acknowledgement revisions/masks; sending a packet or rebuilding a mesh never marks data persisted.
- On the simulation thread, create an immutable snapshot of revision `R` and acquire a persistence lease. Serialization/compression and atomic storage run outside the simulation thread under bounded persistence queues.
- On acknowledgement, validate key and epoch, then advance `PersistedRevision` through `R`. If `Revision > R`, the chunk remains dirty and is queued again; completion of an older save can never clear a newer edit.
- Coalesce ordinary saves: at most one write is in flight per chunk, and later edits are represented by the next revision. Eviction-blocking and shutdown saves are not dropped by coalescing.
- Save priority is: eviction-blocking, shutdown, oldest dirty age, then periodic/background. Within a class, use stable `(firstDirtyTick, chunk key)` ordering. Cap snapshot creation and completed acknowledgements per tick so autosave cannot monopolize simulation.
- A save error releases no dirty state and no eviction gate. Retry transient failures with bounded exponential delay, expose the failure and dirty-byte count, stop admitting soft loads when unsaved bytes exceed a threshold, and reject new joins/teleports before risking data loss.
- `WORLD-04` must define an atomic durable commit. An acknowledged save means the new object and its index/manifest update survive the promised crash model, not merely that bytes reached a process buffer.

### Memory accounting and overload

Use explicit logical charging for all lifecycle-owned memory:

- block/biome/light arrays at allocated capacity;
- palettes and lookup tables;
- block entities, ordinary entities, and their component storage;
- scheduled/block/fluid tick queues and indexes;
- deferred cross-chunk writes and dirty/save metadata;
- immutable save snapshots until their buffers are released.

`WORLD-02` generation scratch and storage/compression queues have separate budgets. Pooled memory remains charged to its current owner until returned; moving a buffer transfers the charge exactly once. Metrics also report managed heap, LOH, process working set, and GC high-memory load, but those signals do not replace ledger enforcement.

Initial automatic budgets are deliberately conservative and provisional:

```text
available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes
dedicated chunk hard budget   = min(configured cap (default 4 GiB), 35% of available)
embedded chunk hard budget    = min(configured cap (default 2 GiB), 20% of available)
soft limit                    = 85% of chunk hard budget
```

The server measures its non-world baseline before opening the world and refuses an automatic configuration that leaves less than 256 MiB for resident world data; the administrator may explicitly supply a tested budget. Fractions and caps must be tuned by the prototype. They are not guarantees that a particular view/simulation distance fits.

At the soft limit:

- stop `Batch` and outer prefetch admission;
- reduce optional send/prefetch radius before simulation radius;
- evict unleased clean chunks in order of grace expiry, oldest access, then largest charge;
- prioritize dirty cold chunks for saving.

At the hard limit:

- admit only already-budgeted dependencies, persistence work needed to free memory, and `RequiredNow` demand that fits after selected eviction;
- skip grace for clean unleased chunks and temporarily reduce optional radii;
- delay or reject teleports, joins, and administrative force-loads whose required set cannot fit;
- if pinned/dirty bytes alone exceed the limit, enter an explicit degraded state, reject new residency demand, and report the top owners. Never unload dirty data or allocate past the limit hoping that GC fixes it.

Eviction removes a record only when it has no leases, no required dependents, no staged/running publication, no save snapshot using mutable ownership, and `Revision == PersistedRevision`. The simulation thread rechecks epoch, leases, revision, and budget state immediately before removal.

### Shutdown

Graceful shutdown is a lifecycle phase, not a timer followed by process exit:

1. Stop accepting connections, teleports, plugin force-loads, pregeneration, and soft materialization demand.
2. Notify clients and stop taking gameplay commands after a final input tick.
3. Release player/soft leases, cancel unneeded queued generation, and drain or reject staged completions deterministically.
4. Disable plugin mutation, snapshot every dirty resident, and keep persistence leases until each target revision is durably acknowledged.
5. Persist world/global metadata only in the ordering required by `WORLD-04`, flush storage, then dispose residents and worker services.

Graceful mode has progress reporting and no default data-loss timeout. A separately named administrator action such as `force-exit-with-data-loss` may terminate after displaying unsaved chunks/revisions; it must never be the ordinary singleplayer close path.

### Required metrics and diagnostics

- Resident count and charged bytes by capability, dimension, allocation category, and lease reason.
- Soft/hard limit crossings, admissions refused, radius reductions, grace bypasses, and eviction/save latency.
- Dirty chunk/byte count, oldest dirty age, snapshot bytes, save queue age, retries, failures, and revision-lag histogram.
- Lease count and oldest age by owner; diagnostics list the largest pinning owners and chunks.
- Load/generation/save stale completion count, epoch mismatch count, and handle-resolution failure count.
- Shutdown target revisions, acknowledgements remaining, bytes remaining, and last error.

## Greenlight criteria

- Under a deterministic walk/teleport/disconnect trace, charged lifecycle memory never exceeds the configured hard budget; optional demand is shed in the documented order.
- No chunk is removed with a lease, dependency, in-flight required publication, or `Revision > PersistedRevision`.
- Mutating a chunk during save revision `R` leaves revision `R+1` dirty after `R` is acknowledged; reordered, duplicated, stale-epoch, and failed acknowledgements cannot mark it clean.
- After 10,000 load/activate/deactivate/unload cycles, lease, lifecycle-record, save-snapshot, and pooled-buffer counts return to baseline plus intentional residents.
- Repeated movement across a radius boundary causes at most one materialization per chunk within the 10-second grace interval unless hard memory pressure intentionally evicts it.
- With storage paused for 60 seconds, the server remains within memory bounds, stops soft admission, reports dirty backpressure, and neither discards nor silently regenerates data.
- A crash at every persistence boundary recovers either the previous durable revision or the newly acknowledged revision according to `WORLD-04`, never a clean marker for missing/corrupt payload.
- Graceful shutdown with injected edits in its final tick exits only after all reported target revisions are durable; forced failure reports exactly which revisions remain unsaved.

## Prototype or benchmark

Required: yes

Smallest useful experiment: Build an engine-neutral C# lifecycle harness around a synthetic section-sized chunk representation, the `WORLD-02` scheduler interface, and a fault-injecting in-memory/file persistence adapter. Replay player interest changes, cross-chunk leases, concurrent load completions, dirty-during-save edits, memory pressure, storage stalls, and shutdown.

Success metrics:

- Run the greenlight criteria at hard budgets of 256 MiB, 512 MiB, and 1 GiB and at 1/2/4 materialization workers.
- Measure actual charged bytes versus process working-set and managed-heap deltas for at least 10,000 homogeneous and palette-diverse chunks; ledger undercount must be zero for owned buffers and total estimate error must stay conservative within 20% after calibration.
- Record p50/p95/p99 activation, save, and eviction latency; lifecycle bookkeeping plus completion commit must consume less than 2 ms p99 of the 16.67 ms fixed 60 TPS step under the target resident count.
- Randomize asynchronous completion and acknowledgement order in 100 seeded runs; final resident keys, revisions, persistent hashes, and lease counts must be identical.
- Kill the process after every simulated storage step and verify recovery against the acknowledged revision log.

Failure rule: If the ledger exceeds its hard bound, a stale save clears a newer edit, or crash recovery contradicts an acknowledgement, leave the decision unapproved and redesign before runtime integration.

## Risks and open questions

- `WORLD-01` is a hard blocker: even the initial approximately 10,000-block build range cannot be one resident/allocation unit. Vertical section grouping affects keys, entity ownership, cross-section ticks, save records, and budget granularity.
- Logical accounting is easiest for owned arrays and pools but approximate for object graphs, runtime overhead, plugins, and native allocations. The ledger must intentionally overcharge calibrated per-entry overhead and compare itself with process/GC telemetry.
- A pinned working set can legitimately exceed an administrator's configured limit. The system can reject new demand and expose owners, but it cannot preserve simulation radius, progress, and a hard cap simultaneously.
- Snapshot copies can double memory during save. `WORLD-03` should test immutable/page-owned or copy-on-write section representations, but v1 should prefer a bounded copy whose cost is charged over unsafe concurrent serialization.
- A fixed ten-second cache grace may be wrong for slow disks or tiny singleplayer budgets; retain the state machine and tune the default from traces.
- Plugin force-load and direct data access are leak vectors. `ARCH-05` must expose expiring leases, quotas, and shutdown revocation rather than mutable chunk references.
- World-level metadata can refer to chunks and entities. `WORLD-04` must define a crash-consistent ordering or generation scheme across those records; chunk-level revision acknowledgement alone is insufficient.

## Dependencies

- Requires: `ARCH-01` authoritative ownership; `ARCH-04` singleplayer/server shutdown; `WORLD-01` finite chunk/section key and memory representation; `WORLD-02` materialization scheduler; `WORLD-03` storage API; `WORLD-04` durable commit/recovery.
- Coordinates with: `WORLD-08` capability-driven ticking; `NET-05` interest and radius degradation; `WORLD-07` cross-chunk structures; `ARCH-05` plugin leases; client rendering lifecycle in `RENDER-02`.
- Blocks: runtime chunk cache, autosave, unload, teleports/joins under pressure, graceful shutdown, and any stable plugin chunk-access API.

## Rejected or deferred alternatives

- One loaded boolean: rejected because resident, block-ticking, entity-ticking, saving, and network-visible are different properties.
- One dirty boolean cleared on save completion: rejected because it loses dirty-during-save information.
- Weak references/finalizers as eviction: rejected because collection timing is nondeterministic and cannot enforce save-before-evict.
- GC high-memory notification as the primary budget: rejected because it arrives after allocation and has no knowledge of leases, dirty revisions, or safe victims.
- Unload dirty chunks after a timeout: rejected because availability is not worth silent world corruption; apply admission backpressure instead.
- Serialize live mutable chunks on a worker: rejected because concurrent mutations can produce torn snapshots; issue an immutable revisioned snapshot on the simulation thread.
- Permanent raw chunk references for plugins/entities: rejected because they defeat unload, epoch validation, and owner diagnostics.
- Persist only player-edit overlays: deferred to `WORLD-03`; it can reduce bytes but does not replace revision acknowledgement or crash-consistent lifecycle gates.
- Region-local independent lifecycle registries: deferred until `WORLD-08` demonstrates a measured need for parallel region ownership.
