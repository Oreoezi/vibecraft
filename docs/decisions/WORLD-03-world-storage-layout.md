# WORLD-03 World storage layout

Status: Proposed

Owner: World-storage research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Leading candidate: Prototype one embedded **SQLite database per world**, in WAL mode,
as the v1 authoritative store for section blobs and gameplay state that must commit
atomically with them. Select it only after the durability/packaging campaign passes.
Regardless of backend, use separate signed 64-bit X/Y/Z key columns, one persistence
writer, versioned checksummed payload envelopes, and an internal `IWorldStore`
boundary.

One-sentence rationale: SQLite already supplies atomic transactions, recovery, indexes, schema migration, and safe snapshot APIs; reimplementing those inside region files is the highest-risk path for a small C#/Godot project.

This decision deliberately does **not** select SQLite from prose or promise it as the
backend for enormous public servers. It is the first E2 candidate because it minimizes
custom correctness machinery; fault, durability, packaging, and workload evidence
must still greenlight it.

## Context and constraints

- Singleplayer and dedicated servers need the same portable world format without requiring a database service.
- Sections are sparse in all three axes and have no file-format height ceiling (WORLD-01).
- Chunk generation/ticking may be threaded, but durable writes need clear ownership and ordering.
- Block edits can be coupled to inventories, block entities, scheduled ticks, or entity movement; splitting these across unrelated files can create duplication/loss after a crash.
- The project requires crash-safe saves and eventually plugins/mods, so schema and payload evolution are first-class concerns.
- Large worlds need random point reads, column/range enumeration, online backup, integrity checks, and repair tooling.
- A storage layout must fail safely on malformed/corrupt compressed blobs instead of allocating attacker-controlled sizes.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| Minecraft-style custom region files | Spatial locality, bounded file damage, easy region copying/deletion | Must build allocation, journaling/COW, checksums, free-space recovery, compaction, locking, migration, and multi-record transactions | Reject for v1 |
| One SQLite database per world | Mature ACID/recovery, simple deployment, composite/range keys, online snapshots, transactional terrain+entity+player state | One writer; large-file backup/blast radius; WAL requires local filesystem; native library version must be controlled | **Leading experiment candidate** |
| Many regional SQLite databases | Smaller files and region-level restore/delete | No atomic transaction across WAL databases; connection/file explosion; cross-boundary operations need another journal | Reject until a measured single-file limit |
| RocksDB/LSM key-value store | High write throughput, WAL, atomic write batches, checksums, mature large-data engine | Native C++ packaging/tuning, compaction/write stalls, no relational constraints, harder inspection and migration | Keep as measured fallback |
| PostgreSQL | Excellent large-server concurrency, operations, remote backup/replication | External service and administration; poor fit for portable singleplayer worlds | Optional future dedicated-server backend |
| Bespoke append-only log plus snapshots | Sequential durable writes and replay/audit potential | Compaction, indexes, torn records, migrations, and recovery become engine code | Reject unless a later event-sourcing requirement justifies it |

## Evidence

### Minecraft region/Anvil behavior

