# GAME-01 Content registries, block states, tags, and recipes

Status: Proposed

Owner: Gameplay/world-generation research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Use typed, frozen, namespaced content registries; represent ordinary block variation as finite canonical block states; persist a never-reused world-local `uint32` for each state alongside its canonical name/properties; and build a separate dense session mapping for runtime and network use.

One-sentence rationale: Names survive pack order and software updates, compact integers keep voxel arrays fast, and an explicit saved mapping preserves unknown content instead of turning a missing mod into air or a different block.

This is not a Minecraft compatibility layer. Minecraft-like names, tags, recipes, and state properties are useful authoring concepts, but VibeCraft owns its schemas and migration rules. Java 1.0-era numeric IDs and metadata are specifically the history not to repeat.

## Context and constraints

- A section contains 4,096 ordinary block states and cannot store strings or objects per voxel.
- Saves may outlive engine releases and may be opened while an optional mod is missing.
- Dedicated servers and clients need an agreed content set, but numeric registration order is not stable enough to be a save or protocol identity.
- Blocks, items, entities, block entities, biomes, recipes, tags, structures, and generator components have different schemas and must not share one untyped integer namespace.
- `ARCH-02` requires dense block arrays plus sparse block entities. Arbitrary per-position property dictionaries would break that split.
- `WORLD-01` already fixes section state entries at `uint32`; `WORLD-03` already proposes a persisted `block_state_registry`; this decision defines their semantics.
- Content extensions need useful composition without permission to replace host invariants, mutate frozen registries during a world tick, or erase foreign data.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| Hard-coded global numeric IDs and metadata bits | Small and fast; resembles early Minecraft | IDs collide, metadata exhausts quickly, load order leaks into saves, renames require global rewrites | Reject |
| Store canonical strings/property maps in every voxel | Durable and inspectable | Catastrophic repetition, larger network/disk data, slow hot-path comparison | Reject |
| Assign numeric IDs from mod load order and save only numbers | Fast runtime and simple bootstrap | A reordered or missing mod can reinterpret every persisted number | Reject |
| Canonical names + saved world IDs + disposable session IDs | Stable identity, compact sections, missing-content preservation, independently optimizable transport | Three mappings and explicit migration tooling | **Recommend** |
| Content-address every definition and make the hash its identity | Detects every definition change | Cosmetic/balance edits become new identity; references and authoring are hostile; hash changes do not explain migration | Use hashes for agreement/provenance, not identity |

## Evidence

### Minecraft

