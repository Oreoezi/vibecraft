# WORLD-01 Chunk coordinate and memory model

Status: Proposed

Owner: World-storage research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Represent the authoritative world as sparse **16×16×16 sections** addressed by a dimension ID and signed 64-bit section coordinates. Treat an X/Z column as an index and scheduling view, never as the allocation or persistence unit. Store block states in an adaptive `Uniform | Paletted | Direct` container and expose Godot only to coordinates relative to a nearby floating origin.

One-sentence rationale: Cubic sections make vertical cost proportional to occupied/loaded volume, while 64-bit coordinates and a local render origin keep the save format height-agnostic without forcing enormous coordinates through Godot physics.

### Spec correction

“Square chunks (aka no max height)” is not a sufficient design. A 16×16 column still needs either a fixed-height array or an unbounded sparse vertical structure. Also, no implementation can provide literally infinite playable height: integer arithmetic, generation work, simulation interest, networking, and floating-point physics all require operational bounds. The decision here is **no hard-coded height in the section key or file format**, not “generate or simulate an infinite vertical column.”

For v1, every dimension should have configurable generation and build ranges plus an operational world border. These are policy and denial-of-service limits, not serialization limits, and can be expanded without converting section keys.

### Owner decision — 2026-08-13

The initial v1 dimension policy is approximately **10,000 buildable blocks tall**.
Represent it as explicit `MinBuildY` and exclusive `MaxBuildY` values whose difference
is 10,000; the exact placement around world zero is a world-generation/product
fixture, not a save-key assumption. Generation may use a smaller subrange. Signed
sparse section keys remain height-agnostic so an operator or later release can expand
the policy without converting every coordinate.

## Context and constraints

- The C# server is authoritative and must stream, generate, tick, and save work in parallel.
- The world format must not encode a Minecraft-style fixed maximum Y.
- Most underground and sky volume is homogeneous, so allocating a dense column to every possible Y would defeat the requirement.
- Mods will add block states; persisted values cannot depend on nondeterministic runtime registration order.
- Negative coordinates must map identically in C#, databases, network messages, and tools.
- Godot rendering and physics use floating-point vectors. Canonical world coordinates therefore cannot be Godot `Vector3` values.
- Lighting, weather, structures, entities, and generation will all consume the same coordinate contract; changing it later would be unusually expensive.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| Fixed-height 16×16 columns | Simple heightmaps, generation, and Minecraft compatibility | Height is embedded everywhere; empty vertical space costs memory; raising height becomes a migration | Reject: contradicts the stated height goal |
| Sparse 16³ sections plus column views | Fine-grained loading, edits, saves, and network deltas; established voxel size; height-independent keys | More section objects and boundary handling; cross-section systems need explicit coordination | **Recommended** |
| Sparse 32³ sections | Fewer keys, database rows, and draw/stream requests | 8× more voxels per section; edits and remeshing have larger blast radius; homogeneous and busy areas share one large unit | Viable only if benchmarks strongly favor it |
| Octree/SVO as canonical world model | Natural multi-resolution sparsity and far-view potential | Complex mutable updates, persistence, neighbor lookup, simulation, and mod APIs | Defer to derived LoD/render data; do not make it authoritative |

## Evidence

### Minecraft