- The Anvil container uses region files containing 32×32 X/Z chunks, an 8 KiB location/timestamp header, and 4 KiB sectors ([community-maintained format documentation; secondary source](https://minecraft.fandom.com/wiki/Region_file_format)). It is compact and spatially inspectable, but its header/allocation scheme is a custom miniature filesystem.
- Minecraft 1.18 still stores a finite-height chunk as a section list with per-section paletted block states ([official Java 1.18 notes](https://feedback.minecraft.net/hc/en-us/articles/4415128577293-Minecraft-Java-Edition-1-18)). VibeCraft should keep the section payload idea without copying the column-level persistence boundary.
- Java snapshot 20w45a moved entities out of terrain chunks into a separate `entities` directory of region files ([official Mojang notes](https://www.minecraft.net/es-es/article/minecraft-snapshot-20w45a)). **Inference:** independent terrain/entity files improve specialization, but they cannot provide one native transaction for “remove entity here, add it there, update inventory/block” across files.
- Snapshot 20w14a changed region files to synchronous mode “to increase reliability” ([official Mojang notes](https://feedback.minecraft.net/hc/en-us/articles/360041405952-Minecraft-Java-Edition-Snapshot-20W14A)); a later 1.16 pre-release limited synchronous opening by platform and exposed `sync-chunk-writes` ([official Mojang notes](https://www.minecraft.net/ru-ru/article/minecraft-1-16-pre-release-3)). This history is a warning that custom file durability becomes platform- and policy-sensitive.

### Luanti

- Luanti stores 16³ MapBlocks behind swappable database backends. SQLite is the default and standard distribution format; its documentation rates SQLite “good” for speed and reliability, warns that LevelDB has corruption reports, and recommends PostgreSQL for larger servers ([official backend guide](https://docs.luanti.org/for-server-hosts/database-backends/)). This is direct precedent for “portable embedded default, service backend for scale.”
- Luanti can migrate backends from the command line ([same official guide](https://docs.luanti.org/for-server-hosts/database-backends/)). VibeCraft should preserve that architectural escape hatch with `IWorldStore`, but should not implement multiple backends before one is needed.

### Godot Voxel Tools

- Voxel Tools calls its single-file `VoxelStreamSQLite` the most featured stream; its older Minecraft-like region stream only supports voxel data ([official stream documentation](https://voxel-tools.readthedocs.io/en/latest/streams/)). That supports choosing a database when terrain must coexist with instances and richer state.
- Its SQLite schema stores one compressed block BLOB per coordinate key ([official schema](https://voxel-tools.readthedocs.io/en/latest/specs/sqlite_format_v1/)). The schema’s multiple packed-coordinate revisions (16-, 19-, and 25-bit axes, plus text) are evidence against range-limited packed persistent keys.
- Its 3D region format demonstrates the sector-file alternative but documents versioning/endianness issues and describes the format as an older stream ([official region v3 specification](https://voxel-tools.readthedocs.io/en/latest/specs/region_format_v3/)). The lesson is not that region files are bad; it is that they require long-term format engineering.

### Vintage Story

- Vintage Story uses one SQLite `.vcdbs` save containing chunk, column/map, region, player, and global game-data tables, with Protobuf BLOB values ([version-verified project wiki; secondary source](https://wiki.vintagestory.at/index.php?title=Modding%3AChunk_Data_Storage)). It is strong evidence that a C# voxel survival game can use this broad shape.
- The same schema packs coordinate fields and devotes only 9 bits to chunk Y. VibeCraft should copy the row-per-cubic-section idea, not the packed key.

### Storage engines

- SQLite WAL allows readers and a writer to proceed concurrently, but still has only one writer, requires all processes to be on one host, and does not make transactions across multiple attached WAL databases atomic as a set ([official WAL documentation](https://sqlite.org/wal.html)). These facts drive the single-database/dedicated-writer recommendation.
- SQLite’s file format supports databases far beyond typical game saves; the official documentation notes terabyte-scale databases exist, while filesystem/device limits usually arrive before SQLite’s theoretical maximum ([official file-format documentation](https://www.sqlite.org/fileformat.html)). This does not prove VibeCraft’s workload will scale; it makes a benchmark more rational than premature sharding.
- RocksDB provides WAL recovery, atomic `WriteBatch`, block/full-file checksums, and optional per-key checksums ([official RocksDB overview](https://github.com/facebook/rocksdb/wiki/RocksDB-Overview)). It is a credible fallback, not a straw man.
- RocksDB writes are not power-loss durable by default unless synchronous writes are requested ([official basic operations](https://github.com/facebook/rocksdb/wiki/Basic-Operations)). Switching engines would not remove the need to define durability policy.

### Current SQLite version requirement

SQLite disclosed a rare WAL-reset race present through 3.51.2 and fixed it in 3.51.3, with backports in 3.50.7 and 3.44.6 ([official WAL-reset notice](https://www.sqlite.org/wal.html#the_wal_reset_bug)). VibeCraft must bundle or require a fixed SQLite build and assert the runtime version. A C# package version alone is not proof of the native SQLite version actually loaded.

## Proposed design

### World directory

```text
<world>/
  world.sqlite            # authoritative state; keep any -wal/-shm beside it
  backups/                # completed, verified snapshots; not authoritative
  quarantine/             # exported corrupt records and diagnostics
```

Do not store authoritative section payloads, entity snapshots, pending ticks, or player inventories in sidecar JSON files. Logs, screenshots, metrics, and caches may be sidecars because losing them cannot alter gameplay state.

### Database setup

On world creation/open, verify—not merely issue—the following:

```sql
PRAGMA journal_mode = WAL;
PRAGMA synchronous = FULL;
PRAGMA foreign_keys = ON;
```

- Use a fixed `application_id`, explicit schema version, and an exact allowlist of
  patched native SQLite builds. Record the loaded native library identity, compile
  options, and provenance; a loose minimum-version check is insufficient.
- Put the database on a local filesystem. WAL is not supported over network filesystems ([SQLite WAL limitations](https://sqlite.org/wal.html)).
- Use one write connection owned by the persistence service. Read workers use bounded, short-lived read transactions on separate connections.
- Configure a finite busy timeout, log every `BUSY`/I/O/full error with world and operation context, and do not spin on the tick thread.
- Checkpoint policy and backup behavior are specified in WORLD-04; they are part of operations, not defaults to ignore.

### Logical schema

This is a stable logical contract, not a final SQL migration or byte layout. Composite-key tables should use `WITHOUT ROWID` where measurements confirm the expected benefit.

```sql
world_info(
  singleton INTEGER PRIMARY KEY CHECK(singleton = 1),
  schema_version INTEGER NOT NULL,
  world_uuid BLOB NOT NULL UNIQUE,
  created_utc INTEGER NOT NULL,
  save_sequence INTEGER NOT NULL,
  clean_shutdown INTEGER NOT NULL,
  active_session_uuid BLOB,
  storage_format_version INTEGER NOT NULL,
  metadata_crc32c INTEGER NOT NULL
)

dimensions(
  dimension_id INTEGER PRIMARY KEY,
  name TEXT NOT NULL UNIQUE,
  config_version INTEGER NOT NULL,
  config_length INTEGER NOT NULL,
  config_crc32c INTEGER NOT NULL,
  config BLOB NOT NULL
)

block_state_registry(
  state_id INTEGER PRIMARY KEY,
  canonical_name TEXT NOT NULL,
  canonical_properties BLOB NOT NULL,
  definition_version INTEGER NOT NULL,
  definition_crc32c INTEGER NOT NULL,
  UNIQUE(canonical_name, canonical_properties)
)

sections(
  dimension_id INTEGER NOT NULL,
  sx INTEGER NOT NULL,
  sz INTEGER NOT NULL,
  sy INTEGER NOT NULL,
  revision INTEGER NOT NULL,
  save_sequence INTEGER NOT NULL,
  payload_version INTEGER NOT NULL,
  generator_version INTEGER NOT NULL,
  provenance INTEGER NOT NULL,
  codec INTEGER NOT NULL,
  raw_length INTEGER NOT NULL,
  stored_crc32c INTEGER NOT NULL,
  raw_crc32c INTEGER NOT NULL,
  payload BLOB NOT NULL,
  PRIMARY KEY(dimension_id, sx, sz, sy),
  FOREIGN KEY(dimension_id) REFERENCES dimensions(dimension_id)
)

column_meta(
  dimension_id INTEGER NOT NULL,
  sx INTEGER NOT NULL,
  sz INTEGER NOT NULL,
  revision INTEGER NOT NULL,
  save_sequence INTEGER NOT NULL,
  payload_version INTEGER NOT NULL,
  codec INTEGER NOT NULL,
  raw_length INTEGER NOT NULL,
  stored_crc32c INTEGER NOT NULL,
  raw_crc32c INTEGER NOT NULL,
  payload BLOB NOT NULL,
  PRIMARY KEY(dimension_id, sx, sz)
)

entities(
  dimension_id INTEGER NOT NULL,
  entity_uuid BLOB NOT NULL PRIMARY KEY,
  owner_sx INTEGER NOT NULL,
  owner_sy INTEGER NOT NULL,
  owner_sz INTEGER NOT NULL,
  revision INTEGER NOT NULL,
  save_sequence INTEGER NOT NULL,
  payload_version INTEGER NOT NULL,
  codec INTEGER NOT NULL,
  raw_length INTEGER NOT NULL,
  stored_crc32c INTEGER NOT NULL,
  raw_crc32c INTEGER NOT NULL,
  payload BLOB NOT NULL
)

players(
  player_uuid BLOB NOT NULL PRIMARY KEY,
  revision INTEGER NOT NULL,
  save_sequence INTEGER NOT NULL,
  payload_version INTEGER NOT NULL,
  codec INTEGER NOT NULL,
  raw_length INTEGER NOT NULL,
  stored_crc32c INTEGER NOT NULL,
  raw_crc32c INTEGER NOT NULL,
  payload BLOB NOT NULL
)
```

The primary section key order is `(dimension, X, Z, Y)` so all materialized Y records for a column are contiguous. Exact section loads remain direct primary-key lookups. Add secondary indexes only after query-plan/trace evidence; speculative indexes amplify every save.

`column_meta` stores sparse generation spans/provenance, structures and column-level caches. It must not contain an array sized to a global world height. Section-owned block entities and scheduled ticks belong in the section payload; independently moving entities have their own rows so ownership transfers can be transactional.

Authentication secrets and server-global ban/account data are outside the world database. Player **in-world** state stays in it so inventory/container/block transactions can be atomic.

All revisions/save sequences are constrained to nonnegative signed 64-bit values. Critical structured rows (`world_info`, dimensions, registries) have canonical application checksums too; SQLite integrity checks alone do not prove their values still have the intended game meaning.

### Keys and record envelope

```text
SectionKey = (dimension_id:uint32, sx:int64, sy:int64, sz:int64)
ColumnKey  = (dimension_id:uint32, sx:int64, sz:int64)

RecordHeader =
  payload_version:uint16
  revision:int64 (nonnegative)
  save_sequence:int64 (nonnegative)
  generator_version:uint32
  provenance: enum { generated_unmodified, modified, imported }
  codec: enum { none, zstd }
  raw_length:uint32 with an enforced upper bound
  stored_crc32c
  raw_crc32c
```

- Coordinates remain independent SQL signed 64-bit integers. Network and payload schemas use signed 64-bit fields; no persistent packed key is public.
- The checksum input includes a canonical encoding of the record key and semantic header (excluding the checksum fields themselves), not only the payload. This detects a valid blob associated with the wrong coordinate/revision.
- `stored_crc32c` covers key + header-without-checksums + compressed bytes and is checked before decode. `raw_crc32c` covers key + header-without-checksums + uncompressed bytes and is checked after decode. CRC32C detects accidental damage; it is not an authenticity mechanism.
- Enforce a provisional 16 MiB uncompressed limit per section record before allocation. Oversized block/plugin data must be rejected or moved to a separately bounded record type; the limit is finalized with mod/API research.
- Use Zstandard for normal section payloads and `none` for tiny values when compression expands them. Codec is per row to allow migration.
- The section body may use a dedicated binary Protobuf schema, but it must not reuse network DTOs. Binary Protobuf supports unknown-field preservation and safe additive evolution when field numbers are not reused ([official evolution guide](https://protobuf.dev/programming-guides/proto3/#updating)), while serialized byte order is explicitly not canonical ([official warning](https://protobuf.dev/programming-guides/serialization-not-canonical/)); therefore CRC input uses VibeCraft’s envelope encoding, not a claim that reserialized Protobuf bytes are stable.
- Removed Protobuf field numbers are reserved. Every payload has an explicit application `payload_version`; Protobuf compatibility does not replace semantic migrations.

### Generation provenance and absent data

An absent `sections` row cannot by itself distinguish unvisited space from previously generated all-air space. Use this rule:

1. `column_meta` records finite generated section-Y spans and the generator version that produced each span.
2. Materialized non-default sections have rows, including uniform-air sections that own ticks/block entities.
3. Outside a recorded generated span, the current generator may answer the request.
4. Inside a span with no section row, the versioned generator’s defined default for that span applies; migration cannot silently change it.
5. A player/mod edit marks provenance `modified` in the same transaction as the new section payload. Corrupt modified data is never regenerated automatically.

WORLD-06 must define whether old generated-unmodified sections can be explicitly regenerated during an administrator-approved migration.

### Write and read flow

```text
tick/gen workers
    -> immutable SaveIntent(key, expected/new revision, transaction group)
    -> bounded encode/compress workers
    -> ordered persistence queue
    -> one SQLite writer: BEGIN IMMEDIATE / validate revision / mutate group / COMMIT
    -> durable acknowledgement to section/entity/player owners

load scheduler
    -> bounded read connections and explicit finite key/range requests
    -> length + stored CRC validation
    -> bounded decompression + raw CRC validation
    -> decode immutable snapshot
```

- Never compress or wait for SQLite on a simulation/tick thread.
- Coalesce superseded ungrouped snapshots by key before the writer, but never split or partially coalesce an atomic transaction group.
- Use optimistic revision predicates. A stale save must not overwrite a newer committed revision.
- A section can unload only after the acknowledged durable revision is at least the eviction target revision; a later edit remains dirty.
- Bound queue bytes and item counts. Backpressure first throttles generation/save-producing work; it never silently discards dirty state.
- Keep read transactions short so WAL checkpoints cannot be starved by long-lived readers.

### Why not regional SQLite sharding now

Regional files make deletion and partial restoration attractive, but SQLite states that transactions spanning multiple attached WAL databases are only atomic per database, not for the set ([official WAL documentation](https://sqlite.org/wal.html)). VibeCraft would need a second transaction coordinator for cross-region entities, redstone, structures, and inventories. That complexity is unjustified until the single DB fails a realistic benchmark.

If a world approaches operational limits, the first scale path is PostgreSQL through `IWorldStore` for hosted servers, not transparent sharding of an existing save. An offline export/import tool can convert backends and verify every record checksum.

## Public interfaces

```csharp
public interface IWorldStore : IAsyncDisposable
{
    ValueTask<SectionRecord?> ReadSectionAsync(SectionKey key, CancellationToken ct);
    IAsyncEnumerable<SectionKey> EnumerateSectionsAsync(SectionRange range, CancellationToken ct);
    ValueTask<CommitReceipt> CommitAsync(WorldStoreTransaction transaction, CancellationToken ct);
    ValueTask<StoreHealth> CheckHealthAsync(HealthCheckLevel level, CancellationToken ct);
    ValueTask<BackupReceipt> CreateBackupAsync(BackupRequest request, CancellationToken ct);
}

public sealed record WorldStoreTransaction(
    Guid OperationId,
    IReadOnlyList<ConditionalRecordMutation> Mutations);

public sealed record CommitReceipt(long SaveSequence, IReadOnlyList<RecordRevision> Revisions);
```

The interface expresses semantic records and conditional revisions, not SQL, filenames, or raw Protobuf messages. Only the server storage implementation can open the write connection.

### Resolved cross-brief contract

- Canonical materialization, lifecycle, network, and storage addressing uses WORLD-01's dimension plus signed-64-bit three-dimensional `SectionKey`. Columns are derived scheduling/query views, not alternate persistent identities.
- Persisted revisions and save sequences are checked nonnegative `long` values so they round-trip through SQLite `INTEGER`; overflow is a fatal “world requires format migration” condition rather than wraparound.
- The authoritative manifest lives in transactional `world_info`/`dimensions` records. Any root discovery file is a derived launcher/export hint and cannot be a migration commit point.
- V1 groups section terrain, section-owned scheduled ticks, and block entities in one atomic payload/revision. Free entities, players, and global state remain separate records and join the same SQLite transaction when a gameplay operation couples them.

## Greenlight criteria

- The prototype passes the representative throughput/latency gates below on the project’s minimum server hardware and on a typical singleplayer machine.
- Every canonical coordinate round-trips without packing and query plans use the intended primary keys for exact and column-range loads.
- Under normal load, WAL remains bounded by the configured checkpoint policy; no long reader causes unbounded growth without detection.
- Terrain, block entities/ticks, entity ownership moves, container/inventory updates, and in-world player state can share one SQLite transaction when semantically coupled.
- The runtime rejects vulnerable SQLite builds and reports the loaded native version.
- Unknown payload versions/codecs, oversize lengths, checksum failures, stale revisions, disk-full, and `SQLITE_BUSY` all fail closed with actionable diagnostics.
- A world can be exported/imported through `IWorldStore` with keys, revisions, registry IDs, payload bytes, and checksums preserved or deliberately migrated.
- No code assumes that copying `world.sqlite` alone while the server runs is a valid backup.

## Prototype or benchmark

Required: yes

Smallest useful experiment:

1. Implement the proposed SQLite schema, fixed runtime-version check, envelope, fake 16³ payload generator, one writer queue, and bounded read pool.
2. Populate 1,000,000 sparse section rows across positive/negative 64-bit coordinates using realistic uniform/paletted/direct payload distributions; include column metadata, entities, and players.
3. Replay a documented “16 active players” workload: nearby section reads, exploration generation, block edits, entity section transfers, inventory/container transactions, periodic checkpoints, and a concurrent online backup.
4. Compare one-row commits with bounded batches (time and bytes), Zstd levels suitable for real-time saves, page sizes chosen before WAL activation, and short versus accidentally long readers.
5. Record database size, WAL high-water mark, write amplification, compression CPU, queue depth, p50/p95/p99 commit latency, batch read latency, and tick-thread enqueue time.

Initial pass/fail budgets (to be revised only with a documented target-hardware decision):

- Sustain 200 section-equivalent mutations/second for 30 minutes while serving loads, with no queue growth trend.
- p99 durable commit under 50 ms for normal bounded groups and p99 warm read/decode under 20 ms for a 64-section batch on target SSD hardware.
- Simulation-thread enqueue under 250 µs p99; no SQLite/compression call appears on a tick-thread trace.
- WAL high-water below 256 MiB and returns below 32 MiB during a reader gap; checkpoint starvation raises a metric/alert before the high-water limit.
- Repeatedly rewriting 10% hot sections leaves total database size no more than 1.5× live logical content after maintenance/VACUUM in an offline copy.
- Online backup and checksum verification complete without blocking simulation for more than one tick budget.

If SQLite fails due to the single writer after batching/queue tuning, repeat the exact trace against RocksDB and PostgreSQL. Do not begin a custom region format until one of those measured alternatives is also unsuitable.

## Risks and open questions

- The stated project has no target player count, save size, minimum hardware, or acceptable crash rollback window. Benchmark budgets above are explicit provisional assumptions, not sourced product requirements.
- One large file increases backup time and corruption blast radius. Checksums and verified snapshots reduce but do not erase that risk.
- SQLite does not provide application-payload semantic validation. Envelopes, schema checks, and load-time validators remain required.
- BLOB-heavy updates may cause fragmentation; monitor page/freelist counts and perform compaction on a verified offline copy, never as an unbounded surprise on the live server.
- A plugin cannot receive direct SQL access. It must use bounded server APIs so it cannot bypass revisions, checksums, or transaction ownership.
- PostgreSQL parity is intentionally deferred; pretending to support it before tests would freeze the lowest-common-denominator interface too early.
- Full-text SQL visibility into every block is not a goal. Blocks remain compact BLOBs; administrative tools operate through schema-aware decoders.

## Dependencies

- Requires: WORLD-01 section/key model; GAME-01 persistent block-state identity; WORLD-06 generator provenance; ARCH-05 plugin data boundary.
- Blocks: WORLD-04 durability/recovery/backups; WORLD-05 dirty tracking and eviction; WORLD-09 migrations; entity and inventory persistence designs.

## Rejected or deferred alternatives

- Direct Anvil compatibility: rejected. Conversion belongs in import/export tooling, not the native persistence contract.
- One file per section/chunk: rejected due to filesystem metadata cost, poor transaction semantics, and backup races.
- Region-sharded SQLite: rejected until a measured single-file limitation outweighs distributed-transaction complexity.
- LevelDB: rejected; Luanti’s current documentation explicitly warns about reliability.
- RocksDB as default: deferred behind the comparative benchmark; technically capable but operationally heavier for portable C# singleplayer.
- PostgreSQL as default: rejected for singleplayer; retained as the likely large hosted-server backend.
- Raw Protobuf files as the whole store: rejected because schema evolution is not an index, transaction log, allocator, backup mechanism, or corruption-recovery design.
