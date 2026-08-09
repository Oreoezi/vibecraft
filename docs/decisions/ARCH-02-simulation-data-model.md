# ARCH-02 Simulation data model

Status: Proposed

## Decision

Recommended choice: Use a hybrid data-oriented model—palette-compressed section arrays for ordinary blocks, explicit records for stateful block entities, and a small component-based store for dynamic entities—without forcing all game state into a general-purpose ECS.

One-sentence rationale: Voxels, chests, dropped items, mobs, and global rules have different density and lifecycle patterns; one universal object or ECS representation would make at least one of them inefficient or awkward.

## Context and constraints

- Terrain contains millions of mostly passive blocks.
- A much smaller set of blocks owns inventory, timers, text, or custom behavior.
- Dynamic entities need composable state, network snapshots, persistence, and batch processing.
- Mods need stable content/API identifiers, but the internal storage layout must remain replaceable.
- Server simulation must not depend on Godot nodes.

## Options considered

| Option | Strengths | Costs/risks | Fit |
| --- | --- | --- | --- |
| Object per block/entity | Simple local behavior | Allocation, traversal, serialization, and cache costs | Reject for blocks |
| Everything in one ECS | Uniform queries and extension story | Dense terrain becomes millions of entities; indirect block access | Reject |
| Custom structures per subsystem | Efficient and direct | Inconsistent lifecycle/tooling without shared conventions | Viable but needs common IDs/snapshots |
| Hybrid blocks + block entities + component entities | Matches density/lifecycle; keeps hot data compact | Multiple stores and cross-store references | Recommended |

## Evidence

Terasology keeps block attributes compact for disk/network efficiency and backs only blocks requiring additional behavior or properties with entities ([block-world documentation](https://metaterasology.github.io/docs/concepts/blockWorld.html)). Its entity system separates components from behavior systems and uses events to support module extension ([entity-system documentation](https://metaterasology.github.io/docs/concepts/entitySystem.html)).

Veloren uses ECS to keep batchable component data contiguous and permit dynamic component composition ([Veloren ECS manual](https://book.veloren.net/contributors/developers/ecs.html)). Luanti uses compact node records grouped into 16³ MapBlocks rather than treating nodes as general entities ([Luanti basic data structures](https://docs.luanti.org/for-engine-devs/basic-data-structures/)). These independent implementations converge on the useful split: dense terrain is not an ECS workload; dynamic actors often are.

## Proposed design

### Ordinary blocks

Each loaded section owns:

- a fixed-volume block-state index array;
- a local palette mapping compact indices to stable registry-backed block states;
- optional packed arrays for world light, flags, or generation state only when present;
- a monotonically increasing content revision;
- immutable read snapshots or exclusive ownership during mutation.

Block state is a small immutable value such as `vibecraft:oak_log[axis=y]`, resolved through the content registry. Do not store arbitrary dictionaries per block.

### Block entities

Stateful positions are stored sparsely by local block index. A block-entity record has:

```text
BlockEntityRecord {
  type_id            // namespaced persistent ID
  local_position
  schema_version
  payload            // typed runtime state; versioned persisted form
}
```

The owning block state declares whether a block entity is legal. Placement/removal transitions create or remove the record transactionally. Orphan/mismatched records are quarantined or migrated during load, never silently attached to the wrong block.

### Dynamic entities

Use opaque generational entity handles `(index, generation)` and typed component stores. Initial components should be driven by actual systems: transform, velocity, collider, health, inventory reference, item stack, player connection, AI state, persistence marker, and network relevance.

Rules:

- Components contain data, not Godot objects or unmanaged resource ownership.
- Structural changes are queued while systems iterate and committed at defined barriers.
- Systems declare read/write sets even before automatic parallel scheduling exists.
- Stable persistent UUIDs are separate from recyclable runtime entity handles.
- Network/save schemas are projections, not raw memory dumps of component stores.
- Terrain queries go through a world interface; sections are not entities.

Do not select or implement a sophisticated archetype scheduler before profiling the first mobs/items/players. A minimal sparse-set or typed dense-store design is sufficient for v1 if its API preserves opaque handles and queries.

### Globals and services

Weather, time, registries, dimensions, recipes, connection state, and scheduler queues are explicit world services/state—not singleton entities created merely to preserve ECS purity.

### Extension boundary

Mods consume stable read models, commands, and events. They do not receive references to internal arrays/component stores. The host may later replace the entity-store implementation without changing the mod ABI.

## Greenlight criteria

- A section containing only ordinary blocks allocates no per-block objects.
- Stateful blocks survive save/load and cannot leave orphan payloads after replacement.
- Runtime handles detect stale references after entity reuse.
- Entity snapshots and saves do not expose storage layout or component-memory order.
- One example each—player, dropped item, animal, chest, ordinary block—fits without special identity hacks.

## Prototype or benchmark

Required: yes.

Implement in-memory models only for 16³ and 32³ sections, 100,000 dropped/simple entities, and 10,000 stateful block entities. Measure memory, iteration, random block reads/writes, structural changes, and snapshot extraction.

Initial decision targets:

- ordinary uniform section comfortably below one byte per block excluding shared registry data;
- no allocation on steady-state block reads;
- linear iteration over a selected component set without per-entity dictionary lookups;
- stale handles reliably rejected;
- deterministic serialized projections independent of insertion order.

Use results to choose section size and entity-store library/implementation; do not treat these initial numbers as shipped budgets.

## Risks and open questions

- Mod-defined components need schema ownership and persistence quotas.
- Cross-section block entities (multiblock structures) should store one owner plus references, not duplicate authoritative records.
- Rollback/prediction may require a separate compact player-state model rather than cloning the whole ECS.

## Dependencies

- Requires: `GAME-01`, `WORLD-01`.
- Blocks: entity ticking, persistence, network snapshots, plugin APIs.

## Rejected or deferred alternatives

- Node per block: rejected.
- Every block as an entity: rejected.
- Fully parallel automatic ECS scheduling: deferred until workloads and ownership boundaries are measured.
