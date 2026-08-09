# WORLD-07 Deterministic cross-section structure generation

Status: Proposed

Owner: Gameplay/world-generation research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Generate structures as durable coordinate-owned plans, not writes into whatever neighboring sections happen to be loaded; derive candidate and instance identities from stable keys and coordinates; persist each accepted/rejected candidate before section finalization; rasterize an accepted plan into deterministic target-section-only patches; and record idempotent application receipts in the same transaction as each section revision.

One-sentence rationale: A stable plan makes a village, tree, dungeon, or cave-room the same object regardless of discovery direction, worker order, chunk boundaries, unload/reload, or crash timing.

The plan/raster split refines `WORLD-02`'s “deferred cross-section structure writes”: the durable deferred object is a bounded semantic `StructurePlan` plus per-section index, not an unordered bag of neighbor mutations. No generation worker may modify a live or persisted neighboring section.

V1 does not pursue bug-for-bug Minecraft structure placement. Familiar spacing and exploration are useful; preserving Java's undocumented random consumption, chunk-order bugs, bounding-box quirks, or version-specific seed cracking is not.

## Context and constraints

- A structure may cross horizontal and vertical 16³ section boundaries, while sections are requested in any order and may never all be resident together.
- `WORLD-06` pins generator profiles, assigns generation epochs by 128×128-block horizontal tiles, and requires target-only immutable stage patches.
- `WORLD-03`/`WORLD-04` require one durable world authority, crash-safe atomic section publication, and revisions.
- Players can edit one generated piece before another intersecting section is ever requested. Future generation must not “complete” the structure by rewriting the edited section.
- Structure blocks use stable `GAME-01` state keys, not session runtime IDs. Templates can contain block entities, loot, entities, markers, replace predicates, and mod-owned content.
- A candidate planner needs terrain/biome facts before chunks exist, but consulting live neighboring chunks would make output depend on load order and player edits.
- Sparse signed coordinates do not permit infinite footprints. Every definition needs finite dimensions, candidate radius, operation count, and target-section count.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| Generate only structures contained in one section | Very simple and order independent | Rules out villages, large trees, dungeons, bridges, and vertical complexes | Reject |
| When an origin section generates, immediately write all neighboring sections | Easy visual continuity | Force-generates/pins neighbors; races workers; overwrites generated/player state; crash leaves partial writes | Reject |
| Queue raw block writes for absent neighbors | Better than live mutation | Ordering/conflict semantics are implicit; stale template/runtime IDs; duplicate block entities and loot on retry | Reject |
| Deterministic plan, but recompute it whenever a section loads | Less metadata | Code/mod changes and missing packages can change pieces; repeated expensive planning; no durable conflict decision | Reject |
| Persist bounded candidate decisions and plans; rasterize clipped idempotent patches | Order independent, auditable, retry safe, supports large finite structures without force-loading | Additional tables/indexes, exact plan/rasterizer versioning, planning discipline | **Recommend** |

## Evidence

### Minecraft and Cubiomes

