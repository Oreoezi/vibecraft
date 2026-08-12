# WORLD-09 World-format versioning and migration

Status: Proposed

## Decision

Recommended choice: Version the world manifest and each persisted record family separately; perform small deterministic migrations lazily on read and rewrite through normal crash-safe saving, while requiring an explicit copy/backup migration tool for major or bulk transformations.

One-sentence rationale: Per-record versions avoid rewriting an entire large world for every additive change, while explicit major migrations prevent a normal game launch from silently and irreversibly converting terabytes of data.

## Context and constraints

- Worlds may outlive many engine, content, mod, and generator releases.
- Only a fraction of an effectively unbounded world is generated or loaded.
- A crash during migration must not corrupt the last valid representation.
- Unknown mod-owned data should not disappear merely because a mod is temporarily missing.
- Generator changes and storage-schema changes are distinct.

## Options considered

| Option | Strengths | Costs/risks | Fit |
| --- | --- | --- | --- |
| One global format integer and full rewrite | Simple state after success | Slow, high free-space requirement, dangerous interruption | Use only for rare majors |
| Upgrade every record lazily | Fast open, proportional work | Mixed versions remain; hot-load spikes; downgrade complexity | Recommended for bounded migrations |
| Never migrate; support all readers forever | No destructive rewrite | Permanent branch/test burden | Reject |
| Append-only event history and rebuild | Excellent audit/replay in theory | Large, complex, generator/content determinism burden | Reject as primary save model |
| Global manifest plus per-record migrations | Bounded compatibility and selective work | Requires migration registry and fixtures | Recommended |

## Evidence

Mojang's open-source DataFixerUpper exists specifically for incremental building, merging, and optimization of transformations between Minecraft Java Edition data versions. It models schemas plus rewrite rules and builds converters between them ([DataFixerUpper repository](https://github.com/Mojang/DataFixerUpper)). This demonstrates the value of typed transformation chains, but also represents machinery VibeCraft should not reproduce before it needs generic recursive data rewriting.

Minecraft's change from older chunk contents to vertically sectioned Anvil chunks retained the outer region-file concept while versioning/changing chunk representation ([Anvil format](https://minecraft.wiki/w/Anvil_file_format)). The useful lesson is that container, record, and generator versions evolve on different schedules.

