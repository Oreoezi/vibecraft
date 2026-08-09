# GAME-02 Deterministic circuits and block updates

Status: Proposed

Owner: Gameplay/world-generation research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Build one deterministic, non-recursive block-update substrate and a specialized circuit layer on top of it; compute an entire connected dust network before publishing signal changes; model devices as explicit face-port state machines with persisted scheduled transitions; and intentionally exclude Minecraft's accidental update-order and quasi-connectivity behavior.

One-sentence rationale: A bounded queue makes every block behavior safe and replayable, while network-level dust evaluation avoids the repeated recursive waves and position-dependent behavior that made vanilla redstone expensive and compatibility-sensitive.

“Pre-1.5 redstone” is a content/style target, not an implementation specification. VibeCraft v1 includes dust, levers, buttons/plates, torches, repeaters, doors/trapdoors, lamps, and basic pistons. It does not promise Java's quasi-connectivity, block-update detection accidents, update suppression, duplication, zero-tick behavior, or orientation/location-specific order.

## Context and constraints

- `WORLD-08` chooses a 20 Hz world clock, one authoritative writer, persisted scheduled ticks, deterministic phases, and bounded immediate work.
- `ARCH-01`, `NET-01`, and `WORLD-08` now share one 20 Hz authoritative
  `WorldTick`. Input, snapshot, and rendering cadence may differ, but they never
  rescale device delays or create another gameplay clock.
- Circuits cross 16³ section boundaries, can remain unloaded for hours, and can intentionally oscillate forever.
- Block placement/removal can trigger support checks, drops, fluids, circuits, block entities, and piston moves. Recursively invoking neighboring code makes stack depth and order dependent on the build.
- A server must bound work from a malicious player or mod without dropping an already accepted state transition.
- Save/reload must preserve repeater/torch/button timing and circuit output. Rebuilding a graph cache is acceptable; inventing a different logical state is not.
- `WORLD-01` has sparse signed vertical sections, not an infinite loaded column. Circuit search must be spatially finite and cannot scan a column's height.
- Mods need a component contract, but arbitrary callbacks cannot run inside the signal solver or bypass queue/fanout limits.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| Reproduce Java pre-1.5 recursive neighbor updates and quirks | Familiar contraptions and tutorial compatibility | Historical behavior is incompletely specified; locational order, recursion, exploits, and high redundant work become permanent API | Reject |
| Generic event queue; every dust node recalculates independently | Simple and extensible | Long lines/grids repeatedly revisit nodes; decreases are especially expensive; order leaks into outputs | Accept only for non-wire block behavior |
| Recompute every circuit in every active section each tick | Predictable and easy to reason about | Cost scales with all built circuits rather than changes; idle megastructures consume CPU | Reject |
| Dirty connected-network solver plus explicit device state machines | Work scales with changed networks; final wire state is coherent; deterministic external notification | Needs topology discovery/cache, atomic work buffers, limits, and boundary rules | **Recommend** |
| General electrical/logic graph detached from blocks | Excellent simulator design and possible compiled circuits | Hard to reconcile with block edits, pistons, section loading, and familiar dust geometry | Defer for future non-redstone devices |

## Evidence

### Minecraft

