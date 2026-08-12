# WORLD-02 Chunk job scheduling and safe publication

Status: Proposed

Owner: VibeCraft architecture research
Date researched: 2026-08-09
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Use a bounded, priority-aware scheduler with a small fixed CPU worker pool for chunk loading/decoding and deterministic generation stages; workers produce immutable results, and the authoritative simulation thread alone validates and publishes those results at tick boundaries.

One-sentence rationale: This captures nearly all useful chunk-generation parallelism while keeping queue growth, cancellation, memory, worldgen reproducibility, and live-world mutation understandable.

This recommendation deliberately interprets “threaded chunk gen” as **parallel materialization with single-writer publication**, not permission for generator code or Godot objects to mutate the live world from arbitrary threads.

## Context and constraints

- The dedicated C# server is authoritative and must remain responsive while players move, teleport, or request unexplored terrain.
- Singleplayer uses the same server architecture, so defaults must work on four-to-eight-core consumer machines as well as dedicated hosts.
- A generated chunk may depend on neighboring chunks or a halo for structures, terrain continuity, lighting inputs, and future generator stages. Workers must never synchronously wait for other workers from the same bounded pool.
- Generation must be reproducible from world seed, generator version, dimension, chunk coordinate, and stage; worker count and completion order must not affect bytes or gameplay.
- Interactive loads, simulation dependencies, reconnects, prefetch, teleport, and administrative pregeneration compete for the same finite CPU, I/O, and memory.
- Cancellation is advisory once a job is running, but stale work must never be published into a chunk whose demand, generation epoch, or base revision changed.
- The server is plain C#; Godot's scene tree is a client concern. Any shared client implementation must still obey Godot's main-thread boundary.
- Chunk geometry and vertical subdivision are owned by `WORLD-01`; generator algorithms/versioning are owned by `WORLD-06`; persistence and recovery are owned by `WORLD-03`/`WORLD-04`.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| A. One `Task.Run` per requested chunk, unbounded FIFO | Very small initial implementation; uses the .NET pool | No backpressure, no useful cancellation/reprioritization, duplicate loads, ThreadPool interference, nondeterministic publication, runaway scratch memory | Reject |
| B. Bounded priority scheduler, fixed workers, immutable results, simulation-thread commit | Bounded cost; deterministic publication; deduplication; explicit overload behavior; testable | Requires a scheduler, job state machine, memory accounting, and pure generator stages | **Recommend** |
| C. Region actors own chunks and execute generation plus live simulation | Natural ownership boundary and a path to parallel ticking | Cross-region structures and interactions become messages; repartitioning, plugins, and deterministic ordering become much harder | Defer until a measured need exists |
| D. Synchronous generation on the simulation thread plus offline pregeneration | Simplest correctness story | Exploration and teleports stall the server; not acceptable for an infinite multiplayer world | Keep only as a deterministic test mode |

## Evidence

### Minecraft

Minecraft's source is not published as a normal open-source repository. The following class inventories are generated from Mojang binaries and official/community mappings, so they are **secondary implementation evidence**, not an official architecture statement.

- Java 1.21.8's mapped `ChunkMap` exposes separate pending generation tasks, world-generation and light task dispatchers, a main-thread executor, ticket storage, pending unloads, active writes, and stage-oriented generation methods. That is strong evidence that modern Minecraft treats materialization as staged asynchronous work whose lifecycle is reconciled by server-owned state, rather than as “call generator on any thread and insert directly.” ([mapped `ChunkMap`](https://mappings.dev/1.21.8/net/minecraft/server/level/ChunkMap.html))
- Java 1.21.1's mapped `ChunkHolder` has separate futures for accessible, block-ticking, and entity-ticking states plus a save dependency. These are capability/lifecycle gates rather than a single loaded boolean. ([mapped `ChunkHolder`](https://mappings.dev/1.21.1/net/minecraft/server/level/ChunkHolder.html))
- Java 1.21.1's distance manager keeps player locations, chunk tickets, simulation distance, a worker executor, a main-thread executor, and explicit checks for block/entity ticking range. This is useful precedent for deriving demand centrally instead of letting every subsystem enqueue arbitrary work. ([mapped `DistanceManager`](https://mappings.dev/1.21.1/net/minecraft/server/level/DistanceManager.html))

