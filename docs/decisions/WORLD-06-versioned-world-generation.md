# WORLD-06 Versioned deterministic world generation

Status: Proposed

Owner: Gameplay/world-generation research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Pin every dimension to an immutable, content-addressed generator profile; reserve horizontal generation tiles to a persisted generation epoch before doing work; run bounded pure stages with counter-derived random streams; publish target-section patches through the simulation thread; and permit a new generator only through an explicit epoch upgrade with a registered seam adapter.

One-sentence rationale: A seed is not a world-generation specification, so reproducibility requires preserving the generator, configuration, content snapshot, stage graph, random algorithm, and ownership of every generated region.

An engine update does **not** change the active generator of an existing world. Existing terrain is authoritative and is never silently regenerated. If the exact generator package is unavailable, already generated sections remain playable while generation of affected ungenerated space fails closed.

The design document's “no max height” goal is interpreted as sparse signed coordinates without a globally dense height array—not permission for an unbounded generation job. Every generator profile declares a finite vertical generation envelope, every stage declares a finite read halo and output extent, and every request names a finite set of 16³ sections. V1 should ship a deliberately finite natural-terrain band and allow sparse building outside it within `WORLD-01`'s operational safety bounds.

## Context and constraints

- `WORLD-01` chooses sparse 16³ sections and signed 64-bit section/block coordinates. A generator must not allocate, scan, or serialize an entire vertical column.
- `WORLD-02` requires bounded priority scheduling, immutable worker results, keyed randomness, and simulation-thread publication.
- `WORLD-03` and `WORLD-04` require one SQLite authority, generated-span metadata, revisions, crash-safe atomic publication, and corruption detection.
- `WORLD-09` separates save-schema migration from generator identity. Migrating a row must not relabel old terrain as if a new algorithm produced it.
- Content registries and mods can alter valid states, biomes, features, and structures. A numeric seed alone cannot identify those inputs.
- Sections may be requested in arbitrary order, concurrently, after restart, at negative coordinates, or from opposite sides of a version frontier.
- Players will modify boundary terrain. A seam system must never overwrite those modifications to make a screenshot prettier.
- “Same seed” has two useful meanings: same profile must reproduce byte-identical generated output; different profiles may intentionally produce different worlds.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| Always use the newest generator for unvisited sections | No old code to support; improvements appear automatically | World shape changes after upgrades; seams and bug fixes are uncontrolled; missing mods silently substitute content | Reject |
| Persist only seed plus a generator version integer | Small and familiar | Integer does not cover config, registry content, stage graph, RNG, or patched binaries | Reject |
| Embed a complete generated baseline for the whole possible world | Exact forever | Impossible for an open sparse world and defeats procedural generation | Reject |
| Pin a content-addressed profile; no mixed-profile upgrades | Strongest reproducibility and simplest support | Existing worlds cannot opt into improved unexplored terrain | Safe v1 minimum |
| Pin profiles and add explicit persisted epochs with pair-specific seam adapters | Reproducible by default; controlled opt-in upgrades; no rewriting old blocks | More metadata, retained generator packages, pairwise seam testing, visible product choice | **Recommend** |

## Evidence

### Minecraft