- Mojang does not publish Java 1.0 source or a normative historical redstone specification. Community histories can identify available components and nominal delays, but they cannot safely define every update edge. This is why VibeCraft must own executable truth tables and traces rather than use “same as pre-1.5” as an acceptance test.
- Mojang's 24w33a redstone experiment explicitly changed wire so all connected wire strengths are set before the line emits block updates, reduced updates to blocks that can receive power, and attempted context-derived deterministic order. The note also warns that changing wire alone can alter interactions with other components ([official 24w33a notes](https://www.minecraft.net/en-us/article/minecraft-snapshot-24w33a)). The experiment is not a shipped compatibility target; it is direct evidence that network-level calculation and update order are separate concerns.
- Modern mapped Java ticking has persisted block/fluid queues with due time, priority, and stable sub-tick order, while chunks distinguish resident, block-ticking, and entity-ticking states. This is secondary evidence from Mojang binaries and supports the `WORLD-08` substrate, not historical parity ([mapped `LevelTicks`](https://mappings.dev/1.21.3/net/minecraft/world/ticks/LevelTicks.html), [mapped `ChunkAccess`](https://mappings.dev/1.21.8/net/minecraft/world/level/chunk/ChunkAccess.html)).

### Alternate Current

- Alternate Current is an open-source Java mod that replaces recursive, per-wire vanilla evaluation with discovery of a connected wire network, identification of external power sources, and propagation through that network. Its author reports that vanilla wires can change repeatedly before settling, emit many duplicate updates, and have location-dependent recursive ordering; the replacement sets each wire once and reports up to 20x lower redstone-dust tick cost while retaining broad vanilla parity ([project README and implementation](https://github.com/SpaceWalkerRS/alternate-current)).
- Its deterministic ordering is based on power-flow direction rather than coordinate hashes. VibeCraft borrows the whole-network/final-state idea, but does not inherit Alternate Current's obligation to emulate Java edge cases.
- Paper issues around Alternate Current and quasi-connectivity show a deeper compatibility trap: a piston can be powered by a distant/diagonal rule yet not receive the update that tells it to re-evaluate ([Paper issue 7852](https://github.com/PaperMC/Paper/issues/7852)). VibeCraft avoids this by making power reach and notification use the same declared ports.

### Mesecons / Luanti

- Mesecons models a redstone-like system with receptors, conductors, and effectors and routes work through an action queue. A historical commit-pinned implementation coalesces selected actions, orders by priority, refuses to execute against unloaded nodes, and saves delayed actions for restart ([Mesecons action queue](https://git.brn.systems/blocky_portaling/mesecons/src/commit/ff0bd76efece4738ed6ba406460859a4d1e7cd8e/mesecons/actionqueue.lua), [component registration shape](https://cheapiesystems.com/git/mesecons/tree/mesecons/init.lua)).
- The same project added overheat protection for controllers/gates after repeated actions ([Mesecons history](https://git.brn.systems/blocky_portaling/mesecons/commits/commit/0e7f68ea92b272d3cca0a1544691cab8d05a08d9/mesecons/init.lua)). This is evidence that programmable or oscillating circuits require quotas and explicit failure behavior.
- Saving only during orderly shutdown and delaying startup execution are useful behavior clues but weaker than VibeCraft's crash-safe transaction requirement. VibeCraft persists logical due ticks and pending boundary work through `WORLD-04`.

### Cuberite

- Cuberite's open incremental redstone simulator uses per-chunk active-block queues and cached power data, with explicit wake-up handling near chunk boundaries ([current simulator source tree](https://github.com/cuberite/cuberite/tree/master/src/Simulator/IncrementalRedstoneSimulator), [chunk-data history](https://ni.xn--ijanec-9jb.eu/anonymous/cuberite/commit/src?h=real-block-count&id=7d93742498e86cd15315c674301469438eb0d807)).
- Its history contains special handling that wakes neighboring chunks for linked-powered blocks up to two positions from a boundary ([boundary fix](https://ni.4a.si/anonymous/cuberite/commit/src?h=template-id-ctor-warning&id=337c4e5cd4e666c34efeb6767fdf1357aa6d3bca)). That is direct evidence that power reach, cache ownership, and section borders must be one contract rather than scattered exceptions.

### Evidence-based findings

The sources support queues, persisted delays, incremental wake-up, network-level dust evaluation, and explicit overload protection. They do not determine VibeCraft's signal geometry or timing table. The rules below are intentionally original, reviewable semantics.

## Proposed design

### General block-update substrate

Every authoritative block mutation is a root operation with a world-assigned sequence. A behavior returns a bounded command batch; it never calls another block behavior directly.

```csharp
public enum BlockUpdateKind : byte
{
    NeighborStateChanged,
    SupportChanged,
    CircuitTopologyChanged,
    CircuitInputChanged,
    Scheduled,
    ActivationRecheck
}

public readonly record struct BlockUpdate(
    ulong ReadyTick,
    ulong RootSequence,
    ulong ChildSequence,
    BlockCoord Target,
    BlockCoord Source,
    BlockUpdateKind Kind,
    WorldStateId ExpectedTargetState,
    long ExpectedSectionRevision);

public interface IBlockBehavior
{
    int MaximumImmediateFanout { get; }
    BlockCommandBatch Evaluate(in BlockUpdate update, IReadOnlyWorldView world);
}
```

- Root commands are sorted by the `WORLD-08` phase contract. Children use fixed face order `Down, Up, North(-Z), South(+Z), West(-X), East(+X)` and then behavior-declared ordinal. This is deterministic but not promised to match Java or be rotation-invariant.
- A batch declares its maximum writes, emitted updates, scheduled ticks, entities, and block-entity changes before commit. The simulation reserves queue/memory capacity, validates all preconditions, then applies the batch atomically or not at all.
- An update whose expected state/revision no longer matches is a deterministic no-op or an explicit recheck according to its kind. It never acts on a new block that reused the position.
- Neighbor/support updates target only behavior-declared subscribers. The engine does not spray every event to a Manhattan radius because one historical implementation did.
- A transaction that targets an inactive destination writes a bounded `DeferredSectionInbox` record keyed by destination section in the world database. The originating block change and inbox insert share a `WORLD-04` atomic group.
- Immediate work is not recursively executed. It drains in `WORLD-08` phase 3/4 and can continue on later ticks with the same ordering metadata.

This refines one open issue in `WORLD-08`: one self-sustaining root must not starve later roots forever. Each root receives a deterministic 4,096 **evaluation-step** quantum per world tick. An unfinished continuation becomes ready at `T+1`; later roots already ready at `T` then run in sequence. A fully computed circuit batch may exceed that quantum only for its one atomic publication: it must reserve and charge every resulting write/notification against the separate global commit budget before publication. This fairness rule should replace the document's ambiguous global FIFO wording during synthesis.

### Circuit signal model

- Signal level is an integer `0..15`.
- Every circuit component declares six directed input/output ports. Connections exist only when both neighboring ports accept one another under the block states and geometry.
- Output has kind `Direct` or `Conducted`. A full solid conductor that receives direct power may expose conducted power to its other faces for one block only; conducted power does not chain through another ordinary solid block.
- Dust accepts declared adjacent direct/conducted sources and same-level dust, plus one-step up/down dust only when the support and clearance predicates pass. The exact geometry is a truth-table fixture, not a mesh/collision inference.
- Pistons, doors, and other devices sample only their adjacent accepted ports. There is no two-block diagonal quasi-connectivity, invisible block-update detector behavior, or “powered but not notified” state.
- Circuit components and tags come from frozen `GAME-01` definitions. A mod can select host-provided evaluators and bounded data; arbitrary native code does not execute inside graph traversal.

```csharp
public readonly record struct Signal(byte Level, SignalKind Kind);

public sealed record CircuitComponentDefinition(
    ContentKey Key,
    ImmutableArray<PortRule> Ports,
    ContentKey Evaluator,
    int MaximumImmediateFanout,
    int MaximumScheduledFanout);
```

### Dust-network algorithm

When wire topology or an external source changes:

1. Discover the connected dust component from the dirty seeds using canonical neighbor order and the immutable section view.
2. Reject the originating placement atomically if joining networks would exceed `MaxCircuitNodes = 65,536` in v1. Existing over-limit imported networks remain inert with an admin-visible diagnostic until split or migrated.
3. Sample each non-dust source port once and seed a max-priority propagation queue.
4. Compute `wireLevel = max(sourceLevel - dustEdgeDistance)` in a private byte buffer. A wire directly fed by a source starts at distance 0; only traversing another dust-to-dust edge attenuates by one. A wire is revisited only when a stronger level is found.
5. Diff against current states. Publish every changed wire state as one atomic network batch only after the final vector is known.
6. Notify external receiver ports once, ordered by `(distance from changed source, flow-relative face order, BlockCoord)`. Equal paths use canonical source/component IDs, never hash iteration or worker completion.
7. Topology/output changes caused by receivers enqueue a new dirty-network root; they do not recurse into the current solver call.

Graph topology and source indexes are derived caches stamped with section revisions. They are not persisted. Signal levels are persisted block-state properties because inactive boundary behavior and visual/network state must survive without recalculating inaccessible neighbors.

Large-network discovery/evaluation reserves its full node/write budget before publish. It may compute across multiple ticks in a private continuation but remains invisible until it can commit the complete result. A stale section revision discards the work and requeues discovery. V1 keeps this computation on the world thread; a snapshot worker is allowed only after replay tests prove stale-result handling.

### Explicit v1 device semantics

All times are 20 Hz world game ticks. These are VibeCraft rules, even where the values feel familiar.

| Component | Inputs and output | Transition rule |
| --- | --- | --- |
| Dust | Connected ports; output level `0..15` | Final network solve; attenuates one level per dust edge |
| Lever | Player toggle; level 15 | Output changes in the committing root tick |
| Button | Player press; level 15 | Turns on immediately; one persisted release tick at `T+20`; another press resets due time |
| Pressure plate | Entity overlap; level 15 | Edge-driven occupancy plus deterministic rescan every 10 ticks while active; off when count reaches zero |
| Torch/inverter | Rear input; level 15 to all allowed non-rear ports | Desired output is inverse of rear input, committed after 2 ticks; newer desired state coalesces pending transition; no torch burnout in v1 |
| Repeater | Rear input; side lock inputs; forward level 15 | Rising/falling target commits after configured 2/4/6/8 ticks; side power freezes current output and pending countdown until unlocked |
| Lamp | Any accepted adjacent input | Lit state changes after the complete circuit batch, with no extra delay |
| Door/trapdoor | Any accepted input plus optional manual latch | `open = powered || manualLatch`; iron-like variants disallow manual latch; paired door blocks update atomically |
| Piston | Any accepted adjacent input except the front | Rising/falling edge schedules action at `T+1`; push line limit 12; no slime/honey behavior in v1; blocked move leaves piston state unchanged |

- A scheduled transition stores expected component type/state, desired output, due tick, root sequence, and component revision. Replacing a device invalidates the transition.
- Multiple pending transitions for the same device/type coalesce only according to its table rule. No generic “last callback wins.”
- Piston planning reads a finite 12-block line, validates all source/destination states and block entities, reserves destination sections, then commits movement as one world transaction. Entity pushing occurs in the entity phase from an emitted movement event.
- A piston never force-loads an unbounded chain. If any required destination is unavailable/corrupt or cannot gain a short `BlockTicking` lease, the move remains blocked and retries only on a new input/activation event.
- Piston-moved block entities retain identity through a `WORLD-04` atomic group. Unknown/missing blocks are immovable by default.

### Section boundaries, activation, and persistence

- Circuit propagation mutates only `BlockTicking` sections. A changed output aimed into a resident-but-inactive or unloaded section becomes a persisted destination inbox and stops at that boundary.
- An inactive section freezes wire outputs, scheduled circuit work, pressure plates, and piston state. Due scheduled work becomes eligible under normal budgets when the section activates; missed periodic scans are not replayed.
- A block-ticking section requires a one-block resident halo for topology reads. Failure to materialize the halo leaves the circuit root pending and presents a boundary/loading state; it does not assume air.
- Before unload, pending updates, scheduled transitions, deferred inbox acknowledgements, wire-output states, and moving-piston block entities must have durable receipts. Derived graph caches are dropped.
- Activation imports deferred inbox entries in sequence order, validates expected revisions, rebuilds graph cache lazily, and schedules `ActivationRecheck` for circuit ports and neighbor-dependent blocks on the six section faces.
- If activation would join materialized dust components beyond 65,536 nodes, it records the merged component as `CircuitBlockedOversize`, preserves all persisted output levels, and emits no partial propagation. Ordinary block ticking may continue; a block edit that splits the component triggers a bounded recheck and can clear the diagnostic.
- The destination acknowledges/removes a durable inbox record in the same transaction that persists the resulting state/continuation. Duplicate delivery is harmless because inbox IDs and expected revisions are idempotent.
- A crash can restore the old block plus no inbox or the new block plus its inbox, never a durable cross-boundary change without the required wake-up record.

“Unlimited height” does not expand circuit activation. Every operation walks an explicitly bounded connected component of materialized sections. A 65,536-node circuit limit and server activation/memory budgets are intentional operational limits even though section coordinates have no small fixed Y ceiling.

### Runaway and failure policy

- Global provisional limits are 100,000 committed block writes/notifications per world tick, 200,000 queued records in memory, 4,096 evaluation steps per root per tick, and 65,536 nodes per connected circuit. Atomic circuit publication is exempt only from the per-root evaluation quantum, never the 100,000 global commit limit. Bytes, not only counts, participate in `WORLD-05` accounting.
- Due work beyond execution limits remains ordered and continues later. No accepted update is dropped, and machine speed never determines which updates happen.
- A combinational network must settle in one atomic solve. Repeated topology/output signatures within one root are diagnosed as a combinational cycle and deferred to `T+1`; 20 identical failures quarantine that circuit until a block changes.
- Intended oscillators use delayed devices and therefore produce finite scheduled work each tick. Per-circuit rate metrics identify lag machines; configured server policy may pause a circuit after sustained quota exhaustion but may not silently change its blocks.
- New external/plugin/player mutations fail atomically with `CircuitCapacity`, `UpdateCapacity`, or `SectionUnavailable` if their declared reservation cannot fit. Built-in invariant failures pause world mutation as `WORLD-04` requires rather than omit consequences.
- Plugins cannot raise limits, forge sequences, enqueue raw updates, or synchronously call a neighbor. They submit validated root commands and receive typed failure results.

### Diagnostics and compatibility fixtures

For every circuit root, debug mode can emit:

```text
root sequence / source command
section and block revisions
network ID and node count
old/new wire-level hashes
ordered device notifications
scheduled transitions created/coalesced/cancelled
operations used, continuation, boundary inboxes, failure
```

The project should publish VibeCraft truth tables and small world fixtures. Minecraft worlds/tutorials are reference material only; a mismatch is a product review input, not automatically a bug.

## Acceptance / greenlight criteria

- One hundred runs of each fixture, with randomized hash insertion and asynchronous load/save completion, produce identical per-tick block states, queues, device events, and replay hashes.
- Dust lines, branches, loops, and grids set each changed wire at most once per network publication; a 65,536-node permitted network completes without recursion or unbounded allocation.
- Rotation fixtures produce the same final wire levels and device outputs. Where simultaneous conflicting device actions use world-axis tie-breaking, the documented trace is stable and an intentional difference is recorded.
- Button, torch, repeater, door, and piston transitions survive save/reload on every tick before/during/after their delay with exactly the same due tick and outcome.
- Cross-section fixtures at positive/negative X/Y/Z boundaries match same-section fixtures while active; inactive-boundary behavior freezes, persists, and resumes exactly as documented.
- Killing the server around a cross-boundary root yields either old state/no inbox or new state/durable inbox/result, never a permanently stale powered device.
- An oscillator and adversarial update storm remain inside count/byte limits, do not overflow the stack, do not starve unrelated roots indefinitely, and expose metrics/actionable errors.
- A piston move cannot duplicate/delete blocks, items, or block entities under rejection, section unload, save race, or process termination.
- Quasi-connectivity, update suppression, zero-tick, and duplication regression fixtures explicitly do not reproduce Java behavior.

## Prototype or benchmark

Required: yes

Smallest useful experiment: Extend the engine-neutral `WORLD-08` harness with the general update queue, deferred section inboxes, dust-network solver, the v1 device table, basic pistons, save/reload, and canonical trace output.

Test matrix:

1. Golden circuits: straight/branched/ring/grid dust, multi-source increase/decrease, conductor blocks, every device edge, repeater lock, piston obstruction, paired door.
2. Boundaries: each fixture translated across all six section faces and negative coordinates; unload one side at every event step, then reactivate.
3. Persistence: crash at every transition and SQLite commit hook; duplicate/reorder load completions and inbox delivery.
4. Abuse: clocks, self-changing blocks, maximum wire component, repeated join/split, piston loops, malicious plugin fanout, one million deferred updates.
5. Comparison: implement a naive per-wire queue solver solely as a benchmark baseline; compare final VibeCraft semantics, not Java parity.

Success metrics:

- Final-state and replay criteria above pass across 100 seeds and worker counts 1/2/4.
- A 10,000-wire grid recompute is at least 5x faster and performs at least 10x fewer block-state writes/neighbor notifications than the naive solver on the baseline CPU.
- A maximum 65,536-wire solve completes below 20 ms p95 with under 8 MiB temporary allocation after warm-up; if it fails, lower the gameplay limit or move snapshot-safe computation off-thread before greenlight.
- A representative active area containing 100 independent clocks, 10,000 idle wires, and 1,000 ordinary neighbor-dependent blocks keeps the entire world tick below 50 ms p99 for 30 minutes.
- Every persistence fault produces an allowed old/new state and no duplicate piston/block-entity identity.

## Risks and open questions

- The integrated baseline selects a fixed 20 Hz world-logic clock plus independently
  tunable input/snapshot cadence. Reopening the authoritative world rate requires a
  new decision, device timing table, replay contract, and compatibility version.
- Fixed face order can still make simultaneous conflicting actions orientation-dependent even though wire strength is not. Flow-relative external notification should be tested with players before locking the compatibility version.
- Atomic network publication can introduce a multi-tick delay for a very large circuit. That is preferable to exposing half-propagated strength, but UI should reveal a throttled/pending circuit.
- A hard connected-network size limit is a product constraint. It is required for abuse resistance and disproves any implication that unlimited world height means unlimited contraption size.
- The one-block inactive boundary freeze differs from some Minecraft chunk-loading behavior. It must be shown clearly in debug overlays and tested after reconnect/restart.
- Moving blocks affects lighting, entities, collision, inventories, and networking; the simple piston is still one of the riskiest M4 features and should not block M0-M3.
- Dust geometry on stairs and through conductors needs exhaustive visual fixtures. Collision/render models cannot be the source of truth because resource packs are cosmetic.
- Modern redstone additions should extend the port/device contract. Comparators, observers, inventory transport, slime assemblies, and analog container output are not hidden v1 requirements.

## Dependencies

- Requires: `GAME-01`, `ARCH-02`, `WORLD-01`, `WORLD-03`, `WORLD-04`, `WORLD-05`, `WORLD-08`; requires tick-rate synthesis with `NET-01`.
- Coordinates with: `NET-04` block transaction results; `RENDER-02` revisioned remeshing; lighting invalidation; plugin quotas and transaction API.
- Blocks: circuit implementation, pistons, neighbor-dependent block behavior, circuit plugin surface, and M4 acceptance fixtures.

## Rejected or deferred alternatives

- Bug-for-bug pre-1.5 Java compatibility: rejected because the behavior is not a stable public specification and imports accidental exploits/order debt.
- Recursive neighbor callbacks: rejected due to stack overflow, repeated work, and hidden ordering.
- Quasi-connectivity and update-powered mismatch: rejected; declared reach and notification use the same ports.
- Dropping updates under overload: rejected; defer deterministically or reject the root transaction before mutation.
- Force-loading an entire connected circuit: rejected because a wire line could pin unbounded world memory/CPU.
- Recompute every circuit every tick: rejected because idle builds should be nearly free.
- Async live-world circuit mutation: rejected for v1; workers may later compute from immutable revisioned snapshots and return atomic proposals only.
- Torch burnout and Java locational order: deferred unless playtesting demonstrates a deliberate gameplay need.

## Source-quality notes

Mojang snapshot notes and all linked open-source repositories are primary/vendor implementation evidence. Java class mappings and historical component details are secondary because no supported Java 1.0 source/spec exists. The signal model, device timings, limits, fairness rule, and boundary behavior are VibeCraft proposals and require the stated executable fixtures.
