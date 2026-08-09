# WORLD-08 Deterministic ticking and activation

Status: Proposed

Owner: VibeCraft architecture research
Date researched: 2026-08-09
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Run authoritative world gameplay on one deterministic 20 Hz simulation thread per world, activate resident chunks through explicit block-ticking and entity-ticking tiers, store block/fluid ticks in bounded ordered per-chunk schedulers, and keep generation, storage, meshing, and carefully validated read-only computations parallel while deferring parallel live chunk ticking.

One-sentence rationale: A fixed single-writer tick gives early-Minecraft-like mechanics, reproducible ordering, safe plugin semantics, and a credible first implementation; Folia-style parallel regions are an architectural migration, not a worker-pool toggle.

This decision explicitly challenges two requirements in the current spec:

1. “32/64/128 server ticks” is rejected as a v1 authoritative profile menu. `NET-06` owns measured input/snapshot packet cadence, not another simulation clock. Minecraft-like gameplay is designed around a 20 Hz game clock; running the whole world at 128 Hz multiplies work and changes delays by 6.4x unless every rule is rescaled.
2. “Threaded chunk ticking” is overambitious for the first playable release. Correct parallel live mutation requires region ownership, cross-region message ordering, migration, plugin restrictions, and deterministic conflict rules. Parallel materialization from `WORLD-02` is approved; parallel authoritative mutation is deferred until profiling proves it necessary.

## Context and constraints

- The dedicated server is authoritative, and singleplayer uses the same simulation. Client render frames and local prediction do not advance world truth.
- Early-Minecraft-like behavior includes scheduled block/fluid work, random block ticks, block entities, entities/AI/physics, redstone/neighbor propagation, spawning, weather, and world time. These systems need explicit order and overload behavior.
- Loaded, block-ticking, and entity-ticking are separate capabilities supplied by `WORLD-05`; network visibility does not imply simulation activity.
- Distant chunks must not consume full simulation CPU. Their state still has to persist without replaying millions of random ticks when a player returns.
- Cross-chunk interactions are normal: fluids, neighbor updates, pistons, entities, and structures cross boundaries. Arbitrary per-chunk parallelism would turn each one into a race or distributed transaction.
- Plugins must observe a stable event order and may not mutate the live world from asynchronous tasks. Slow plugins are an isolation/administration problem under `ARCH-05`, not a reason to make mutation concurrent.
- Tick backlog and scheduled queues must be bounded by operation and memory budgets. Wall-clock cutoffs cannot decide which authoritative operations occur, because machine speed would then change simulation results.
- Vertical world extent is unbounded in address space, but random ticking and active iteration operate only over finite resident sections defined by `WORLD-01`.
- Exact player physics and network frequencies belong to `NET-02`/`NET-06`; this decision owns the world game clock and server-side application order.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| A. Tick all loaded chunks in parallel at configurable 32/64/128 Hz | Superficially matches the spec; high update frequency on powerful machines | Changes Minecraft-like timing, multiplies cost, races cross-chunk effects, makes plugin/thread safety and deterministic replay extremely difficult | Reject |
| B. One 20 Hz simulation writer, capability tiers, deterministic bounded schedulers | Smallest correct architecture; stable ordering; easy replay and persistence; compatible with parallel I/O/worldgen | One world eventually reaches a single-thread CPU ceiling; slow callbacks can stall it | **Recommend** |
| C. Folia-style independent region tick loops with strict ownership | Real multicore live simulation for geographically separated players | Region merge/split, entity migration, cross-boundary messages, global systems, plugin APIs, and debugging become much more complex | Defer behind profiling gate |
| D. Parallel per-system snapshot jobs returning command buffers to one commit thread | Can accelerate selected AI/pathfinding or broad read-only scans while retaining one writer | Snapshot cost, stale results, deterministic conflict resolution, and one-tick latency | Permit only case-by-case after the base loop is measured |

## Evidence

### Minecraft: modern implementation and historical behavior

Minecraft's source is not published as a normal open-source repository. Mapped class inventories are generated from Mojang binaries and official/community mappings, so they are **secondary implementation evidence**. Community wiki evidence is labeled as such.