- Java's 17w47a “Flattening” removed block/item data values, split or renamed almost every block and item, and was described by Mojang as years of refactoring that would break existing assumptions. It is direct evidence that a compact numeric layout can become severe compatibility debt when treated as durable identity ([official 17w47a/17w47b notes](https://www.minecraft.net/en-us/article/minecraft-snapshot-17w47a)).
- Modern Java uses a namespace/path `ResourceLocation`; its mapped registry interface exposes both resource keys and raw integer IDs, tags, codecs, and a `freeze` operation. These mappings come from Mojang binaries and are secondary implementation evidence, not a supported API specification ([mapped `ResourceLocation`](https://mappings.dev/1.21.8/net/minecraft/resources/ResourceLocation.html), [mapped `Registry`](https://mappings.dev/1.21.8/net/minecraft/core/Registry.html)). The useful shape is durable keys plus compact runtime indexing, not Java's exact classes.
- Minecraft Java's DataFixerUpper is published by Mojang and builds schema-versioned transformations between old and new data forms. Its existence supports explicit migration, while its complexity argues against cloning a generic recursive rewrite framework before VibeCraft has real formats ([DataFixerUpper](https://github.com/Mojang/DataFixerUpper)).
- Bedrock's supported creator format requires namespaced block identifiers, finite block states, permutations, and namespaced tags. Its recipe format has shaped, shapeless, and processing forms and an explicit priority field ([block descriptions](https://learn.microsoft.com/en-us/minecraft/creator/reference/content/blockreference/examples/blockdescription), [states and permutations](https://learn.microsoft.com/en-us/minecraft/creator/reference/content/blockreference/examples/blockstatesandpermutations), [block tags](https://learn.microsoft.com/en-us/minecraft/creator/reference/content/blockreference/examples/blockcomponents/minecraftblock_tag), [recipe introduction](https://learn.microsoft.com/en-us/minecraft/creator/documents/recipeintroduction)). Bedrock's breaking tag-definition change in 1.26.20 is also evidence that the syntax version and semantic content identity must be separate ([creator update notes](https://learn.microsoft.com/en-us/minecraft/creator/documents/update1.26.20)).

### Clones, engines, and open implementations

- Luanti exposes names such as `default:stone` to content authors, compact Content IDs to voxel-manipulation hot paths, and a per-mapblock name/ID mapping in serialized data. Content IDs are stable only for one registration/execution, while the saved mapping reconnects numbers to names ([itemstrings](https://docs.luanti.org/for-players/itemstrings/), [Content ID API](https://api.luanti.org/lua-voxel-manipulator/), [MapBlock layout](https://docs.luanti.org/for-engine-devs/basic-data-structures/)). This is the closest direct precedent for the recommended three-layer model.
- Luanti has explicit `unknown`, `air`, and `ignore` nodes; unknown content is not identical to empty or ungenerated space. It also documents aliases and one-shot loading modifiers for world compatibility ([basic structures](https://docs.luanti.org/for-engine-devs/basic-data-structures/), [compatibility guidance](https://docs.luanti.org/for-creators/keeping-world-compatibility/)). VibeCraft should preserve the distinction but make migrations transactional rather than relying on arbitrary load callbacks.
- Luanti groups participate in digging, damage, and recipes, including intersection of multiple groups. This demonstrates the flexibility of declarative sets, but a stringly typed group with many unrelated meanings is difficult to validate; VibeCraft should use typed tags per registry ([Luanti groups](https://api.luanti.org/groups/)).
- Terasology addresses block families and variants with module-qualified URIs, keeps ordinary block attributes compact, and uses block-backed entities for additional state. Its block definitions, shapes, and prefab behavior are separate assets ([block access](https://metaterasology.github.io/docs/developing/blocks/accessingBlocks.html), [block definitions](https://metaterasology.github.io/docs/developing/blocks/blockDefinition.html), [block-world model](https://metaterasology.github.io/docs/concepts/blockWorld.html)). The separation is useful; VibeCraft should avoid encoding render assets or arbitrary behavior payloads into block-state identity.
- Minestom's open registry work separates registry keys from registry-backed protocol objects and recently made generated registry keys/tags deterministic. It also calls mutation/removal of live registry entries inherently unsafe ([Minestom releases](https://github.com/Minestom/Minestom/releases), [repository](https://github.com/Minestom/Minestom)). That supports a frozen world-session registry and explicit restart/migration boundary.

### Evidence-based findings

The sources establish that namespaced identifiers, compact numeric hot-path IDs, finite states, tags/groups, and a freeze boundary work in real engines. They do not establish one universal file syntax or that every definition can safely be data-driven. The schema and limits below are VibeCraft design inferences and require the prototype.

## Proposed design

### Three identity layers

```text
CanonicalKey       vibecraft:oak_log                  durable author/API identity
CanonicalStateKey  vibecraft:oak_log[axis=y]          durable logical block state
WorldStateId       uint32 allocated once per world     section/save compression
RuntimeStateId     dense uint32 for one server session resolved definition lookup
SessionStateId     dense uint32 for one connection     negotiated network encoding
```

- `CanonicalKey` is lowercase ASCII `namespace:path`; namespace matches `[a-z0-9_.-]+`, path matches `[a-z0-9_./-]+`, each is length-bounded, and Unicode display names live in localization assets.
- Keys are unique only inside a typed registry. `vibecraft:oak` may legally identify both a block and an item, but every serialized reference includes or implies its registry kind.
- A block-state key is the block key plus every declared property in property-name order. Defaults are expanded before identity is calculated; two spellings cannot denote the same state.
- `WorldStateId` is allocated monotonically in `block_state_registry`, never reused, and remains bound to the same canonical state for that world's lifetime. `0` is reserved for `vibecraft:air`; maximum value is a hard world-format exhaustion condition, not wraparound.
- `RuntimeStateId` and `SessionStateId` may be rebuilt. Neither appears in a durable record, replay log, plugin-owned data, or administrative API.
- Persisted item/entity/biome references use canonical keys initially; add world-local integer maps only for a measured hot/dense record family. Do not create numeric IDs merely for symmetry.

### Typed registries and startup lifecycle

Initial registries are `Block`, `Item`, `EntityType`, `BlockEntityType`, `Biome`, `Recipe`, `Structure`, and their corresponding typed tag registries. Component/behavior implementation IDs have a separate trusted host registry.

```text
discover manifests
  -> dependency/version resolution
  -> parse bounded schemas
  -> register canonical definitions
  -> expand and validate block states
  -> resolve typed references and tags
  -> compile recipes/behavior descriptors
  -> reconcile with saved world mappings
  -> compute content fingerprint
  -> freeze
  -> admit world simulation and clients
```

- Registration occurs only at startup or an explicit full-world reload with no players and no live simulation. V1 does not hot-add/remove gameplay definitions.
- Duplicate keys are fatal unless the pack system has explicitly selected one whole definition through its documented override policy. Registration callback order never decides the winner.
- Definitions and tag memberships are sorted canonically before hashing or runtime-ID assignment. Filesystem order, dictionary enumeration, mod load timing, and worker completion are excluded.
- The content fingerprint covers manifest IDs/versions/hashes, resolved definition schemas, state tables, tags, recipes, behavior implementation compatibility IDs, and generator-relevant content. Cosmetic resource-pack data has a separate fingerprint.

### Block and state contract

```csharp
public readonly record struct ContentKey(string Namespace, string Path);
public readonly record struct WorldStateId(uint Value);
public readonly record struct RuntimeStateId(uint Value);

public sealed record BlockProperty(
    ContentKey Key,
    ImmutableArray<string> AllowedValues,
    string DefaultValue);

public sealed record CanonicalBlockState(
    ContentKey Block,
    ImmutableArray<KeyValuePair<ContentKey, string>> Properties);

public sealed record BlockDefinition(
    ContentKey Key,
    ImmutableArray<BlockProperty> Properties,
    ContentKey Behavior,
    ContentKey CollisionShape,
    ContentKey RenderModel,
    ImmutableArray<ContentKey> BlockEntityKinds);
```

- State properties are finite booleans, bounded integers represented canonically, or finite string enums. No floating point, nested objects, callbacks, inventory, text, timers, or arbitrary mod data belongs in a block state.
- Static configuration such as orientation, growth stage, open/closed, repeater delay, and current circuit output may be states when every combination is valid and bounded. Inventories, custom names, piston motion, and unbounded data belong in block entities.
- State expansion is capped provisionally at 4,096 states per block, 1,048,576 total resolved states, 32 properties per block, and 256 values per property. The loader rejects Cartesian explosions before allocating the table.
- A definition declares legal block-entity types. A state transition creates/removes/migrates the sparse record atomically as required by `ARCH-02`.
- Render model and texture references do not form gameplay identity. A resource pack can change appearance without changing the saved state mapping.

### Tags

- Tags are typed namespaced immutable sets: `Tag<Block>`, `Tag<Item>`, and so on. Cross-registry membership is invalid.
- A tag file can list entries and other same-typed tags. Resolution detects cycles, missing required members, duplicate/conflicting declarations, and expansion limits before freeze.
- Pack layering uses one explicit operation per declaration: `replace`, `append`, or `remove`. The final pack order comes from the asset/content-pack decision; load discovery order is irrelevant.
- Built-in logic queries semantic tags such as `vibecraft:mineable/pickaxe`, `vibecraft:logs`, or `vibecraft:worldgen/replaceable`. It does not infer behavior from key prefixes.
- Tags are definitions, not persisted facts. A saved item remains its item key even if a later content version changes tag membership. Such a change is part of the content fingerprint and can intentionally alter future gameplay.

### Items and recipes

- Items are separate from blocks. A placeable `BlockItemBehavior` explicitly references a block and placement state resolver; not every block automatically becomes an item and not every item is a block.
- An `ItemStack` persists item key, count, schema-versioned bounded component payload, and optional stable stack UUID only when uniqueness is required. Stack equality uses a canonical component projection, never object/reference equality.
- Recipes have namespaced IDs and one of a small set of host-owned schemas: shaped grid, shapeless multiset, furnace-like processing, and later explicitly added types.

```text
RecipeDefinition
  key: ContentKey
  type: shaped | shapeless | processing
  priority: int16
  inputs: exact Item key or typed Item tag + count
  output: explicit ItemStack template
  remaining_items: explicit per-input rule
  unlock: optional predicate ID + bounded data
```

- The loader compiles recipes into deterministic matchers. If two recipes of equal priority match the same concrete input fixture and produce different results, loading fails with both IDs; registration order never breaks the tie.
- A client may propose `recipeKey` and slot revisions. The authoritative server reruns the matcher, validates ownership/counts, and commits consumed inputs plus outputs as one inventory transaction.
- In-progress processing stores the recipe key, content fingerprint/definition version, elapsed ticks, and reserved inputs. Missing or changed recipes pause with preserved input rather than converting or deleting it silently.

### Persistence, networking, and missing content

- `block_state_registry(state_id, canonical_name, canonical_properties, definition_version, definition_crc32c)` is authoritative for world IDs. Reconciliation inserts newly used states transactionally and verifies every existing binding before sections load.
- Section palettes store `WorldStateId`. Save/network codecs resolve through immutable mapping snapshots; they never call a mutable global registry during worker execution.
- On connection, `NET-09` first verifies required content manifests. The server then sends or selects a versioned `SessionRegistryMap`; chunk palettes use session IDs only after that map is acknowledged.
- A known canonical state with a changed definition is not automatically a new block. Its content package must declare compatibility or a migration; the world records the resolved content fingerprint.
- An unresolved block state becomes `MissingBlockState(original key, properties, saved definition metadata)`: conservative solid full-cube collision, opaque rendering placeholder, no drops, no ticking, and admin-only replacement/export. It is never air.
- An unresolved item remains an inert stack carrying its original key/components. An unresolved block entity/entity remains a bounded dormant record. Reinstalling a compatible provider resolves it without changing its persistent ID.
- Unknown payloads are length/checksum/schema bounded as required by `WORLD-04`. Missing content never grants execution of stored code or trusts saved collision/behavior callbacks.

### Renames and migrations

- Aliases are migration declarations with source key, destination key, source version range, payload transform ID, and owning namespace. They are not permanent fuzzy lookups.
- One-to-one no-payload renames may migrate lazily through `WORLD-09`; state/property changes require an explicit deterministic transform and fixtures.
- A mod can migrate only keys/payloads in its namespace. Cross-namespace takeover requires an owner-approved world migration manifest.
- Removing content without replacement leaves placeholders. Destructive cleanup is a separate admin operation with a verified backup and report.

### Public interfaces

```csharp
public interface IFrozenRegistry<TDefinition>
{
    TDefinition Get(ContentKey key);
    bool TryGet(ContentKey key, out TDefinition definition);
    RegistrySnapshotId SnapshotId { get; }
}

public interface IBlockStateResolver
{
    RuntimeStateId Resolve(WorldStateId saved);
    WorldStateId GetOrAllocate(CanonicalBlockState state, WorldStoreTransaction tx);
    CanonicalBlockState Describe(WorldStateId saved);
}

public interface IRecipeMatcher
{
    RecipeMatchResult Match(in CraftingInput input);
    CommandResult Craft(RecipeKey proposed, InventoryRevision expected, WorldTransaction tx);
}
```

Plugins receive stable keys, immutable definitions, typed tags, and validated commands. They do not receive mutable registry dictionaries, raw world-ID allocation, or direct section palettes.

## Acceptance / greenlight criteria

- Reordering files, packs with equivalent dependency order, worker completion, or hash-map insertion produces identical canonical registry snapshots and content fingerprints.
- A save made with a test mod survives load/save without that mod and resolves byte-for-byte when the same compatible mod returns; no unknown block becomes air and no unknown item disappears.
- One million random canonical states round-trip `key -> WorldStateId -> saved section -> RuntimeStateId -> key` with no collision or dependence on registration order.
- Malformed names, duplicate definitions, tag cycles, missing required references, recipe ambiguity, and state-space bombs fail before world mutation with actionable diagnostics.
- The default content set stays below the total-state cap, and registry lookup adds no allocation to steady-state block reads.
- A malicious client cannot select a session ID before mapping acknowledgement or craft from a recipe ID without the server reproducing the match and inventory transaction.
- Every supported rename/state migration has old/new golden fixtures; an unsupported future or foreign definition is preserved or fails closed, never silently defaulted.

## Prototype or benchmark

Required: yes

Smallest useful experiment:

1. Implement typed registries, canonical key/state parsing, state expansion, tags, shaped/shapeless recipes, persisted world-ID reconciliation, and session-map serialization without Godot.
2. Generate 100 synthetic mods in randomized discovery order, including 100,000 states, nested tags, and 10,000 recipes; compare canonical fingerprints across 100 runs.
3. Save sections/inventories with one mod, remove it, round-trip placeholders, then restore and migrate it under kill/fault injection through `WORLD-04`.
4. Fuzz names, counts, property products, tag graphs, recipe ambiguity, payload lengths, stale inventory revisions, and network mappings.
5. Benchmark 100 million state lookups, tag membership checks, recipe matches, and palette encode/decode operations in Release builds.

Success metrics:

- All determinism and missing-content greenlight criteria pass.
- Steady block-state resolution is array-indexed, allocation-free, and at least 100 million lookups/second on the declared baseline CPU; the absolute target may be revised only with a recorded benchmark.
- Loading the 100,000-state/10,000-recipe fixture completes in under 5 seconds and 512 MiB peak managed memory on the baseline development machine.
- Every rejected fuzz input remains within configured allocation/recursion/count limits and performs zero world writes.

## Risks and open questions

- The exact trusted behavior-component API belongs to plugin/mod research. A data definition can select only host-registered behavior; it cannot inject unrestricted C#.
- `uint32` provides enormous but finite state capacity. “Unlimited mods/states” is not promised; quotas are part of world safety.
- Tags are powerful enough to alter recipes, tool behavior, and world generation. A changed tag set must participate in content/generator fingerprints even though tag membership is not copied into each save record.
- Saving a full definition snapshot would aid archaeology but could accidentally become executable compatibility behavior. Persist identity, bounded recovery metadata, and hashes—not arbitrary old logic.
- Recipe overlap analysis can be combinatorial for large tags. The loader needs indexed candidate expansion plus limits; equal-priority ambiguity may sometimes require author-supplied test fixtures rather than exhaustive proof.
- A conservative solid missing block can trap a player. Recovery/admin tooling should offer safe teleport and explicit replacement, but preserving the world is more important than pretending the block is air.

## Dependencies

- Requires: `FOUNDATION-00`, `ARCH-02`, `WORLD-01`, `WORLD-03`, `WORLD-04`, `WORLD-09`, `NET-09`.
- Coordinates with: asset-pack override/version policy; `MOD-03` extension stability; `GAME-02` circuit component definitions; `WORLD-06` generator content fingerprints.
- Blocks: section implementation, inventories/crafting, content packs, block entities, gameplay commands, world generation, network chunk palettes, and public mod APIs.

## Rejected or deferred alternatives

- Bug-for-bug Java 1.0 IDs/metadata and recipes: rejected; VibeCraft is inspired by the gameplay scope, not save/protocol compatible.
- Persisting runtime registration indices: rejected because pack order and missing content would reinterpret saves.
- Mapping unknown blocks to air or a generic replaceable block: rejected because it silently destroys builds and can release fluids/entities.
- One global registry for every content type: rejected because type confusion becomes a save, API, and security problem.
- Live registry mutation while a world runs: deferred indefinitely; it invalidates palette, tag, recipe, generator, network, and plugin snapshots simultaneously.
- Arbitrary dictionaries per block position: rejected; finite states plus sparse block entities cover the required density/lifecycle split.
- Full DataFixerUpper-style generic migration graph: deferred until concrete migrations justify it; `WORLD-09`'s typed step registry is sufficient for v1.

## Source-quality notes

Mojang's snapshot notes, Bedrock creator documentation/samples, DataFixerUpper, and open engine repositories are primary/vendor evidence. Java implementation mappings and historical Minecraft Wiki details are secondary and are used only where Mojang publishes no supported Java internals. All VibeCraft limits, placeholder behavior, ID layering, and ambiguity policy are design recommendations to validate, not claims about those projects.