Inference for VibeCraft: Minecraft's exact machinery is too complex to copy for v1, but its separation of demand, staged materialization, capabilities, and main-thread reconciliation is the right shape.

### Luanti

- Luanti's current emerge manager creates dedicated generation threads and chooses the least-loaded worker. Its default calculation reserves two processors, caps according to RAM using a conservative 1 GiB per worker, and caps at four because project testing found more than four did not improve speed while the implementation remained lock-heavy. ([`EmergeManager::initThreads`](https://github.com/luanti-org/luanti/blob/master/src/emerge.cpp#L533-L569))
- Luanti globally deduplicates queued coordinates and enforces total, per-peer generation, and disk-only queue limits; active-block requests are prevented from consuming more than half the total queue. This is direct evidence that a voxel server needs admission control by source, not merely a concurrent queue. ([queue admission source](https://github.com/luanti-org/luanti/blob/master/src/emerge.cpp#L653-L706))
- Pending emerge items have an explicit cancelled completion state. The same source also shows live map lookup/publication guarded by a server environment lock, illustrating how worker parallelism can collapse around shared mutable publication. ([cancellation and load/generation boundary](https://github.com/luanti-org/luanti/blob/master/src/emerge.cpp#L750-L817))

Lesson: dedicated workers, deduplication, source quotas, and completion status are proven useful; lock-heavy access to a shared live map and simplistic per-worker FIFO distribution are limits not to inherit.

### Minestom

- Minestom's current `InstanceContainer` stores a single `CompletableFuture` per in-progress chunk coordinate, rechecks the cache inside `computeIfAbsent`, and removes the exact future on completion. Its comments document reentrant-loader hazards and the need to keep completion from occurring inside the map computation. ([load deduplication and race handling](https://github.com/Minestom/Minestom/blob/master/src/main/java/net/minestom/server/instance/InstanceContainer.java#L308-L360))
- The same implementation performs load/generation on a virtual thread when the loader opts into parallel operation, but contains a `TODO` asking whether cache/publication should happen on the instance thread. That TODO is precisely the boundary VibeCraft should specify up front. ([load and publication chain](https://github.com/Minestom/Minestom/blob/master/src/main/java/net/minestom/server/instance/InstanceContainer.java#L318-L355))
- Minestom's July 2026 release notes call out fixes so concurrent requests share one in-progress load and completed/failed entries are removed safely, demonstrating that seemingly small load registries are race-prone in practice. ([Minestom releases](https://github.com/Minestom/Minestom/releases))

Lesson: one shared result per coordinate is correct, but “the future completed” must not itself grant permission to mutate authoritative state.

### Veloren

- An early Veloren issue identified the essential requirements for chunk request management: distance priority, limits on generation and sends, rejecting client spam, avoiding duplicate sends, and removing chunks no longer visible. ([Veloren issue #81](https://gitlab.com/veloren/veloren/-/issues/81))
- A later client issue records a limit of four ongoing chunk requests and proposes not scanning while the limit is full and spreading scanning over multiple ticks. This is evidence that backpressure must propagate to demand discovery, not merely accumulate behind the worker queue. ([Veloren issue #430](https://gitlab.com/veloren/veloren/-/issues/430))
- Veloren 0.5.0 explicitly added cancellable chunk generation and parallelized significant world-generation stages. ([Veloren 0.5.0 release](https://gitlab.com/veloren/veloren/-/releases/v0.5.0))

Lesson: prioritize and bound work at admission, and make cancellation part of the job protocol from the beginning.

### C#, .NET, and Godot constraints

- .NET bounded channels provide producer/consumer coordination and backpressure, with explicit full modes. They are suitable for bounded completion and I/O lanes, although VibeCraft still needs an indexed priority heap for reprioritizable generation jobs. ([Microsoft `System.Threading.Channels` documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels))
- Arrays at or above 85,000 bytes normally enter .NET's large object heap; Microsoft recommends reusing pools of large objects when temporary large arrays would create GC pressure. Chunk scratch buffers therefore need ownership and a separate byte budget, not only a worker-count limit. ([Microsoft LOH guidance](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/large-object-heap))
- Godot documents that the active scene tree is not thread-safe and that scene-tree changes should be deferred to the main thread; loading or modifying shared resources from several threads can also behave unexpectedly. Server chunk data must not contain `Node`, `Resource`, `GodotObject`, mesh, rendering, or physics-server handles. ([Godot thread-safe API guidance](https://docs.godotengine.org/en/4.5/tutorials/performance/thread_safe_apis.html))

## Proposed design

### Ownership rule

There is exactly one authoritative writer for a world: its simulation thread. Worker jobs may read immutable configuration and immutable snapshots. They may allocate and mutate job-private buffers. They may not read or write resident chunk objects, entity stores, plugin state, registries that can reload, or network/client objects.

The client may reuse generator code for previews or singleplayer visuals only if inputs and outputs remain engine-neutral data. It must publish Godot nodes/resources separately on the Godot main thread.

### Public server interfaces

Names are illustrative but the semantics are required:

```csharp
public enum ChunkDemandClass : byte
{
    RequiredNow,      // player spawn/teleport or collision-critical dependency
    SimulationSoon,   // needed to enter a ticking tier
    NetworkSoon,      // inside near send radius
    Prefetch,         // predicted movement / outer send radius
    Batch             // pregeneration or maintenance
}

public readonly record struct ChunkJobKey(
    SectionKey Section,
    GeneratorVersion Generator,
    ChunkStageId TargetStage,
    ulong MaterializationEpoch);

public readonly record struct ChunkDemand(
    SectionKey Section,
    ChunkStageId TargetStage,
    ChunkDemandClass Class,
    int Distance,
    DemandOwner Owner);

public interface IChunkLease : IDisposable
{
    SectionKey Section { get; }
    bool IsSatisfied { get; }
    ValueTask<ChunkMaterializationOutcome> Completion { get; }
}

public interface IChunkJobScheduler
{
    IChunkLease Acquire(in ChunkDemand demand);
    ChunkSchedulerSnapshot GetMetrics();
}
```

`Acquire` is called on the simulation thread. Multiple demands for the same job share one job record and completion; disposing a lease removes only that owner's demand. Plugins never receive `CancellationTokenSource`, worker tasks, mutable chunks, or scheduler internals.

### Job state and stages

```text
Missing
  -> Queued
  -> Reading -> Decoding -----------+
  -> Generating(stage N) -> ... ----+-> ReadyToCommit -> Resident
                         \-> Cancelled
                         \-> Faulted
```

- Storage lookup decides `Reading` versus `Generating`; an error reading existing data is **not** permission to silently regenerate and erase player changes.
- Generator stages declare their input stage, neighbor halo, scratch estimate, and whether they are pure. The scheduler creates dependency edges and schedules prerequisites; a worker never blocks on `.Wait()`, `.Result`, or `.GetAwaiter().GetResult()` for another chunk job.
- Stage keys and dependency order are opaque profile data owned by `WORLD-06`; the scheduler API never hard-codes generator algorithms. The first playable may expose one target-section generator stage, while the later profile currently proposes `base_density`, `biomes`, `carvers`, `surface`, `structure_plans`, `structure_raster`, `local_features`, and `finalize`.
- Cross-section structures use `WORLD-07`'s persisted coordinate-owned plans plus clipped target-`SectionKey` patches/receipts. They are not unordered deferred neighbor writes and never mutate a neighboring resident from a worker.

### Determinism and generation inputs

- Each stage receives an immutable `WorldgenContext` snapshot containing world seed, generator version/config hash, dimension ID, registry snapshot ID, chunk key, and dependency results.
- Stage randomness is derived by the pinned `WORLD-06` keyed-stream contract over world/profile identity, full signed-64-bit 3D `SectionKey` (`dimension,x,y,z`), stage/feature keys, origin coordinates, and attempt index. No stage shares mutable RNG state or consumes randomness based on worker completion order.
- A stage output is a sealed immutable object or an ownership-transferred buffer graph. After enqueueing completion, the worker must not retain or mutate any output reference.
- Publication is deterministic: at the beginning of a simulation tick, ready results are sorted by `(dimension stable ID, chunk X, chunk Z, stage ordinal, job creation sequence)` before validation and commit.
- Commit validates the job key's materialization epoch, generator/config hash, expected base revision, and current demand. A stale result is disposed without side effects.
- Determinism tests run with synchronous mode and with 1, 2, and 4 workers and compare canonical chunk hashes and deferred-write order.

### Priority, deduplication, and backpressure

- Keep one indexed job record per `ChunkJobKey`; update its effective priority when leases appear, move, or disappear.
- Effective priority is `(demand class, minimum distance among leases, oldest request tick, stable chunk key)`. Recompute dirty priorities once per simulation tick, not from worker threads.
- Jobs older than two seconds are promoted one class per two seconds, capped at `SimulationSoon`. `Batch` may age only to `Prefetch`; pregeneration must yield indefinitely to interactive work when necessary.
- Default total queued-job capacity is 2,048 deduplicated job records. The last 256 slots are reserved for `RequiredNow` and dependency jobs. These are provisional defaults, exposed in configuration and validated by the prototype.
- At capacity, reject or cancel the farthest `Prefetch`, then oldest `Batch`, then farthest `NetworkSoon`; never evict running jobs or accepted `RequiredNow`. The demand system retries rejected soft work on a later tick rather than retaining a second hidden queue.
- A single client cannot create jobs directly. Interest management converts validated player position/capability into server-side leases and caps each player's contribution; `NET-05` owns exact radii and network send policy.
- Administrative pregeneration uses `Batch`, has an explicit chunks-per-second cap, and pauses while queue occupancy exceeds 50% or simulation tick p95 exceeds budget.

### Worker and memory defaults

- CPU generation workers default to `clamp(Environment.ProcessorCount - 2, 1, 4)`. The cap follows Luanti's measured experience but remains a benchmarked default, not a universal truth.
- Storage reads are asynchronous and bounded separately to eight in-flight operations; decompression/decoding consumes CPU-worker and scratch-budget tokens. Writes use the persistence scheduler from `WORLD-04`/`WORLD-05` and cannot be starved by world generation.
- Generation scratch has a byte semaphore with a default limit of `min(512 MiB, 12.5% of GC-reported total available memory)`. A job declares its conservative maximum before dispatch. A worker that cannot acquire the bytes does not start that job and tries another eligible job.
- Pooled buffers have one explicit owner. Returning a buffer revokes all references; this follows the `ArrayPool<T>` ownership requirement, which warns that use-after-return or double-return can corrupt or disclose data. ([Microsoft `ArrayPool<T>.Return` contract](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1.return))
- Do not use the general .NET ThreadPool for sustained CPU generation. Start named long-running worker loops so networking, timers, persistence continuations, and plugin asynchronous work cannot be starved by exploration.

### Cancellation and failure behavior

- Removing the last demand cancels queued work immediately and signals a running job's token. A dependency lease keeps prerequisite work alive even if the original user lease disappears.
- Generator code checks cancellation at stage boundaries and at least every 4,096 voxel/feature operations. It need not roll back private buffers; disposal is the rollback.
- Completed cancelled work may enter a cold cache only if the chunk lifecycle manager explicitly accepts it under budget. It may never auto-publish merely because the expensive work finished.
- I/O failures retry three times with exponential delays of 100 ms, 500 ms, and 2 s, then enter `Faulted`. Deterministic generator exceptions do not retry in a loop; they record generator version, chunk, stage, seed hash, and exception, and fail all current leases.
- A `RequiredNow` fault rejects the spawn/teleport with an actionable error. A `Prefetch` fault is logged and suppressed from repeated requests for 30 seconds. Existing-data corruption follows `WORLD-04` recovery policy and is never replaced automatically.
- Worker death is fatal to the scheduler: stop admission, cancel remaining jobs, and begin controlled server shutdown. Silently reducing worker count can leave a corrupted plugin/generator process alive.

### Required metrics

- Queue length and oldest age by demand class and stage.
- Enqueue, dedup-hit, promotion, cancellation-before-start, cancellation-during-run, stale-result, fault, and commit counts.
- Wait/run/commit duration histograms by stage; worker utilization; scratch bytes reserved/peak; I/O concurrency.
- Per-owner admitted and rejected demand, especially player and plugin owners.
- Determinism hash mismatch counter, which must remain zero outside explicit generator-version changes.

## Greenlight criteria

- The same pinned generator profile and request trace produces byte-identical finalized section hashes, structure plans, and target patches across 20 runs at worker counts 1, 2, and 4.
- Queue records and scratch bytes remain below configured hard limits under sustained teleport, disconnect, and pregeneration churn; there is no hidden unbounded continuation/task collection.
- A cancelled or superseded job never becomes resident, and duplicate demand causes one load/generation execution.
- On a machine with at least eight logical processors and a generator fixture costing 50–100 ms per chunk, two workers achieve at least 1.7× single-worker throughput and four achieve at least 2.5× without pushing the fixed 60 TPS simulation step above its 16.67 ms p99 capacity gate.
- `RequiredNow` jobs admitted under a saturated prefetch/batch queue begin execution within 100 ms after a worker is available.
- Generator, decoder, and completion paths contain no Godot types and no access to resident mutable chunks.
- Fault injection for read errors, generator exceptions, cancellation, worker shutdown, and stale epochs produces the documented outcome with no silent regeneration.

## Prototype or benchmark

Required: yes

Smallest useful experiment: Build an engine-neutral C# harness implementing the scheduler, a six-stage deterministic synthetic generator, dependency halos, pooled scratch buffers, load deduplication, cancellation, and simulation-thread commit. Replay traces for steady walking, sprinting/turning, repeated long teleports, four players diverging, disconnect while generating, and pregeneration under load.

Success metrics:

- Run the greenlight criteria above for worker counts 1/2/4/8 and record CPU, allocation rate, LOH size, queue age, scratch peak, throughput, p50/p95/p99 latency, and simulation-loop jitter.
- After 10,000 demand changes, all lease/job/buffer counts return to baseline plus intentionally cached residents.
- Deliberately complete jobs in reversed/random order; committed hashes and ordering remain identical.
- Inject cancellation at every stage and storage/generator exceptions at every boundary; no task remains incomplete and every pooled buffer is returned exactly once.

Failure rule: If any hard bound, publication-safety check, or determinism comparison fails, do not greenlight threaded materialization; keep the synchronous test path and revise the scheduler or generator contract.

## Risks and open questions

- Generator stages and halo widths are not yet defined; a generator that frequently writes across large radii can explode dependency count despite a bounded root queue. `WORLD-06` must impose bounded, declared halos.
- A 2,048-job queue and four-worker default are evidence-informed starting values, not capacity promises. The benchmark must tune them for the chosen chunk dimensions and generator.
- Compression/decompression may dominate CPU and should be measured as separate stages; do not create an additional unbounded I/O continuation graph.
- Plugin-provided worldgen can violate purity, cancellation, scratch estimates, or deterministic randomness. The plugin boundary must reject worker execution unless a generator opts into a restricted worldgen API and passes determinism tests.
- Very fast travel can still outrun generation. Correct overload behavior is to reduce prefetch/send radius and delay teleport completion, not to exceed memory bounds.
- Caching completed-but-undemanded output is a lifecycle decision; default to disposal until `WORLD-05` defines an accounted cold cache.

## Dependencies

- Requires: `WORLD-01` chunk/section representation; `WORLD-03` storage API; `WORLD-04` read recovery; `WORLD-06` deterministic generator stages and versioning; `ARCH-01` authoritative ownership rule.
- Coordinates with: `WORLD-05` lifecycle/memory leases; `WORLD-07` structure writes; `NET-05` interest management; `RENDER-02` client mesh jobs; `ARCH-05` plugin execution boundary.
- Blocks: implementation of runtime chunk generation, prefetch, teleport/spawn materialization, and any claim of “threaded chunk gen.”

## Rejected or deferred alternatives

- Unbounded `Task.Run`/`Parallel.ForEach`: rejected because worker count does not bound queued continuations, duplicate demand, or scratch memory.
- Worker mutation of live chunks behind per-chunk locks: rejected for v1 because cross-chunk stages require lock ordering, cancellation cannot roll back partial publication, and plugin callbacks can escape the lock discipline.
- Blocking dependency waits inside generation workers: rejected because a bounded pool can deadlock when every worker waits for queued prerequisites.
- One FIFO queue: rejected because exploration and pregeneration can delay collision-critical spawn/teleport work.
- Work stealing without central admission accounting: deferred; local deques may improve locality later, but the central job registry and byte budget remain authoritative.
- Folia-style region ownership for generation and simulation: deferred to `WORLD-08`; it is a different programming model, not a scheduler optimization.