Minecraft Java 1.18 introduced “blending” at borders between old and new terrain, while explicitly warning that abrupt biome borders could still occur. This is evidence that changing a generator under an existing world is a migration problem, not merely selecting a new noise function ([Mojang Java 1.18 release notes](https://feedback.minecraft.net/hc/en-us/articles/4415128577293-Minecraft-Java-Edition-1-18)). Bedrock's corresponding release described blending old and new chunks and changes to world height as coordinated upgrade features rather than transparent implementation details ([Mojang Bedrock 1.18 release notes](https://feedback.minecraft.net/hc/en-us/articles/4414284658701-Minecraft-Caves-Cliffs-Part-II-1-18-0-Bedrock)).

Modern Java exposes an ordered status pipeline including structure starts/references, biomes, noise, surface, carvers, features, and lighting. The useful lesson is explicit stage readiness; VibeCraft need not copy the exact order or chunk representation ([mapped `ChunkStatus`](https://mappings.dev/1.21.8/net/minecraft/world/level/chunk/status/ChunkStatus.html), [mapped `NoiseBasedChunkGenerator`](https://mappings.dev/1.21.8/net/minecraft/world/level/levelgen/NoiseBasedChunkGenerator.html)).

Minecraft's history argues against bug-for-bug generator compatibility. It would require preserving undocumented random-consumption order, overflow behavior, data-pack interactions, and bugs indefinitely. VibeCraft instead promises compatibility with its own declared profile fingerprint and golden fixtures.

### Voxel Tools

Voxel Tools requires generators to be deterministic because blocks may be regenerated and notes that generation blocks can be requested in an unpredictable neighbor order. Its multipass API exists for cross-block work, and its documentation recommends precomputed blueprints for very large structures ([generator documentation](https://voxel-tools.readthedocs.io/en/latest/generators/), [procedural generation](https://voxel-tools.readthedocs.io/en/latest/procedural_generation/)). This supports pure target-bounded generation and a separate persisted planning layer rather than mutating whichever neighbors happen to be loaded.

Voxel Tools also distinguishes a fixed vertical generation block from terrain that can extend vertically. VibeCraft adopts the useful part—sparse coordinates—while adding finite per-profile envelopes and hard resource limits. “Vertically unbounded storage” must never mean “iterate until no more terrain.”

### Luanti

Luanti passes a deterministic `blockseed` to map generation callbacks and separates mapgen selection/settings from the world's ordinary block data ([core/mapgen callback API](https://api.luanti.org/core-namespace-reference/), [mapgen overview](https://docs.luanti.org/for-creators/mapgen/)). Its debugging guidance notes that equal seeds require equal mapgen settings to reproduce worlds ([debugging worlds](https://docs.luanti.org/for-creators/debug/)). VibeCraft goes further by hashing all effective settings and content and by making random streams independent of callback order.

### Veloren

Veloren's open world simulation stores versioned world-map representations and has had to expand a user seed into the internal seed material expected by world generation ([world simulation source](https://gitlab.com/veloren/veloren/-/blob/master/world/src/sim/mod.rs), [seed expansion merge request](https://gitlab.com/veloren/veloren/-/merge_requests/410)). The lesson is that a public seed and an internal generated-world representation evolve independently; both require explicit versioning.

### Terasology

Terasology modules carry IDs, versions, and dependency metadata, and a world can select a generator supplied by a module ([module documentation](https://metaterasology.github.io/docs/concepts/modules.html)). This supports treating generator code and its content dependencies as world-level packages, not whichever plugin happens to resolve at startup.

## Normative model

### Terms

- **Generator profile:** immutable effective generator code, configuration, content, RNG, and stage graph.
- **Generation epoch:** a world-local monotonically increasing ID selecting one profile and seam policy for future tile reservations.
- **Generation tile:** an 8×8 horizontal group of section columns (128×128 blocks), extending only as metadata across Y. It is not a dense vertical allocation.
- **Generation stamp:** per-section proof of which epoch and stages produced the persisted baseline.
- **Seam plan:** immutable instructions for a boundary between two epochs, created before a new-profile section touching that boundary is generated.

### Generator profile identity

The canonical serialized contract is:

```csharp
public sealed record GeneratorProfile(
    ResourceKey ProfileKey,               // e.g. vibecraft:classic_overworld
    uint ProfileSchemaVersion,
    string PackageSha256,                 // exact generator implementation package
    string CanonicalConfigSha256,
    string StageGraphSha256,
    string RegistrySnapshotSha256,        // states, biomes, feature/structure definitions
    RngSpecification Rng,
    GenerationEnvelope Envelope,
    IReadOnlyList<StageDescriptor> Stages);

public sealed record RngSpecification(
    string Algorithm,                     // v1: vibecraft:philox4x32_10
    uint AlgorithmVersion,
    string KeyDerivation);                // v1: SHA-256 with fixed field encoding

public sealed record GenerationEnvelope(
    long MinSectionY,
    long MaxSectionY,                     // inclusive; must be >= MinSectionY
    OutsideEnvelopePolicy Below,
    OutsideEnvelopePolicy Above);
```

`ProfileFingerprint = SHA-256(canonical GeneratorProfile bytes)`. Canonical encoding uses UTF-8 resource keys, unsigned LEB128 lengths, big-endian fixed-width integers, sorted maps, and no floating-point JSON. Generator math must specify integer/fixed-point operations or a tested cross-platform floating-point mode; “whatever `MathF` does” is not a stable format.

The world stores and displays both the friendly key and fingerprint. A package with the same key/version but a different fingerprint is different code and cannot impersonate the old profile.

### Epoch and tile ownership

```sql
generation_epochs(
  dimension_key TEXT,
  epoch_id INTEGER,
  profile_fingerprint BLOB,
  predecessor_epoch_id INTEGER NULL,
  seam_adapter_key TEXT NULL,
  seam_adapter_hash BLOB NULL,
  blend_width_blocks INTEGER NOT NULL,
  activated_revision INTEGER,
  PRIMARY KEY(dimension_key, epoch_id)
)

generation_tiles(
  dimension_key TEXT,
  tile_x INTEGER,
  tile_z INTEGER,
  epoch_id INTEGER,
  reserved_revision INTEGER,
  PRIMARY KEY(dimension_key, tile_x, tile_z)
)
```

The active epoch is dimension metadata. Before scheduling any section in an unreserved tile, the simulation thread atomically inserts that tile's epoch. All vertical sections in the tile use that epoch. Concurrent requests therefore cannot choose different generators based on worker timing.

Tile coordinates use mathematical floor division, `tileX = floorDiv(sectionX, 8)` and `tileZ = floorDiv(sectionZ, 8)`, so sections `-1..-8` belong to tile `-1`. Language-default truncation toward zero is forbidden and covered by coordinate fixtures.

Changing the active epoch uses a world-write barrier:

1. Stop accepting new generation reservations for the dimension.
2. Drain or cancel unpublished jobs and publish/rollback completed transactions.
3. Verify the new profile package, registry snapshot, migration preflight, and seam adapter for every neighboring old epoch.
4. Commit the new epoch and active-epoch pointer atomically.
5. Resume reservations. Existing tile rows retain their old epoch; new rows receive the new epoch.

Deleting a tile reservation to make it use a newer generator is a destructive admin operation and is forbidden once any section or structure plan in that tile has been published.

### Randomness

No stage receives a mutable world-global PRNG. It requests a stream by semantic identity:

```text
streamKey = SHA-256(
  "VibeCraft worldgen stream v1" ||
  worldSeed[32] || dimensionKey || epochId ||
  stageKey || featureKey || originX || originY || originZ || attemptIndex)
random = Philox4x32-10(streamKey[0..127], counter)
```

Fields have the canonical encoding above and signed coordinates use 64-bit two's-complement big-endian values. Adding a new feature or changing loop order cannot consume another feature's random values. A stage may derive child streams only by another stable key; it may not split based on completion order.

### Stage contract

The v1 profile uses this logical order:

1. `base_density`
2. `biomes`
3. `carvers`
4. `surface`
5. `structure_plans`
6. `structure_raster`
7. `local_features`
8. `finalize`

Lighting is downstream world processing, not part of the generator fingerprint. `WORLD-07` specifies the two structure stages. Profiles may add stages only by changing their fingerprint.

```csharp
public interface IGenerationStage
{
    ResourceKey Key { get; }
    uint ContractVersion { get; }
    FiniteBounds DeclaredReadHalo { get; }
    FiniteBounds DeclaredOutputBounds { get; } // v1: target section only
    GenerationPatch Execute(in GenerationContext context,
                            in SectionKey target,
                            CancellationToken cancellation);
}

public sealed record GenerationPatch(
    SectionKey Target,
    uint CompletedStageMask,
    uint ExpectedEpochId,
    Hash256 InputFingerprint,
    Hash256 OutputHash,
    ImmutableArray<BlockWrite> Blocks,
    ImmutableArray<BiomeWrite> Biomes,
    ImmutableArray<MetadataWrite> Metadata);
```

A stage can read only immutable previous-stage samples from the pinned profile, declared structure plans, and its own bounded halo. It cannot inspect loaded chunks, player blocks, wall time, thread IDs, network state, runtime registry IDs, or unordered hash-map iteration. Cross-section effects are expressed as coordinate-owned plans and clipped target-section patches, never direct neighbor mutation.

The scheduler rejects a stage whose declared halo/output exceeds profile limits. V1 profile limits are measured during the prototype and stored in the profile; no mod can request “all Y” or an unbounded radius.

### Publication and generation stamp

Workers may run, cancel, retry, and finish out of order. Only the simulation thread publishes. In one `WORLD-04` transaction it verifies:

- the tile still has the expected epoch;
- the target is absent or has exactly the expected partial-stage stamp;
- all referenced plan/input hashes match;
- every state key resolves through the world's `GAME-01` mapping;
- the patch writes only its declared target and remains within hard count/byte limits.

It then writes section data, generated-span/column metadata, structure application receipts, and:

```csharp
public sealed record GenerationStamp(
    uint EpochId,
    Hash256 ProfileFingerprint,
    uint CompletedStageMask,
    Hash256 BaselineOutputHash,
    long GeneratedRevision, // checked nonnegative SectionRevision
    bool ModifiedSinceGeneration);
```

Any later gameplay block mutation flips `ModifiedSinceGeneration` for the section in the same transaction. The baseline hash is provenance and corruption evidence, not permission to replace the section. V1 deliberately uses a conservative section-level modification bit instead of pretending it can always distinguish player edits, fluid motion, pistons, mods, and repairs block by block.

### Explicit generator upgrades and seams

Default policy is no upgrade: an old world keeps generating with its pinned profile, eliminating mixed-version seams.

An opt-in upgrade must register an adapter for each `(oldFingerprint, newFingerprint, dimension)` pair. The generic v1 adapter is allowed only when both profiles expose compatible deterministic samples for base density, surface envelope, fluid level, and biome climate. It uses a persisted default blend width of 256 horizontal blocks:

```text
a = smoothstep(0, blendWidth, distanceIntoNewEpoch)
baseDensity = lerp(oldProfile.sampleDensity(p), newProfile.sampleDensity(p), a)
surface/climate = profile-defined deterministic blend using the same a
```

At every old/new tile edge, the simulation thread creates a `SeamPlan` before scheduling the touching new tile. The plan records both fingerprints, adapter package hash/version, exact frontier geometry, blend width, and plan hash. Generation samples both pinned profiles; it never samples live or player-modified blocks. Old sections are not rewritten.

For several touching edges backed by the same old profile, the default adapter treats their union as one frontier and uses fixed-point Euclidean distance to the nearest point on that union; canonical tie handling makes corners independent of which edge was discovered first. If one new transition band is influenced by two or more **distinct old profile fingerprints**, the pairwise adapter is insufficient: v1 refuses generation there unless a multi-frontier adapter keyed by the sorted complete fingerprint set is installed and preflighted.

Seam rules are explicit:

- The new side owns all blend output. At the frontier `a=0`; at 256 blocks into the new epoch `a=1`.
- Old and new carvers are clipped at the epoch frontier. In the transition band, only a seam adapter's bounded deterministic carver may run; the default adapter fades new carvers from zero to full strength and creates no cross-frontier tunnel guarantee.
- Structures cannot cross an epoch frontier unless `WORLD-07` has a pair-specific structure seam contract. V1 rejects such candidates.
- Local features use the target tile's epoch and are clipped to it in the blend band.
- Biome identity may change sharply where no meaningful mapping exists; the seam report must expose this before the owner confirms the upgrade.
- If either exact profile or adapter is missing, hashes differ, or an adapter exceeds its finite limits, creation of new-profile boundary sections fails. Falling back to a hard seam is not automatic.

This is a continuity mechanism, not a promise that two unrelated terrain algorithms can be made visually identical. The upgrade preflight generates a fixed boundary preview suite and reports height/density discontinuities and rejected structure candidates.

### Finite height and coordinates

- Coordinates and section keys remain signed 64-bit as required by `WORLD-01`.
- A profile's natural terrain exists only within its inclusive finite `GenerationEnvelope`.
- Requests outside the envelope return the profile's explicit constant/procedural outside policy without looping toward a top or bottom. V1 should use air above and a documented finite foundation policy below.
- Structures/features each declare a finite AABB and write count (`WORLD-07`).
- Server operational bounds may be much larger than the natural band but are still checked before coordinate arithmetic, allocation, physics, networking, or persistence.
- A future “sky realm at very high Y” is another finite band/profile feature, not justification for a full-height column array.

### Mods, removal, and migration

A worldgen mod contributes a signed/trusted package containing stable resource keys, exact hashes, stage contracts, configuration schema, and any seam adapters. Native code is allowed only under the server's trusted-plugin policy; a data-only or sandboxed stage is preferred. The package cannot alter a frozen profile in place.

- Installing a mod creates a candidate new profile; it does not mutate existing epochs.
- Removing a package leaves generated terrain loadable. Ungenerated tiles pinned to it return `GeneratorUnavailable(profileFingerprint)` and are not filled by the vanilla generator.
- A replacement package may declare a migration but gets a new fingerprint. The owner must choose between restoring the original package, freezing exploration, or creating an explicit epoch upgrade.
- Save-schema migration may rewrite the encoding of a `GenerationStamp`; it must preserve its epoch/profile identity.
- Registry migration maps stable keys under `GAME-01`. It cannot reinterpret an old numeric runtime ID as a new block.

### Repair and administration

Automatic regeneration is forbidden for persisted sections. An explicit repair command may regenerate only if all are true:

1. the exact profile and all plan inputs are available;
2. the stamp says `ModifiedSinceGeneration == false`;
3. the baseline output hash reproduces in a dry run;
4. the owner confirms the exact section set and backup revision;
5. replacement is committed as a crash-safe audited transaction.

If the dry-run hash differs, classify it as generator nondeterminism or corrupt provenance and stop. Do not “repair” by accepting today's output.

## Required data and API contracts

- `GeneratorPackageStore.GetExact(Hash256)` loads by fingerprint/package hash, never “latest compatible.”
- `GenerationEpochRepository.ReserveTile(...)` is simulation-thread-only and atomic.
- `IGenerationSampler` exposes deterministic bounded samples independent of loaded section state.
- `IGenerationStage` returns immutable target-only patches and declared resource usage.
- `ISeamAdapter` is keyed by exact old/new fingerprints and produces a finite `SeamPlan` plus target-only samples.
- `GenerationStamp` is stored with each section revision and included in diagnostic exports.
- Admin/network status exposes friendly profile key, exact fingerprint, epoch, unavailable package state, pending generation, and seam-upgrade warnings. Clients never choose the generator.

## Failure modes and required behavior

| Failure | Required behavior |
| --- | --- |
| Exact generator package missing | Load existing sections; reject generation for pinned tiles with an actionable error |
| Same key/version but different package bytes | Hash mismatch; refuse to load it as the pinned profile |
| Crash while reserving a tile | Transaction leaves either no row or one durable epoch row |
| Crash after patch write but before receipt | Atomic publication yields old or complete new revision, never an untracked baseline |
| Worker finishes after epoch upgrade barrier | Expected epoch check rejects stale patch; job may be rescheduled under persisted tile owner |
| Two workers produce different hashes | Quarantine result, report nondeterminism, publish neither |
| Player edits an old/new frontier | Preserve edit; seam continues to use generated profile samples and touches only new ungenerated sections |
| Adapter/profile missing at frontier | Stop boundary generation; do not silently hard-seam or rewrite old terrain |
| Mod requests unbounded halo/output | Reject profile at validation |
| Coordinate arithmetic overflows | Checked failure before allocation/database access; no wraparound alias |
| Corrupt generation stamp | Fail closed under `WORLD-04`; restore or administrator repair, never infer a profile |

## Acceptance criteria

### Determinism

- A golden corpus containing at least 10,000 sections across positive/negative X/Y/Z, envelope edges, all biomes, and seam bands produces identical canonical section hashes on supported Windows/Linux builds.
- Hashes are identical with 1, 2, 4, and 8 workers; FIFO, reverse, random, duplicate, cancellation/retry, and restart schedules; and at least 100 randomized seeds.
- Adding an unrelated feature stream to an experimental profile does not change any existing feature's random sequence in an instrumented fixture.
- Every map/dictionary affecting output is covered by a randomized insertion-order test.

### Persistence and upgrades

- Reopening a world after an engine update selects the exact stored profile, never the executable's default.
- Fault injection at every tile-reservation, epoch-switch, seam-plan, and section-publication write boundary yields either the complete prior state or complete new state.
- A new epoch performs zero block writes to previously persisted sections and changes no previous `BaselineOutputHash`.
- Concurrent tile requests across an upgrade barrier always use their persisted tile epoch; 10,000 repetitions produce no mixed ownership within one tile.
- Missing old package, new package, or seam adapter is detected during preflight or first affected request and never triggers substitution.

### Seam quality and safety

- For canonical old/new fixtures, the blend coefficient is exactly 0 at the frontier, exactly 1 at 256 blocks, monotonic between them, and byte-identical regardless of generation direction.
- The default adapter introduces no exposed uninitialized section, foundation wall, NaN/non-finite sample, or write outside the new epoch in the preview corpus.
- Carvers, local features, and rejected cross-epoch structures obey clipping in every cardinal/corner frontier fixture.
- Editing every old-side boundary block before generating the new side results in zero writes to those blocks and deterministic new-side hashes.
- The upgrade UI emits a machine-readable preview report containing maximum generated surface step, rejected candidate count, biome mapping gaps, and both profile/adapter hashes. Product thresholds are chosen from prototype data rather than hidden in code.

### Bounds and performance

- No generation path allocates proportional to the distance between `MinSectionY` and a requested section outside the envelope, or to the world's discovered vertical span.
- A target-section job stays within its declared peak memory and halo. Validation fails profiles that exceed configured hard caps.
- Checked-coordinate fuzzing around `long.MinValue`, `long.MaxValue`, tile division, and section/block conversion never aliases two coordinates or reaches an allocation after overflow.
- Generation cancellation latency is below 50 ms p99 at instrumented cancellation points; no partially published section remains.

## Prototype / validation spike

Timebox: 8 engineering days after registry and section serialization prototypes exist.

1. Implement canonical profile encoding, SHA-256 fingerprints, Philox stream derivation, epoch/tile rows, and an integer/fixed-point two-noise terrain profile.
2. Generate the golden corpus under randomized worker order, cancellation, duplicate scheduling, restart, and two platforms. Deliberately add an unrelated RNG consumer and prove keyed streams isolate output.
3. Implement a visibly different second profile and the 256-block generic density/surface adapter. Create straight, corner, island, and checkerboard epoch-frontier fixtures.
4. Persist generation stamps and inject process termination around reservation/publication. Verify old-or-new atomic outcomes.
5. Remove each generator/adapter package and confirm existing sections load while affected generation fails with exact fingerprints.
6. Exercise finite envelopes at distant positive/negative Y and profile/structure halo rejection. Record CPU, allocation, patch size, and cancellation latency.

Greenlight only if determinism, stale-job rejection, no-old-block-write, missing-package, and overflow tests all pass. If generic seams fail visual/product thresholds, ship pinned profiles without mixed-epoch upgrades; do not weaken provenance to hide seams.

## Risks and open questions

- Long-lived worlds require retaining exact generator packages or accepting that exploration can stop. Package export/backup must be part of world administration.
- A 128×128 tile and 256-block blend width are provisional product constants. Changing either is a new epoch/seam contract, not a hidden tuning tweak.
- Profile fingerprints can prove input identity, not prove a binary is safe. Server operators still need trust/sandbox policy for worldgen mods.
- Floating-point determinism across architectures is easy to overclaim. The prototype should prefer integer/fixed-point noise or pin and test exact operations before promising cross-platform hashes.
- Generic density blending cannot preserve every cave, aquifer, biome, or feature relationship. Pair-specific adapters or a refused upgrade are legitimate outcomes.
- Section-level modification protection is conservative and may prevent automatic repair of mostly natural sections. That is safer than deleting a build.
- Keeping old profiles active alongside new registry content may require their exact frozen registry snapshot and opaque placeholders from `GAME-01`/`WORLD-09`.

## Dependencies

- Requires: `FOUNDATION-00`, `GAME-01`, `ARCH-02`, `WORLD-01`, `WORLD-02`, `WORLD-03`, `WORLD-04`, `WORLD-09`.
- Coordinates with: `WORLD-07` structure planning, lighting/finalization, plugin packaging/trust, backup/export, and server administration UI.
- Blocks: production terrain generator, generator mod API, world creation profile selection, explicit generator-upgrade tooling, and deterministic worldgen CI fixtures.

## Rejected or deferred alternatives

- Bug-for-bug Minecraft seed compatibility: rejected; it turns undocumented implementation accidents and every historical dependency into VibeCraft's permanent file format.
- Automatic newest-generator adoption: rejected because it changes unexplored terrain without owner consent.
- Unlimited vertical generation pass: rejected; sparse coordinates do not remove finite CPU/memory requirements.
- Reading live neighboring chunks in workers: rejected because output would depend on load order and player edits.
- Mutable sequential world RNG: rejected because unrelated code changes consume the stream and alter distant output.
- Rewriting old terrain to hide seams: rejected because generated aesthetics do not outrank persisted player state.
- Silent fallback when a generator/mod is missing: rejected because plausible but incorrect terrain is durable corruption.
- Per-block natural/player provenance in v1: deferred; block movement, fluids, mods, and replacements make it more complex than a trustworthy bit.

## Source-quality notes

Mojang release notes and the linked Voxel Tools, Luanti, Veloren, and Terasology project documentation/source are primary vendor or open-source implementation evidence. Mapped Minecraft class names are secondary evidence of current stage organization, not a supported historical specification. The epoch/tile size, profile encoding, RNG selection, finite-envelope policy, seam formula, limits, and repair rules are VibeCraft proposals and require the executable criteria above.