- Java 1.21.8's mapped `ServerLevel` has distinct block and fluid tick containers, an entity tick list, random-tick handling, and a named `MAX_SCHEDULED_TICKS_PER_TICK`; its methods separately tick chunks, blocks, fluids, and entities. ([mapped `ServerLevel`](https://mappings.dev/1.21.8/net/minecraft/server/level/ServerLevel.html))
- Java 1.21.3's mapped `LevelTicks` keeps per-chunk tick containers, each chunk's next trigger time, queues/ordered collections for the current tick, and a caller-supplied maximum tick count. This is strong evidence for per-chunk persistence/locality plus a bounded global due-work pass, not a scan of every scheduled entry. ([mapped `LevelTicks`](https://mappings.dev/1.21.3/net/minecraft/world/ticks/LevelTicks.html))
- Yarn's Java 1.21 `Tick` record carries a type, position, delay, priority, and sub-tick order and supports NBT serialization. Persisting both priority and stable order is important for restart-equivalent behavior. ([Yarn `Tick`](https://maven.fabricmc.net/docs/yarn-1.21%2Bbuild.1/net/minecraft/world/tick/Tick.html))
- Java 1.21.1's mapped `ChunkHolder` exposes full, block-ticking, and entity-ticking futures; the mapped `DistanceManager` separately decides whether blocks and entities should tick. ([mapped `ChunkHolder`](https://mappings.dev/1.21.1/net/minecraft/server/level/ChunkHolder.html), [mapped `DistanceManager`](https://mappings.dev/1.21.1/net/minecraft/server/level/DistanceManager.html))
- Paper's official server documentation says `simulation-distance` controls the radius in which living entities are updated, while its Spigot configuration documents entity activation ranges outside which entities tick less often. Paper also exposes per-world ticking and autosave controls. ([Paper server properties](https://docs.papermc.io/paper/reference/server-properties/), [Paper entity activation configuration](https://docs.papermc.io/paper/reference/spigot-configuration/), [Paper world configuration](https://docs.papermc.io/paper/reference/world-configuration/))
- Community-maintained Minecraft documentation records the long-standing target of 20 game ticks per second and a 65,536 scheduled block/fluid tick processing ceiling. It also records that Java 1.21.5 changed random-tick reach to follow simulation distance for blocks, while some systems retained different ranges. These details show that activation policy has evolved independently of the base 20 Hz clock. ([community tick documentation](https://minecraft.fandom.com/wiki/Tick), [community Java 1.21.5 notes](https://minecraft.wiki/w/Java_Edition_1.21.5))

Inference for VibeCraft: copy the semantic shape—20 Hz logical time, capability tiers, ordered persisted scheduled ticks, and a deterministic work ceiling—not undocumented Java quirks or the full ticket implementation. Exact redstone compatibility needs gameplay tests, not architecture folklore.

### Folia and Paper

- Folia does not tick arbitrary chunks independently. Its official overview says loaded chunks are grouped into independent regions, adjacent ticking regions are forbidden, each region owns region-local data and its own tick counter, and cross-region work is communicated through queues. ([Folia overview](https://docs.papermc.io/folia/reference/overview/))
- Region topology is dynamic: regions can merge and split, and the region implementation has transient, ready, ticking, and dead states. ([Folia region logic](https://docs.papermc.io/folia/reference/region-logic/))
- Folia's repository warns that plugin compatibility starts at zero, requires strict region ownership, recommends at least 16 cores, and recommends pregenerated worlds for best results. Paper exposes separate global, region, asynchronous, and entity schedulers to plugins that support both models. ([Folia repository](https://github.com/PaperMC/Folia), [Paper Folia-support guidance](https://docs.papermc.io/paper/dev/folia-support/))

Lesson: region parallelism can scale geographically separated populations, but it replaces the programming model. VibeCraft should preserve a future region boundary in interfaces while declining the operational complexity for v1.

### Luanti

- Luanti's API states that active block modifiers are run periodically only for active blocks, while loading block modifiers run as mapblocks become active. ([Luanti ABM/LBM definitions](https://api.luanti.org/definition-tables/))
- Luanti's server environment computes active blocks around players, removes distant blocks, deactivates far objects, activates newly included blocks, and steps node timers in active blocks. ([Luanti `serverenvironment.cpp`](https://github.com/luanti-org/luanti/blob/master/src/serverenvironment.cpp))
- Luanti's lifecycle API distinguishes emerging, loaded-but-inactive, and active blocks. ([Luanti core namespace reference](https://api.luanti.org/core-namespace-reference/))

Lesson: activation is a first-class CPU policy distinct from residency. VibeCraft should avoid one generic periodic callback system, however: block/fluid ordering, random ticks, entities, and plugin timers need separate semantics and quotas.

### Minestom

- Minestom's `ThreadDispatcher` partitions tickable elements such as chunks, dispatches partitions to configurable worker threads, then uses an `updateAndAwait` barrier; ownership updates are drained at the start of a tick. ([Minestom `ThreadDispatcher`](https://raw.githubusercontent.com/Minestom/Minestom/master/src/main/java/net/minestom/server/thread/ThreadDispatcher.java))
- Minestom's scheduler implementation uses ordered tick state and separate queues for newly submitted tasks, tick-start work, tick-end work, and temporary work. ([Minestom `SchedulerImpl`](https://raw.githubusercontent.com/Minestom/Minestom/master/src/main/java/net/minestom/server/timer/SchedulerImpl.java))

Lesson: a barrier and ownership-update boundary can make threaded ticking tractable, but VibeCraft would still need cross-chunk ownership and plugin rules. The ordered phase queues are useful even in the recommended single-writer design.

### Terasology

- Terasology's entity-system documentation describes systems that run each frame or react to events, while its event documentation defines priority ordering and consumption. ([Terasology entity systems](https://metaterasology.github.io/docs/concepts/entitySystem.html), [Terasology events](https://metaterasology.github.io/docs/developing/entitySystem/events.html))
- Terasology uses finite `32x32x64` chunks despite a world limited primarily by coordinates and memory. ([Terasology block-world concepts](https://metaterasology.github.io/docs/concepts/blockWorld.html))

Lesson: phase/system decomposition and ordered events are extensible, but “every frame” is not a sufficient server timing contract. VibeCraft needs logical ticks, activation, persistence, and deterministic budgets around systems.

## Proposed design

### Clock and overload semantics

- `WorldTick` is a persisted unsigned 64-bit logical counter advancing at a fixed **20 Hz** (`50 ms` nominal duration). Gameplay delays are expressed in world ticks, not milliseconds.
- A monotonic clock drives an accumulator. The loop may execute at most three catch-up ticks consecutively before yielding to network/I/O completions. It never skips a logical world tick; if permanently overloaded, game time runs slow and the server reports tick debt rather than changing simulation results based on wall time.
- At increasing debt, pause pregeneration/prefetch, reduce optional network work, suppress non-authoritative diagnostics, and expose overload. Do not drop scheduled gameplay operations, randomize ordering, or run multiple world ticks concurrently.
- Network receive remains asynchronous. Inputs are stamped and consumed at a tick boundary. `NET-06` begins with no more than one input bundle and one coalescible snapshot per world tick; extra packets never advance movement commits, world AI, redstone, fluids, or block time.
- Pause is an `ARCH-04` policy. If singleplayer pauses, no `WorldTick` advances; server downtime likewise does not advance logical time unless a specific gameplay system defines analytical offline progress.

### Activation tiers

`WORLD-05` leases derive one capability per resident finite chunk unit:

```csharp
public enum ChunkCapability : byte
{
    Resident,       // data available; no ordinary world simulation
    BlockTicking,   // scheduled block/fluid ticks, random ticks, block entities
    EntityTicking   // BlockTicking plus entities, AI, physics, spawning
}
```

- Player simulation leases require `EntityTicking` for the server-authoritative radius selected by `NET-05`. Network-only interest may require only `Resident`.
- Connected players always tick. A player cannot be committed into an unsatisfied chunk; movement/teleport waits at the materialization boundary rather than reading missing collision data.
- Short-lived continuity-critical entities such as projectiles may acquire small, expiring entity-ticking leases as they cross a boundary. This is explicit and quota-accounted; ordinary distant items do not keep arbitrary regions alive.
- Desired promotions apply at the next tick boundary. Capability downgrades have a provisional 40-tick (2-second) grace to avoid CPU thrash at the radius edge; `WORLD-05`'s resident unload grace is independently 200 ticks (10 seconds). Overload may remove optional grace deterministically.
- Deactivated chunks retain their scheduled queues and block/entity state. Random ticks, AI, physics, and ordinary block-entity ticking freeze. On reactivation, overdue scheduled ticks become eligible under normal budgets; missed random ticks and AI frames are **not replayed**.
- Systems intentionally based on elapsed wall time—if any are approved later—store a timestamp and analytically calculate one transition on activation. They must not synthesize one callback per missed tick.
- Activation sets are immutable for the duration of a tick and iterated in canonical `(dimension stable ID, chunk X, section Y, chunk Z)` order. Promotions/demotions discovered during a tick apply at the following boundary.

### Scheduled block and fluid ticks

Use separate generic queues for block and fluid ticks, sharing the same ordering contract:

```csharp
public enum ScheduledTickPriority : sbyte
{
    ExtremelyHigh = -3,
    VeryHigh = -2,
    High = -1,
    Normal = 0,
    Low = 1,
    VeryLow = 2,
    ExtremelyLow = 3
}

public readonly record struct ScheduledWorldTick(
    ulong DueTick,
    ScheduledTickPriority Priority,
    ulong Sequence,
    BlockPosition Position,
    RegistryId ExpectedType);

public enum TickScheduleResult : byte
{
    Accepted,
    Coalesced,
    RejectedCapacity,
    ChunkNotResident
}
```

- Each resident unit owns an indexed min-heap ordered by `(DueTick, Priority, Sequence)`. A world-level heap indexes only each active chunk queue's next due item, ordered by `(DueTick, Priority, Sequence, stable chunk key)`, so the server does not scan all chunks or all future ticks.
- The simulation thread assigns `Sequence`; worker completion order and dictionary insertion never assign gameplay order. Sequence and due tick are persisted with the chunk.
- A deduplication index keyed by `(position, expected type, block-or-fluid queue)` coalesces duplicate future ticks. An earlier due tick replaces a later one; equal due ticks retain the stronger priority and original stable sequence.
- Execution rechecks `ExpectedType`; replacing the block/fluid makes an obsolete scheduled tick a deterministic no-op.
- `DueTick` is absolute logical world time. World metadata and queue records are saved together under `WORLD-04`'s generation/commit rule. Since logical time does not advance while the world is closed, restart does not create artificial offline backlog.
- Initial deterministic processing limits are 65,536 block ticks and 65,536 fluid ticks per world tick, matching the documented modern-Minecraft scale but exposed as world-creation compatibility settings. Due work beyond the limit remains ordered and overdue; it is never dropped. The prototype must lower these defaults if the chosen block API cannot keep a tick within budget.
- Pending entries are charged to the `WORLD-05` memory ledger. Provisional caps are 262,144 entries per chunk unit and a configured world byte budget. External player/plugin transactions that cannot reserve required tick entries fail atomically with an explicit capacity result. Built-in simulation must propagate scheduling failure as an invariant/overload condition, never silently omit a required future tick.

### Immediate block, neighbor, and redstone updates

- Immediate consequences use a simulation-thread FIFO command queue; block code never recursively calls arbitrary neighboring block/plugin code.
- Each root mutation receives a stable root sequence. Children are ordered by a documented face order and child ordinal, producing `(root sequence, depth, parent sequence, face ordinal)` ordering across chunks.
- Before executing an update, reserve its declared maximum fanout in the queue. If capacity is temporarily unavailable, leave the producer update at the head for the next world tick instead of partially mutating and losing children.
- Start with a 100,000-update execution cap and 200,000 queued-update cap per world tick/world. Overflow is carried forward in exact order and reported. These are safety defaults, not a claim of Java redstone parity; `GAME-02` must benchmark contraptions and define compatibility behavior.
- Built-in block handlers must be non-blocking, deterministic, and bounded in fanout. Plugin callbacks observe events and return commands within the same simulation phase; asynchronous plugin work may only submit future-tick commands with revision preconditions.
- Cross-chunk updates are ordinary commands because v1 has one writer, but all accesses still resolve generation-stamped handles. This preserves a migration seam for future region messages and prevents an update from resurrecting an unloaded neighbor.

### Random ticks, block entities, entities, and global systems

- Random ticking visits active resident sections in canonical key order. The compatibility default is three attempts per nonempty 16-block-high section per world tick, configurable as `RandomTickSpeed`; `WORLD-01` maps its actual section geometry to deterministic 16-high sampling bands. Community Minecraft documentation is the available evidence for the default value, so it must be verified with gameplay tests rather than treated as a primary specification. ([community random-tick discussion](https://minecraft.wiki/w/Talk%3ATick))
- Random choices use a counter-derived stream keyed by `(world seed, generator/registry compatibility ID, WorldTick, section key, attempt index)`. Adding a worker, entity, or unrelated random system cannot shift block-random outcomes.
- Block entities tick in stable `(chunk key, local position)` order after scheduled/random block effects. Registration/removal commands are staged and apply at a phase boundary, so iteration is never modified in place.
- Entities tick only in `EntityTicking` chunks, in stable entity-ID order within deterministic system phases: player/control, physics, ordinary behavior/AI, item/projectile effects, and migration. Exact phase content is owned by gameplay documents, but changing it is a compatibility-version change.
- Entity migration is committed after entity iteration. Destination capability is acquired before commit; otherwise the entity remains at the boundary or follows its type-specific failure rule. It never mutates two chunk entity stores concurrently.
- VibeCraft v1 does not implement Paper-style per-entity “tick less often” heuristics. Whole-chunk activation is more predictable. Expensive AI sensing may later run on a stable modulo schedule if gameplay explicitly adopts that semantic.
- Weather, world time, sleeping/voting, and other global systems run once per world tick in a fixed phase. They do not require every resident chunk to tick.

### Tick phases and publication boundary

Every tick executes the same barriers:

```text
0. Start barrier
   - drain WORLD-02/load/save completions
   - validate epochs/revisions and publish in stable chunk-key order
   - apply queued lifecycle and activation transitions

1. Inputs and administrative commands
   - sort by (target tick, connection stable ID, per-source sequence)
   - validate; apply accepted world commands

2. Global systems
   - world time, weather, global plugin scheduler

3. Scheduled work
   - due block ticks, due fluid ticks
   - drain resulting immediate updates within deterministic caps

4. Random and block-entity work
   - random ticks; block entities
   - drain resulting immediate updates within the shared cap

5. Entity systems
   - players, physics, AI/behavior, items/projectiles

6. Commit barrier
   - resolve deferred cross-chunk commands and entity migration
   - finalize dirty revisions and next-tick activation demand
   - produce immutable network/save observations and replay hash
```

No worker may publish resident data or invoke a gameplay/plugin callback between these barriers. A completed generator, pathfinding request, storage read, or save acknowledgement is merely an input to phase 0.

### Controlled parallel work and migration path

- Approved parallel work in v1: chunk materialization, compression/storage, client meshing, and pure read-only jobs whose inputs are immutable snapshots and whose outputs are commands.
- A read-only simulation job carries source tick, source revisions, target entity/chunk IDs, and a deterministic command key. Phase 0 or 6 discards stale results and sorts accepted commands before commit. No correctness path may depend on the job finishing by a wall-clock deadline.
- Do not parallelize trivial per-entity loops: snapshot allocation, barriers, stale work, and conflict sorting can cost more than the work.
- Preserve an internal `ISimulationPartition` abstraction only for metrics, key ranges, and command routing; v1 provides exactly one partition per world. Do not expose region-thread promises to plugins.
- Reconsider region ticking only after a representative profile shows authoritative simulation consuming more than 50% of the tick budget at the declared target population after algorithmic and activation optimizations, and a prototype demonstrates at least 1.8x throughput on 16 cores without changing replay results for cross-region fixtures.
- A region prototype must specify ownership, non-adjacent region formation, merge/split, entity migration, global systems, plugin schedulers, shutdown, and cross-region message ordering. Folia shows all of these are part of the feature.

### Deterministic replay and diagnostics

- Never derive authoritative order from `Dictionary`/`HashSet` enumeration, worker completion, thread ID, wall clock, or unstable object hash codes.
- Give chunks, registries, entities, connections, plugin commands, and scheduled work stable IDs/sequences. Sort only at documented phase boundaries; use ordered/intrusive storage in hot loops where sorting would dominate.
- Random systems use independent keyed streams. Plugin randomness comes from a named stream included in the plugin compatibility ID.
- Record accepted external inputs, administrative/plugin commands, compatibility configuration, registry/generator versions, and periodic canonical state hashes. A replay starts from a durable snapshot and consumes this log.
- Required metrics: phase duration and operation count; active chunks by capability; scheduled/overdue queue size and oldest lateness; immediate-update depth/backlog; entities by type/tier; tick debt and catch-up bursts; stale async results; plugin callback cost; replay hash mismatch.

Bitwise cross-platform floating-point replay is not promised by this brief. Block/tick ordering and integer state must be exact; `NET-02` must decide whether movement physics needs fixed point or a narrower supported runtime/platform contract.

## Greenlight criteria

- Twenty replays of the same snapshot and input log, with randomized asynchronous completion order and `WORLD-02` worker counts 1/2/4, produce identical per-tick canonical hashes, scheduled queues, entity IDs, and durable final state.
- Activation changes take effect only at boundaries; no chunk is ticked below its capability, no iterator observes mid-phase membership mutation, and crossing a chunk boundary never reads an absent chunk.
- One million scheduled block/fluid ticks spread across active and inactive chunks execute in exact `(due, priority, sequence, chunk key)` order subject to caps, survive save/reload, and are neither duplicated nor lost.
- An inactive chunk receives no random, AI, physics, or ordinary block-entity ticks. On reactivation, overdue scheduled ticks drain under budget without replaying missed random/AI ticks.
- A sustained update storm carries deterministic backlog without exceeding queue/memory caps or overflowing the call stack; a capacity failure is surfaced to the initiating transaction or built-in invariant handler.
- In a fixture with eight separated players, simulation distance six, 200 simple entities per player, default random tick speed, and representative block entities, phase work remains below 50 ms p99 for 30 minutes on the declared baseline server. If `WORLD-01` makes this fixture unrealistic, replace it before greenlight with an equally explicit capacity target.
- Tick debt, not state loss, is observed when the fixture is intentionally overloaded; optional generation/prefetch is shed before authoritative operations.
- Static analysis/tests prove no Godot object, resident mutable chunk, entity store, or plugin callback is accessed from generation, storage, or read-only worker jobs.

## Prototype or benchmark

Required: yes

Smallest useful experiment: Build an engine-neutral C# simulation harness containing finite chunk sections, `WORLD-05` capability transitions, persisted block/fluid heaps, an immediate-update/redstone queue, random ticks, simple block entities and moving entities, deterministic input/replay logs, and fake asynchronous `WORLD-02` completions.

Success metrics:

- Run all greenlight criteria with asynchronous completions delivered normally, reversed, duplicated, delayed, and randomly.
- Benchmark tick phases at simulation distances 4/6/8, 1/4/8 separated players, 0/50/200 entities per player, and random tick speeds 0/3/10. Record p50/p95/p99, allocation rate, queue size/lateness, active sections, and tick debt.
- Create redstone/fluid fixtures that cross chunk boundaries and activation edges; compare exact event traces before and after save/reload and across 100 seeded runs.
- Pause chunks for 10,000 world ticks, reactivate them, and verify bounded overdue processing with no random/AI catch-up burst.
- Inject a one-million-entry scheduled backlog and a self-propagating immediate-update fixture. Memory remains charged/bounded, no operation disappears, and unrelated chunks make progress according to documented ordering.
- Measure candidate read-only parallel pathfinding/AI snapshots separately. Adopt one only if end-to-end tick p95 improves by at least 20% at target load and stale-result rate remains below 5%; otherwise keep it serial or deferred.

Failure rule: Any replay mismatch, lost due operation, mid-phase ownership violation, unbounded queue, or inability to meet the declared 20 Hz capacity fixture blocks greenlight; it does not justify silently relaxing ordering or memory guarantees.

## Risks and open questions

- The product owner must explicitly accept one 20 Hz v1 `WorldTick` and measured packet cadence. Keeping “128 Hz everything” would require a future architecture decision that rescopes gameplay timing, CPU capacity, plugin APIs, persistence tests, and probably the single-writer decision.
- “Early Minecraft 1.0-ish” is not a precise redstone/fluid/tick compatibility target. Modern Java evidence informs architecture but does not prove old-version event order. `GAME-02` needs black-box fixtures against the selected reference version.
- Count budgets preserve determinism but cannot guarantee wall-clock duration if one block, entity, or plugin callback is expensive. Built-ins need bounded complexity; plugins need watchdogs, quotas, and disable policy under `ARCH-05`.
- Carrying overdue work preserves state but can produce gameplay lag or starvation. The prototype may need deterministic fair interleaving by chunk while retaining priority/order guarantees; any change becomes part of compatibility semantics.
- A global monotonically increasing sequence can eventually wrap only after an impractical duration, but serialization and comparison must define wrap as unsupported world exhaustion rather than silently reorder.
- Stable full sorts can allocate or dominate hot ticks. Use canonical data structures and incremental heaps, then profile; do not relax ordering ad hoc.
- Region ticking may become necessary for large geographically distributed servers. Deferral is safe only if internal APIs avoid thread-affine Godot/server globals and route cross-chunk effects through explicit commands.
- Analytical offline progress is a gameplay decision. Applying it inconsistently to crops, furnaces, fluids, and entities would surprise players more than a clear “inactive chunks freeze” rule.
- Unlimited vertical addressability multiplies the number of potentially active sections. Activation must follow actual resident/nonempty sections, not scan from a global minimum to maximum Y.

## Dependencies

- Requires: `ARCH-01` authoritative single-writer ownership; `ARCH-04` pause and local-server lifecycle; `ARCH-05` plugin execution budgets; `WORLD-01` finite section/chunk representation; `WORLD-02` safe publication; `WORLD-05` leases, revisions, and memory accounting.
- Coordinates with: `ARCH-02` data layout/entity ownership and `NET-06` packet cadence/client prediction experiments.
- Coordinates with: `NET-02` movement/prediction; `NET-05` interest/simulation radii; `NET-06` packet and subsystem frequencies; `WORLD-03`/`WORLD-04` scheduled-tick persistence; `GAME-01` registries; `GAME-02` redstone/block-update semantics.
- Blocks: implementation of scheduled ticks, fluids, random ticks, block entities, AI/entity simulation, redstone/update propagation, plugin timers, replay debugging, and final server-rate configuration.

## Rejected or deferred alternatives

- Configurable 32/64/128 Hz for every world system: rejected for v1 because it multiplies CPU and alters tick/replay semantics. Reopening it requires a future architecture decision, not a configuration change.
- One worker per chunk or `Parallel.ForEach(activeChunks)`: rejected because neighboring chunks interact, workers would concurrently mutate entities/plugins/global systems, and completion order would become gameplay order.
- Per-chunk locks around live mutation: rejected because multi-chunk effects require lock ordering, plugin callbacks escape the discipline, and rollback after partial updates is undefined.
- Time-budgeted “process until stopwatch says stop”: rejected for authoritative queues because different hardware would process different operations; use deterministic count/memory budgets and report tick debt.
- Catch up every missed random/AI tick after activation: rejected because cost grows with absence duration and produces unrealistic burst simulation.
- Drop overdue scheduled ticks or neighbor updates under load: rejected because it silently changes fluids, redstone, and player builds.
- Tick all loaded chunks: rejected because residency is a memory/network concern while activation is a CPU/gameplay concern.
- Paper-style per-entity activation heuristics: deferred; they can improve CPU but introduce type-specific exceptions and altered behavior. Start with explicit chunk tiers.
- Folia-style parallel regions: deferred behind the profiling/prototype gate; Folia's ownership and plugin restrictions demonstrate that this is a separate architecture.
- General asynchronous plugin mutation: rejected; asynchronous plugins may compute on snapshots and submit validated future commands only.