- Minecraft Java 1.18 expanded the Overworld to a still-fixed 384-block range and added `yPos`, the minimum section Y, while retaining section-oriented `block_states` and paletted biomes. Mojang’s release notes also describe the migration/blending data required to extend old worlds ([official Java 1.18 notes](https://feedback.minecraft.net/hc/en-us/articles/4415128577293-Minecraft-Java-Edition-1-18)). This demonstrates both the value of sections and the compatibility cost of embedding a global height.
- Mojang explicitly delayed the height/world-generation work because the increased build height affected performance and many other systems ([official Caves & Cliffs announcement](https://www.minecraft.net/en-us/article/a-caves---cliffs-announcement)). That is evidence against assuming height is only a storage concern.
- The exact Anvil internals are not officially specified. The community-maintained format documentation records 16×16×16 section data, local palettes, and omission of empty sections ([Minecraft Wiki mirror, secondary source](https://minecraft.fandom.com/wiki/Chunk_format)). The recommendation borrows the proven section granularity, not Anvil’s fixed-height column record.
- OpenCubicChunks documents replacing Minecraft columns with stacked 16³ cubes ([project wiki](https://github.com/OpenCubicChunks/CubicChunks/wiki/About-the-mod)), while its README still calls out a 32-bit-coordinate limit ([project source](https://github.com/OpenCubicChunks/CubicChunks)). Its experience supports cubic allocation while warning that “unlimited” is always bounded by the chosen coordinate type.

### Luanti

- Luanti’s `MapBlock` is a linear array of 16×16×16 nodes and is stacked in Y under an X/Z map sector ([official engine documentation](https://docs.luanti.org/for-engine-devs/nmpr/)). This is close to the recommended section/column separation.
- Luanti uses signed 16-bit map coordinates and consequently limits all three world axes to roughly −30,912…30,927, short of the theoretical integer edge to avoid bugs ([official world-boundary documentation](https://docs.luanti.org/for-players/world-boundaries/)). This is a concrete example of a coordinate type becoming a permanent product limit.
- Luanti 5.14 added a single-node representation for homogeneous mapblocks rather than allocating all nodes ([official 5.14 release post](https://blog.luanti.org/2025/10/05/5.14.0-released/)). Uniform sections are therefore a practical optimization, not speculative complexity.

### Godot Voxel Tools

- Voxel Tools generates and streams 16³ blocks, and its column generator documentation distinguishes a fixed generation band from a terrain container that remains vertically unlimited ([official generator documentation](https://voxel-tools.readthedocs.io/en/latest/generators/)). That is the same separation recommended here: finite work requests over a height-agnostic address space.
- Its block format stores homogeneous channels as one uniform value instead of a dense array ([official block-format specification](https://voxel-tools.readthedocs.io/en/latest/specs/block_format_v3/)).
- Its SQLite format has evolved through packed keys with 16-, 19-, and 25-bit coordinates plus a text form ([official SQLite-format specification](https://voxel-tools.readthedocs.io/en/latest/specs/sqlite_format_v1/)). **Inference:** repeated key-format expansion is evidence that compact bit-packed persistent coordinates save little compared with their migration cost.
- Godot’s own documentation says normal `Vector3` components are 32-bit and lose precision far from the origin; double-precision builds add memory/performance cost and still have shader limitations ([official large-world documentation](https://docs.godotengine.org/en/4.3/tutorials/physics/large_world_coordinates.html)). A floating origin keeps that client concern out of the server and save format.

### Vintage Story

- Vintage Story’s community-documented save format uses 32³ chunk rows in SQLite and packs only 9 bits of chunk Y into its key ([version-verified project wiki, secondary source](https://wiki.vintagestory.at/index.php?title=Modding%3AChunk_Data_Storage)). It shows that 32³ is viable, but also that a packed key quietly reintroduces a vertical limit.

### Evidence-based interpretation

The sources establish that cubic subregions, uniform compression, and local palettes work in real engines. They do **not** establish that 16 is universally optimal for VibeCraft’s C# server or rendering pipeline. The 16-vs-32 choice must therefore pass the benchmark below. Sixteen is the default because it limits edit/remesh/save amplification and aligns with the Minecraft-like gameplay/network vocabulary.

## Proposed design

### Canonical coordinates

Define shared, engine-independent value types; do not expose raw tuples as interchangeable coordinates.

```csharp
public readonly record struct DimensionId(uint Value);
public readonly record struct BlockCoord(long X, long Y, long Z);
public readonly record struct SectionCoord(long X, long Y, long Z);
public readonly record struct ColumnCoord(long X, long Z);
public readonly record struct LocalBlock(byte X, byte Y, byte Z); // each 0..SectionSide-1

public readonly record struct SectionKey(DimensionId Dimension, SectionCoord Coord);
```

Prototype profile (not yet a persistent contract):

- E1 begins with `SectionSide = 16` and compares it with 32. The selected side,
  indexing order, and codec become format-v1 constants only after the benchmark and
  before any user world is created.
- For the 16-side candidate, `section = floor(block / 16)` and
  `local = floorMod(block, 16)` on every axis.
- For the 16-side candidate, `linearIndex = localX | (localZ << 4) | (localY << 8)`;
  X is contiguous, then Z, then Y. The 32-side candidate must define and test its own
  checked equivalent rather than inheriting these shifts.
- A valid section coordinate must have a representable signed-64-bit block origin
  and end for the selected side. Decode and conversion use checked arithmetic;
  operational borders sit vastly inside this representational edge.
- Neighbor arithmetic is checked before adding/subtracting near `long.MinValue`/`long.MaxValue`.
- C# `/` on signed integers rounds toward zero ([Microsoft language reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/arithmetic-operators)), so generic coordinate code must use shared `FloorDiv`/`FloorMod` helpers rather than raw `/` and `%`.
- All coordinate codecs and databases store the three signed values separately. No Morton code or fixed-width bit packing is part of a persistent key.

Required property tests include `-1 -> section -1/local 15`, `-16 -> -1/0`, and `-17 -> -2/15` on every axis.

### Shared scalar domains

- `WorldTick` is an unsigned 64-bit authoritative logical tick.
- `ClientInputSequence`/`ClientPredictionStep` are wrapping unsigned 32-bit,
  connection-local ordering domains. They are never converted to elapsed authority
  time without the time-sync mapping.
- `SectionRevision` is a checked nonnegative signed 64-bit value (`long`). It does not
  wrap in a valid world. Cell references use the owning section revision unless a
  brief explicitly declares a separate revision domain.
- Entity, inventory, registry, render, and cache revisions are separate named domains;
  equal numeric values do not imply causality or convertibility.

### Height-agnostic, not work-agnostic

- The section map is sparse. No array is indexed by global Y and no file header contains `min_y`, `height`, or a section-count ceiling.
- A dimension descriptor specifies `GenerationRange`, `BuildRange`, and
  `OperationalBorder`. The initial `BuildRange` is 10,000 blocks tall. Expansion
  changes policy, not keys.
- Generation is requested by finite section/region ranges. A generator must never attempt to “generate a whole unbounded column.”
- Columns maintain an ordered set of materialized/occupied section Ys and cached topmost opaque/solid positions. This supports skylight, weather, and spawn queries without scanning from an imaginary maximum height.
- “No persisted row” means **no materialized override**, not necessarily “known air.” WORLD-03 must persist generation provenance/ranges so a changed generator cannot silently reinterpret previously visited empty space.
- A fully air section that contains no block entities, ticks, or other state may collapse to a uniform section or a generation-provenance marker; it must not be confused with never-generated data.

### In-memory section

```text
SectionSnapshot
  key: SectionKey
  revision: int64 (nonnegative, monotonically increasing)
  generationVersion: uint32
  blocks: BlockStateContainer
  blockEntities: sparse map<localIndex, BlockEntity>
  scheduledTicks: section-owned queue/index
  derived: non-persistent occupancy/height/mesh/lighting caches

BlockStateContainer
  Uniform(stateId)
  Paletted(palette[], bitsPerEntry, packedIndices[4096])
  Direct(stateIds[4096])
```

- `stateId` is a world-stable unsigned ID resolved through a persisted registry. It must not be a mod load-order index; the exact registry lifecycle belongs to GAME-01/WORLD-09.
- `Uniform` stores one state. `Paletted` uses 1–8 bits per voxel and grows at 2, 4, 8…256 states. `Direct` uses 32-bit state IDs above that threshold.
- Approximate block-array payloads are 512 B at 1 bit, 2 KiB at 4 bits, 4 KiB at 8 bits (plus palette), and 16 KiB direct. Object/header overhead must be measured in .NET rather than guessed.
- Mutations increment `revision`. Palette growth may repack a 4096-entry section; palette compaction happens on save/unload or a background snapshot, not synchronously on every deletion.
- Snapshots crossing worker boundaries are immutable. A writer that edits a section after snapshot revision N must leave N+1 dirty even if N later commits.
- Meshes, LoD, lighting textures, and network encodings are derived consumers. They may use larger aggregation blocks but cannot redefine canonical storage coordinates.

### Client-local coordinates

- Network state carries `DimensionId`, `SectionCoord` (`sint64` per axis if Protobuf is selected), and bounded local/fractional values. It does not send a global floating-point position as authoritative state.
- The client selects an origin section near the local player. Godot node transforms are `(worldSection - originSection) * 16 + localPosition`.
- Rebase at a configurable distance (initially 128 sections / 2,048 blocks), between physics steps. Rebase all active nodes and physics objects as one operation.
- Server simulation uses integer block/section coordinates plus bounded local fixed- or double-precision offsets; it never depends on Godot’s origin.
- Multiplayer players can be far apart because each client has its own origin. Server interest management works in section coordinates.

## Interfaces affected

```csharp
public interface ISectionSource
{
    ValueTask<SectionSnapshot?> LoadAsync(SectionKey key, CancellationToken ct);
}

public interface ISectionDirectory
{
    ValueTask<IReadOnlyList<long>> ListMaterializedYAsync(
        DimensionId dimension, ColumnCoord column, long minY, long maxY,
        CancellationToken ct);
}

public interface IWorldOrigin
{
    SectionCoord Origin { get; }
    Vector3 ToClientLocal(BlockCoord block, Vector3 fractionalOffset);
}
```

`IWorldOrigin` is client-only. Shared gameplay code receives `BlockCoord`/`SectionKey`, not Godot vectors.

## Greenlight criteria

- Coordinate property tests pass for positive/negative boundaries, values beyond 32-bit range, and checked-overflow edges in every serializer.
- No public world/storage/network API uses a packed coordinate key or global `Vector3` as canonical position.
- The adaptive container uses at least 50% less block-state memory than `uint[4096]` across representative generated terrain, while high-entropy sections use no more than 10% extra memory.
- A 16³ section is no more than 15% slower than a dense array for the representative block lookup/edit/tick benchmark; otherwise simplify the mutable hot representation while retaining the same external contract.
- The 16³ vs 32³ streaming benchmark confirms 16³ stays within the agreed load/remesh/save budget. If 32³ wins materially, change `SectionSide` before any persistent world ships.
- A Godot client can move/rebase at large synthetic section coordinates without visible jumps, collision discontinuity, or authoritative-coordinate drift.
- Generation, skylight/weather, and structure designs explicitly consume sparse vertical section ranges and do not reintroduce a fixed global height array.

## Prototype or benchmark

Required: yes

Smallest useful experiment:

1. Implement only the coordinate value types, floor helpers, three block-state representations, immutable snapshots, and a fake section directory.
2. Generate 100,000 sections with four distributions: all-air/stone, layered terrain, normal mixed gameplay, and adversarial high-entropy modded states.
3. Benchmark random reads, clustered writes, palette growth, snapshot creation, memory retained after GC, and 16³ versus 32³ save/remesh invalidation volume.
4. Round-trip one million random coordinates, including negative boundaries and values outside signed 32-bit range, through the proposed network and storage key representations.
5. In a minimal Godot scene, teleport through synthetic world coordinates while keeping nodes near a rebased origin; verify rendering and collision across a rebase.

Success metrics are the greenlight criteria above. A failure of the palette CPU target changes the internal mutable representation, not the coordinate/storage unit. A failure of 16³ against 32³ before worlds exist may change `SectionSide`; after release it becomes a format migration and is therefore too late for casual tuning.

## Risks and open questions

- Truly unlimited building conflicts with weather/skylight semantics and abuse prevention. The format can be height-agnostic while each server still enforces configurable bounds.
- Per-section objects may create GC pressure in C#. Use value types, pooled buffers, and immutable ownership transfer; measure before adopting unsafe/native memory.
- A 256-state palette cutoff is a starting policy, not an ABI. Only the serialized logical variants are stable; thresholds may be tuned.
- Block entities or scheduled ticks can make an otherwise uniform/air section non-empty and therefore non-omittable.
- “Lighting calculated fully client-side” is incompatible with authoritative gameplay rules that depend on light (for example mob spawning) unless the server owns a separate coarse gameplay-light model. Rendering light must remain derived and cannot decide server simulation.
- Cross-section structures and redstone need explicit ownership and transaction groups. They must not infer ownership from an X/Z column object.
- A persisted state registry is required before saves are durable across mod-set changes.
- “Generated empty” provenance needs a compact interval/version representation in WORLD-03/WORLD-06.

## Dependencies

- Requires: GAME-01 block-state registry semantics; WORLD-06 generator version/provenance contract; ARCH-01 authoritative simulation boundary.
- Blocks: WORLD-02 generation scheduling; WORLD-03 storage keys; WORLD-05 loading/eviction; NET-05 interest management; RENDER-01/02 meshing; RENDER-04 lighting.

## Rejected or deferred alternatives

- Fixed 16×16×N chunk objects: rejected because N becomes a hidden global height contract.
- Signed 32-bit canonical block coordinates: rejected; they are adequate for many games but needlessly turn range into a save-format constraint.
- Packed 64-bit XYZ keys: rejected because range allocation among axes becomes permanent and makes ordered range queries awkward.
- Global double-precision Godot build as the only large-world strategy: deferred; it does not solve persistent coordinates, costs memory/performance, and still has shader caveats.
- Octree/SVO authoritative storage: deferred to LoD research; mutable block gameplay and ticking benefit from regular sections.
- Separate dimensions stacked at extreme Y values: rejected. Dimensions have explicit IDs and independent coordinates, avoiding Luanti-style consumption of one finite axis.