SQLite reserves application-controlled `application_id` and `user_version` fields for applications using a database as a file format ([SQLite PRAGMA reference](https://www.sqlite.org/pragma.html#pragma_application_id)). If VibeCraft chooses SQLite for a persistence domain, these should identify the database and schema, but they do not replace versions on serialized world records.

## Proposed design

### World manifest

The authoritative world manifest is a small checksummed/versioned record inside the transactional world store (`world_info` plus dimension records under the SQLite decision). An optional root-level discovery file may mirror non-sensitive display metadata for launchers and tools, but it is derived, replaceable, and never a commit point:

```text
WorldManifest {
  format_major
  format_minor
  minimum_reader_major
  world_id
  created_with_build
  last_saved_with_build
  dimensions[] {
    id
    generator_id
    generator_version
    seed_reference
  }
  registry_snapshot_id
  required_content_lock
  feature_flags[]
}
```

`format_major` changes only when the existing runtime cannot safely interpret/upgrade data in place. `format_minor` records additive container/manifest evolution. It is not reused as every individual record's schema version.

### Record envelope

Every independently addressable persisted value uses a bounded envelope:

```text
RecordEnvelope {
  record_kind
  schema_version
  stable_key
  logical_revision
  payload_length
  payload_checksum
  payload
}
```

For v1, section terrain, section-owned scheduled ticks, and block entities share one atomic section-state payload and revision. Other record families include free entities, player state, global dimension state, maps, and mod-owned storage. Compression belongs outside or is explicitly identified by the envelope/container.

### Migration registry

Each core record family registers a linear tested chain:

```text
Migrate(kind, from_version, payload) -> (to_version, payload | typed value)
```

Rules:

- Transformations are deterministic, bounded, and side-effect-free.
- Each step handles exactly one source version and either succeeds or returns a typed failure.
- A migrated record is validated against current invariants before publication.
- Read migration marks the current in-memory record dirty; normal persistence writes the new version atomically later.
- The old durable value remains valid until the new transaction/record commits.
- Migration code stays in the supported-reader window and is removed only by an announced major-format policy.

Do not deserialize arbitrary runtime types by assembly-qualified class name. Persist stable namespaced type IDs and explicit schemas.

### Major and bulk migrations

A migration requiring global indexes, coordinate/key changes, registry remapping across all records, or removal of information runs only through a migration command/tool:

1. Verify source format and integrity.
2. Estimate time and temporary disk requirement.
3. Create/require a backup or write to a separate destination world.
4. Write a migration journal/progress index.
5. Convert and verify independently addressable batches.
6. Atomically publish the destination world's authoritative manifest record only after required batches are valid; publish any external discovery/backup manifest last as a derived pointer.
7. Keep the source/backup until the user explicitly removes it.

The normal server refuses a major migration unless explicitly requested; it must never join/open and silently make the only copy unreadable by the prior release.

### Generator evolution

Every generated/populated section records generator identity/version and generation stage. Existing generated terrain is authoritative and is never regenerated merely because the current generator changed. New neighboring sections may use a newer generator; seam policy is a world-generation concern, not a storage migration.

If a bug fix must modify existing terrain, implement it as an explicit terrain migration with scope and backup—not by changing the meaning of an old generator version.

### Registries and missing mods

- Persistent records use namespaced IDs plus a saved registry snapshot/mapping.
- A normal gameplay open compares the required gameplay-content lock before
  activating sections. Missing or incompatible required content aborts with a report
  and zero world writes; it does not enter simulation with placeholders.
- An explicit read-only recovery/export path may represent missing content as an
  unresolved placeholder retaining original ID and payload.
- Unknown mod-owned records are preserved as bounded opaque bytes when their container is understood.
- A mod may provide migration functions only for its namespace, with time/memory quotas and no access to unrelated records.
- Removing a mod requires an explicit cleanup/export/migration decision with backup;
  loading without it does not silently erase data or enter gameplay.

### Reader/writer policy

- Older reader encountering a newer incompatible major: open read-only metadata for a useful error, never write.
- New reader encountering a supported older major: require explicit major migration if needed.
- Unknown additive minor fields: preserve/ignore according to that schema's rules.
- Any failed record migration quarantines the record/region or aborts load according to criticality; never replace it with air/default data silently.

## Greenlight criteria

- Network, pack, mod ABI, generator, and world-record versions are separate concepts in code and docs.
- Golden fixtures exist for every supported old record version.
- Missing required mod data causes zero-write gameplay-open refusal. Recovery/export
  decoding preserves it byte-for-byte where the container permits preservation.
- Unsupported newer worlds are never modified.
- Generator updates do not regenerate already-generated authoritative sections.
- Major migration estimates disk requirements and preserves a recoverable source copy.

## Prototype or benchmark

Required: yes.

Build three versions of a toy section/entity/block-entity schema. Test direct and chained migration, mixed-version records, unknown fields/record kinds, zero-write refusal for missing required mods, read-only recovery/export of missing records, corrupt checksums, cancellation, and process termination before/after commit. Run a bulk migration to a separate destination and verify source immutability plus restartable progress.

Pass condition: every interruption yields either the prior valid record/world or the fully validated new one; no fixture is silently defaulted; opening an unsupported future major performs zero writes.

## Risks and open questions

- Lazy migration can cause load spikes; background pre-migration is useful only after correctness is established.
- Preserving opaque unknown records needs strict byte and count quotas to prevent malicious-world memory/storage attacks.
- Supporting downgrade is substantially harder than upgrade and is not promised; backups provide the rollback mechanism.
- A complete registry snapshot can become large and needs its own compact/versioned representation.

## Dependencies

- Requires: `WORLD-03`, `WORLD-04`, `WORLD-06`, `GAME-01`, `MOD-01`.
- Blocks: public save compatibility promises and release rollback procedures.

## Rejected or deferred alternatives

- Silent destructive conversion on ordinary launch: rejected.
- Reusing runtime class names as persisted type identity: rejected.
- Downgrade converters for every release: deferred; restore a pre-migration backup instead.
- Full DataFixerUpper-equivalent generic transformation framework: deferred until actual schema complexity justifies it.