Modern Java separates structure starts and references in its chunk-status pipeline and stores structure starts/references on chunk access, showing that structure identity and reach are different from ordinary local terrain writes ([mapped `ChunkStatus`](https://mappings.dev/1.21.8/net/minecraft/world/level/chunk/status/ChunkStatus.html), [mapped `ChunkAccess`](https://mappings.dev/1.21.8/net/minecraft/world/level/chunk/ChunkAccess.html)). VibeCraft adopts explicit planning/indexing but not Java's exact serialization or stage order.

Cubiomes, an open-source reimplementation of Minecraft biome and structure algorithms, exposes the common two-step model: compute candidate positions from seed/grid rules, then test terrain/biome viability ([Cubiomes repository](https://github.com/Cubitect/cubiomes)). It also illustrates why “Minecraft-compatible seed behavior” becomes a large versioned algorithm surface. VibeCraft uses coordinate candidates and viability checks while defining its own stable stream and plan contracts.

### Voxel Tools

Voxel Tools warns that neighboring generation blocks are processed in unpredictable order, provides multipass generation for cross-block dependencies, and recommends precomputed blueprints for very large structures ([generator documentation](https://voxel-tools.readthedocs.io/en/latest/generators/)). This directly supports a coordinate-owned planning pass and clipped target patches.

### Luanti

Luanti schematics store dimensions, node-name mappings, probabilities, and placement information rather than relying only on ephemeral numeric content IDs ([Luanti schematic format](https://docs.luanti.org/for-creators/luanti-schematic-file-format/)). Its mapgen feature/decorations documentation distinguishes placement rules from the blocks eventually emitted ([mapgen features](https://docs.luanti.org/for-creators/mapgen/features/)). VibeCraft similarly keeps canonical state keys and placement semantics, but adds durable instance IDs, exact fingerprints, conflict rules, receipts, and strict bounds.

### Veloren

Veloren's world simulation and site generation model large world features as world-level deterministic state rather than incidental edits by an arbitrary loaded voxel column ([world simulation source](https://gitlab.com/veloren/veloren/-/blob/master/world/src/sim/mod.rs)). VibeCraft's persisted plan is the smaller block-world equivalent: the site exists once, while each section materializes only its clipped piece.

### Terasology

Terasology identifies blocks through module-qualified URIs and block families rather than assuming one global permanent integer ([block access documentation](https://metaterasology.github.io/docs/developing/blocks/accessingBlocks.html), [block definitions](https://metaterasology.github.io/docs/developing/blocks/blockDefinition.html)). Structure templates therefore should carry stable registry keys and compile through the world's frozen mapping, as required by `GAME-01`.

## Normative model

### Structure definitions and hard limits

Every structure type is a frozen generator-profile input:

```csharp
public sealed record StructureDefinition(
    ResourceKey TypeKey,
    uint DefinitionVersion,
    Hash256 DefinitionHash,
    Hash256 PlannerPackageHash,
    Hash256 RasterizerPackageHash,
    CandidateGrid Grid,
    FiniteBounds MaximumFootprint,
    uint MaximumTargetSections,
    uint MaximumBlockOperations,
    ConflictPolicy Conflict,
    ImmutableArray<ResourceKey> RequiredStateAndTagKeys);
```

V1 global safety ceilings, applied in addition to tighter per-type values:

- no AABB axis longer than 1,024 blocks;
- no more than 4,096 intersected 16³ sections;
- no more than 1,048,576 block/biome/block-entity/entity operations in a complete plan;
- no more than 64 MiB uncompressed normalized plan data or 4 MiB persisted compressed plan data;
- no unbounded recursion, “search until found,” or read halo inferred from world height.

These are abuse/corruption ceilings, not content targets. Initial trees, dungeons, ruins, and ore-like structures should remain orders of magnitude smaller. Hierarchical cities or roads require a later site/region-planning brief instead of raising limits casually.

### Candidate identity and keyed randomness

Each structure type declares a finite candidate lattice. A candidate ID is independent of generation order:

```text
candidateId = SHA-256(
  "VibeCraft structure candidate v1" ||
  worldSeed[32] || dimensionKey || epochId || profileFingerprint ||
  structureTypeKey || candidateCellX || candidateCellY || candidateCellZ || attemptIndex)
```

Most surface structures use X/Z cells and a fixed `candidateCellY=0`; underground/sky families may declare a bounded 3D lattice. All fields use `WORLD-06` canonical encoding. Candidate random values come from the candidate ID and semantic child labels through the pinned Philox/SHA-256 stream, never a mutable stage PRNG.

Candidate-cell and grid-region conversion uses mathematical floor division for negative coordinates, never language-default truncation toward zero. Definitions state whether anchors lie at a cell minimum, center, or keyed offset; there is no implicit rounding.

The planner computes anchor, rotation, mirror, palette choices, piece graph, and viability using only:

- the exact `WORLD-06` profile/epoch;
- a bounded `IGenerationSampler` for pre-structure density, biome, surface, fluid, and carver facts;
- already normalized candidate plans in the definition's finite conflict neighborhood;
- frozen registry/tag inputs in the profile.

It cannot read live chunks, player edits, entities, wall time, thread count, or session runtime IDs.

### Candidate enumeration and completeness

Before finalizing target section `S`, the structure-planning stage enumerates every candidate cell whose declared `MaximumFootprint` could intersect `S`. This is a finite inverse-AABB query. It evaluates candidates in canonical `(priorityClass, typeKey UTF-8 bytes, candidateId bytes)` order and persists an accepted or rejected decision.

Persisting negative decisions is intentional. It avoids repeatedly performing expensive viability checks and records that a candidate was considered under the exact profile. A rejected row stores a stable reason code—not a localized message—and the hash of the planner inputs.

If an accepted candidate could touch a tile not yet assigned in `WORLD-06`, plan publication atomically reserves all footprint tiles to the candidate's epoch. If any touched tile belongs to another epoch, v1 rejects the candidate with `CrossEpochFootprint`; it does not clip half a building or silently use another generator. Thus a structure accepted before a generator upgrade carries its bounded neighborhood into the old epoch, while a new structure cannot invade finalized old terrain.

The simulation thread must prove candidate completeness before publishing `S`. A section cannot reach the `structure_plans` stage while any potentially intersecting candidate is unknown.

### Plan contract

```csharp
public sealed record StructurePlan(
    Hash256 StructureId,                  // equal to accepted CandidateId in v1
    ResourceKey TypeKey,
    uint DefinitionVersion,
    Hash256 DefinitionHash,
    uint EpochId,
    Hash256 ProfileFingerprint,
    BlockCoord Anchor,
    AxisAlignedBox64 Bounds,              // inclusive min, exclusive max
    Hash256 PlannerPackageHash,
    Hash256 RasterizerPackageHash,
    ImmutableArray<NormalizedPiece> Pieces,
    ImmutableArray<StructureMarker> Markers,
    ImmutableArray<SectionKey> TargetSections,
    Hash256 PlanHash);
```

`PlanHash` covers the canonical plan excluding itself. Target sections are sorted by signed `(x,y,z)`, deduplicated, and checked against the exact AABB. Coordinates use checked signed 64-bit arithmetic. Piece transforms, integer pivots, rounding, and palette selection are explicit; implementations cannot inherit library-specific matrix rounding.

Plans contain normalized semantic data sufficient to audit placement, but may still require the exact fingerprinted rasterizer package. A missing rasterizer fails affected ungenerated sections; it never swaps in a newer implementation. Existing generated pieces remain normal world data.

### Persistence schema

Logical additions to the `WORLD-03` database are:

```sql
structure_candidates(
  dimension_key TEXT,
  epoch_id INTEGER,
  structure_type_key TEXT,
  cell_x INTEGER,
  cell_y INTEGER,
  cell_z INTEGER,
  attempt_index INTEGER,
  candidate_id BLOB,
  decision INTEGER,                    -- accepted/rejected
  reason_code INTEGER,
  planner_input_hash BLOB,
  plan_hash BLOB NULL,
  decided_revision INTEGER,
  PRIMARY KEY(dimension_key, epoch_id, structure_type_key,
              cell_x, cell_y, cell_z, attempt_index),
  UNIQUE(candidate_id)
)

structure_instances(
  structure_id BLOB PRIMARY KEY,
  dimension_key TEXT,
  epoch_id INTEGER,
  type_key TEXT,
  definition_hash BLOB,
  profile_fingerprint BLOB,
  bounds_min_x INTEGER, bounds_min_y INTEGER, bounds_min_z INTEGER,
  bounds_max_x INTEGER, bounds_max_y INTEGER, bounds_max_z INTEGER,
  planner_hash BLOB,
  rasterizer_hash BLOB,
  plan_hash BLOB,
  plan_blob BLOB,
  created_revision INTEGER
)

structure_section_index(
  dimension_key TEXT,
  section_x INTEGER, section_y INTEGER, section_z INTEGER,
  structure_id BLOB,
  target_slice_hash BLOB,
  PRIMARY KEY(dimension_key, section_x, section_y, section_z, structure_id)
)

structure_applications(
  dimension_key TEXT,
  section_x INTEGER, section_y INTEGER, section_z INTEGER,
  structure_id BLOB,
  plan_hash BLOB,
  patch_hash BLOB,
  applied_revision INTEGER,
  PRIMARY KEY(dimension_key, section_x, section_y, section_z, structure_id)
)
```

Candidate decision, accepted plan, section index, and required tile reservations commit in one transaction before the first affected section publishes. Concurrent identical attempts compare canonical hashes and become a no-op. The same candidate key with a different input/plan hash is a fatal nondeterminism error, not last-write-wins.

`target_slice_hash` covers the plan's normalized pieces/markers that can affect that target and is known when the plan is indexed. It is not the final `PatchHash`, which also covers rasterized operations against the deterministic pre-structure snapshot and is recorded at section publication.

An accepted plan may remain “orphaned” if no intersecting section is ever requested; that is harmless bounded metadata. Vacuuming such plans is allowed only if no section receipt exists, no target section has reached structure planning, and an audited tool can deterministically recreate the same decision.

### Rasterization and ordering

For a target section, the worker loads all indexed accepted plans and verifies hashes. It rasterizes each one through an immutable pre-structure section snapshot and emits:

```csharp
public sealed record StructureSectionPatch(
    Hash256 StructureId,
    Hash256 PlanHash,
    SectionKey Target,
    Hash256 ExpectedPreStructureHash,
    Hash256 PatchHash,
    ImmutableArray<StructureOperation> Operations,
    ImmutableArray<ExclusionVolume> FeatureExclusions);

public sealed record StructureOperation(
    uint Ordinal,
    LocalBlock Position,
    ReplacePredicate Predicate,
    CanonicalBlockState State,
    BlockEntityDescriptor? BlockEntity,
    EntityDescriptor? Entity);
```

Operations are absolute sets/removes/upserts—never “increment,” “append if absent,” or callbacks with hidden side effects. They are sorted by `(structure priority class, StructureId, Ordinal)`. Every operation must target the named section. A write outside it rejects the entire patch.

Replace predicates are a closed, versioned set:

- `RequireExact(stateKey)`
- `ReplaceAir`
- `ReplaceTag(tagKey)` for a frozen worldgen tag such as `vibecraft:worldgen_replaceable`
- `CarveTag(tagKey)`
- `AlwaysWithinUnpublishedBaseline` for explicitly authored foundations only

Predicates evaluate against the deterministic generation pipeline snapshot, never a player's later live state. Arbitrary mod callbacks are forbidden inside rasterization. Definitions can add data-driven predicates only through a new reviewed contract version.

Structure rasterization occurs after base density, biome, carvers, and surface, and before local features. Accepted structures may emit exclusion volumes or named sockets for later decorations. Local features cannot overwrite protected structure states unless the structure explicitly permits that tag.

### Deterministic conflicts

Definitions select one conflict policy:

- `Exclusive`: overlapping protected AABBs cannot coexist; the canonical lower conflict rank wins.
- `TerrainIntegrated`: AABBs may overlap, but operation conflicts resolve in canonical patch/ordinal order and predicates remain authoritative.
- `Decoration`: may place only into declared replace tags and loses to protected structural writes.
- `SocketOnly`: may write only through sockets exported by another accepted plan.

To decide an `Exclusive` conflict without request-order dependence, a candidate evaluates every possible competing candidate in the finite radius derived from both definitions' maximum footprints. A candidate is accepted only if no viable higher-ranked competitor overlaps it. Candidate recursion is flattened into a bounded sorted batch; planners may not recursively request arbitrary candidates.

If two supposedly nonconflicting accepted patches target the same position and both predicates pass, canonical order produces one specified result and records a collision diagnostic. Built-in fixture suites must treat unapproved collisions as errors. “Whichever worker finishes last” is never a policy.

### Idempotence, identity, loot, and entities

For each target section, rasterization and publication calculate the same `PatchHash`. In the section publication transaction:

1. If no application receipt exists, verify expected section/stage hash, apply operations, materialize block entities/loot/entities, and insert the receipt with the new section revision.
2. If a receipt has the same plan and patch hashes, treat the attempt as an idempotent no-op.
3. If a receipt or index has a different hash, fail closed as corruption/nondeterminism.

Publication gathers **all** accepted indexed plans for the section, verifies candidate completeness, rasterizes them, sorts all operations canonically, applies the resulting section state once, and inserts all missing receipts in one transaction. A normal section can never expose a prefix in which one overlapping structure committed and another did not.

Stable child identities are derived from the plan:

```text
childId256 = SHA-256(structureId || markerKind || localMarkerOrdinal)
entityUuid = RFC-4122-compatible 128-bit encoding of childId256[0..15]
lootSeed = SHA-256(structureId || "loot" || localContainerOrdinal)
```

UUID version/variant bits and collision checks are explicit. Block entities/entities use upsert-by-stable-ID in the same transaction, so retry cannot duplicate a chest, spawner, painting, or villager.

V1 materializes container inventory during section publication using the exact profile's loot-table key and hash. It does not wait until first open, where a content update could change results. Empty/generated state, inventory, and application receipt commit atomically.

### Chunk boundaries, unload/reload, and partial visibility

- A plan does not force-load or force-generate all target sections.
- Each section independently materializes its clipped patch before that section becomes visible/active.
- Seeing part of a structure at the edge of unexplored terrain is allowed; seeing a half-applied patch within a published section is not.
- Unload discards only plan/raster caches. Plans, indexes, section generation stamps, and receipts remain durable.
- Reload verifies receipts against section revisions. It does not replay already applied operations.
- If a player edits one generated piece and later explores another target section, only the new section receives its patch. The modified section is never revisited.
- If an intersecting section was finalized without the required receipt, classify this as candidate-completeness corruption. Do not retroactively write into it during normal loading.

### Mods and migrations

- Structure type keys, planner/rasterizer hashes, normalized plan schema, block state keys, tags, and loot hashes are generator-profile inputs under `GAME-01` and `WORLD-06`.
- Updating a structure definition creates a new profile/epoch. Existing plans retain old IDs, hashes, and behavior for their ungenerated target sections.
- Removing a mod does not erase existing blocks. Planning/rasterization that needs its missing exact package fails with the structure/profile fingerprint shown to the operator.
- Unknown generated blocks already persisted use `GAME-01`/`WORLD-09` opaque placeholders. New terrain is not generated with placeholders as a silent substitute for missing profile content.
- Plan-schema migration changes representation while preserving `StructureId`, `PlanHash` semantics, definition identity, and receipts. A migration that changes output must create a new plan/epoch and may not masquerade as format conversion.
- Intentional retrofitting of a new structure into generated terrain is a separate administrator migration with backup, player-block conflict policy, dry-run diff, and durable receipts. It is not world generation.

## Required data and API contracts

- `IStructurePlanner.PlanCandidate(...)` is pure, bounded, profile-pinned, and returns a normalized accepted plan or stable rejection.
- `IStructureCandidateQuery.PossiblyIntersecting(section)` performs finite inverse-footprint enumeration.
- `IStructureRepository.CommitDecisionBatch(...)` atomically reserves footprint tiles and writes candidates/plans/indexes.
- `IStructureRasterizer.Rasterize(plan, target, preStructureSnapshot)` returns a target-only immutable patch.
- `IWorldgenSampleView` exposes bounded pre-structure profile samples and never loaded/player state.
- `StructurePublication.ApplyOnce(...)` verifies plan/index/patch hashes and writes block data, child objects, loot, receipt, generation stamp, and revision atomically.
- `LocateStructure` searches candidate cells in a caller-supplied finite radius, can perform deterministic viability checks without generating sections, and reports “candidate/viable/planned/materialized” distinctly. It never searches forever or claims a materialized structure merely because a grid position exists.

## Failure modes and required behavior

| Failure | Required behavior |
| --- | --- |
| Two workers evaluate the same candidate | Simulation thread commits one identical decision; differing hashes stop publication |
| Crash after plan commit but before section publish | Durable plan remains; retry produces the same patch |
| Crash during section/loot/entity publication | Atomic old-or-complete-new revision; no duplicate child identity |
| Target section generated before a future origin section | Inverse-footprint planning already considered that origin candidate; no late neighbor writes |
| Plan touches another epoch | Reject `CrossEpochFootprint` in v1; do not clip or rewrite |
| Player edits an earlier piece | Preserve it; materialize only still-ungenerated target sections |
| Exact planner/rasterizer or content missing | Existing materialized sections load; affected planning/generation fails closed |
| Plan exceeds AABB/section/op/byte ceiling | Reject before persistence/publication with type and measured limit |
| Operation escapes target section | Reject whole patch as implementation error |
| Receipt hash differs from recomputation | Quarantine section/plan as corruption or nondeterminism; never replay |
| Structure/entity ID collision | Fatal diagnostic with both source plans; no random fallback ID |
| Checked AABB arithmetic overflows | Reject candidate before database lookup/allocation |
| Unrecognized replace predicate/schema | Keep data recoverable; refuse rasterization until exact code/migration is available |

## Acceptance criteria

### Determinism and order independence

- A golden corpus with at least 100 structure plans of every built-in type produces identical candidate decisions, plan bytes, plan hashes, target indexes, patches, and final section hashes on supported Windows/Linux builds.
- For each fixture, generate intersecting sections in forward, reverse, random, nearest-player, vertical-first, duplicated, canceled/retried, unload/reload, and 1/2/4/8-worker schedules. All canonical hashes and child IDs must match.
- A structure spanning at least 3×3×3 sections is tested through every permutation of a reduced representative target set plus 10,000 randomized full schedules.
- Randomized hash-map insertion and candidate discovery order never alter winners or collision diagnostics.

### Persistence and idempotence

- Reapplying every patch 100 times yields one receipt and one instance of each block entity/entity/container inventory.
- Fault injection at every candidate, plan, tile reservation, index, section, loot, child-object, and receipt write boundary yields a valid old or complete new transaction state.
- A plan committed before a forced process exit materializes identically after restart with all caches empty.
- Corrupting each plan/index/receipt hash independently causes a fail-closed diagnostic and zero block writes.
- An already finalized section without a required receipt is detected before any adjacent section patch is published.

### Boundaries, edits, and epochs

- Every face/edge/corner intersection of a section boundary has a fixture for rotation and mirror; patches contain no out-of-target operation and their union equals a one-shot reference rasterization.
- Editing all blocks in one generated target section, unloading it, then generating every remaining target causes zero writes/revision changes to the edited section.
- Accepted plans atomically reserve every unassigned footprint tile. A candidate touching an old/new epoch frontier is rejected identically from either exploration direction.
- No structure request loads or generates a non-target section solely to place blocks.

### Bounds and performance

- Boundary fuzzing around negative coordinates and `long.MinValue`/`long.MaxValue` never wraps candidate cells, AABBs, target sections, or tile reservations.
- Every over-limit definition/plan is rejected before allocating more than 64 MiB or writing any candidate/plan row.
- On the baseline development CPU after warm-up, built-in M0 candidate planning is below 10 ms p99 with under 2 MiB temporary allocation, and rasterization for one section is below 5 ms p99 with under 1 MiB temporary allocation. Complex future types must declare separately approved budgets.
- Planning an idle/already-decided candidate is a bounded indexed lookup; unloading/reloading 10,000 times causes no plan growth.
- Database queries for one target use the section index and do not scan all world structures.

### Content and migrations

- Templates round-trip canonical namespaced states/properties, markers, block-entity data, transforms, replace predicates, and feature exclusions without runtime IDs.
- Removing each structure mod/package leaves existing sections loadable and causes exact actionable failures only where further planning/rasterization needs it.
- Updating a definition creates different profile/plan identities while leaving all old plan/application hashes unchanged.
- A format-only migration preserves canonical plan meaning and regenerates identical per-section patch hashes.

## Prototype / validation spike

Timebox: 7 engineering days after `GAME-01`, `WORLD-03`, and the `WORLD-06` profile prototype exist.

1. Implement candidate/plan/index/application tables and a pure planner for a rotated multi-section ruin, a large tree, and an underground room.
2. Implement normalized canonical plan encoding, target-only rasterization, replace predicates, exclusions, stable chest/entity identities, and eager deterministic loot.
3. Run randomized section/worker/cancel/restart schedules and compare plan/patch/final hashes against one-shot references.
4. Inject crashes through plan and section publication; deliberately duplicate every job and prove idempotence.
5. Add two overlapping candidate types and prove canonical exclusive winner selection independent of discovery direction.
6. Create an epoch frontier, a player-edited partial structure, missing-package cases, over-limit plans, and signed-coordinate overflow fuzz cases.
7. Record planner/raster/database timing and allocations. Inspect plans and collisions in a debug visualizer keyed by `StructureId`.

Greenlight only if candidate completeness, deterministic conflict choice, target clipping, old-section preservation, idempotent child identity, fault injection, and hard-limit tests pass. If a large feature cannot fit the finite contract, design a hierarchical region/site planner rather than allowing unbounded structure callbacks.

## Risks and open questions

- Persisting rejected candidates consumes storage. Measure it and use compact reason/input hashes; do not remove the record until deterministic recomputation and profile retention are proven cheap enough.
- Plan blobs can become a second content format. Keep them normalized, versioned, bounded, inspectable, and covered by migration fixtures.
- Exclusive conflict radius depends on truthful maximum footprints. Validation must reject any raster output outside the declared bound.
- Eager loot makes generation cost and disk use slightly higher but avoids first-open version drift and duplication. If deferred loot is later desired, it needs its own exact-table fingerprint and atomic receipt contract.
- A bounded site may legitimately span more than 4,096 sections. That is a different hierarchical feature class, not evidence for unlimited per-structure height or callbacks.
- Partial visibility at the explored frontier is unavoidable without force-generating terrain. Art direction should make clipped exploration edges acceptable.
- Structure locate commands can become denial-of-service tools. Radius, candidate count, and per-request CPU budgets must be permissioned and explicit.

## Dependencies

- Requires: `FOUNDATION-00`, `GAME-01`, `ARCH-02`, `WORLD-01`, `WORLD-02`, `WORLD-03`, `WORLD-04`, `WORLD-06`, `WORLD-09`.
- Coordinates with: biome/terrain sampling, local features, loot/content definitions, block entities/entities, lighting/finalization, plugin trust, admin repair/migration, and debug visualization.
- Blocks: villages/ruins/dungeons/large trees, structure locate/index APIs, structure mods, deterministic worldgen tests, and safe cross-section feature publication.

## Rejected or deferred alternatives

- Java/Minecraft bug-for-bug placement: rejected because random-consumption and historical implementation quirks are not a stable VibeCraft contract.
- Direct writes to loaded neighbor chunks: rejected due to order dependence, races, player overwrite, and crash inconsistency.
- Raw deferred block-write bags: rejected because they lose semantic identity, conflict policy, version provenance, and child-object idempotence.
- Force-generating every touched section: rejected because a structure could amplify one request into large CPU/memory/disk work.
- Clipping ordinary structures at generation-epoch seams: rejected because it creates silently broken buildings; v1 rejects the candidate.
- Unlimited vertical/recursive structures: rejected; every plan and lookup must be finite and preflightable.
- First-open unversioned loot: rejected because content updates and retries alter durable results.
- Retroactive normal-load completion into generated sections: rejected because it can overwrite player state.
- Arbitrary callbacks for replace/conflict rules: rejected because output and resource use would no longer be auditable or deterministic.

## Source-quality notes

The linked Voxel Tools, Luanti, Veloren, Terasology, and Cubiomes materials are primary project documentation or open-source implementation evidence. Mapped modern Minecraft classes are secondary evidence of current architecture, not an official historical structure specification. Candidate grids, persistence schema, IDs, limits, conflict ranks, eager-loot rule, epoch reservation, and acceptance thresholds are VibeCraft proposals and must be validated by the executable spike.
