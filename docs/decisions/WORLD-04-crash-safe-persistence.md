# WORLD-04 Crash-safe persistence, recovery, and backups

Status: Proposed

Owner: World-storage research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended contract: one persistence writer, revisioned immutable save intents,
atomic transaction groups for coupled state, explicit durable receipts, checksummed
record envelopes, fail-closed recovery, and verified manifest-last backups. SQLite WAL
with `synchronous=FULL` is the first backend candidate, not a durability conclusion;
it must pass the fault and native-packaging campaign before selection.

One-sentence rationale: SQLite can guarantee that committed database transactions recover as old-or-new, while VibeCraft must add the game-specific guarantees SQLite cannot know—what belongs in one transaction, whether a blob is semantically valid, when memory may be evicted, and whether a backup is complete.

### What “crash-safe” means here

The guarantee is:

1. A process/OS crash or power loss must not leave a structurally half-committed database when the filesystem and storage honor SQLite’s sync contract.
2. Each declared atomic gameplay group is wholly old or wholly new after recovery.
3. Every save revision acknowledged as durable is present after recovery under that same hardware contract.
4. Unacknowledged recent simulation may be lost; normal autosaves are not a globally atomic snapshot of every section at one tick.
5. Corruption, unsupported formats, and incomplete backups are detected and rejected rather than silently converted to air/new terrain.

This is stronger and more testable than the spec’s “(semi) atomic,” but it is not magic. SQLite explicitly relies on the operating system, filesystem, and storage device behaving as advertised and does not add redundancy for random bit errors ([official atomic-commit assumptions](https://sqlite.org/atomiccommit.html)). No software-only design can promise survival when hardware lies about flushes or destroys both primary data and backups.

## Context and constraints

- The authoritative server continues simulating while save encoding and I/O run asynchronously.
- A block edit may race with a snapshot; an old save completion must not clear a newer dirty revision.
- Cross-section entity movement, container/inventory transfers, and block-entity changes can duplicate or delete value if persisted independently.
- Abrupt exit is normal in development and possible in production. Cleanup hooks are useful but cannot be part of correctness.
- Power loss, process kill, disk full, permission changes, short writes, corrupt payloads, unsupported versions, and restore interruption all need explicit behavior.
- Singleplayer must receive visible save errors; a dedicated server must expose health/metrics and stop lying to operators.
- Backups must work while a server is running and must be test-restorable.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| SQLite WAL + FULL sync + app checksums/backups | Mature atomic recovery, transaction grouping, concurrent readers, snapshot API; smallest custom correctness surface | Requires version control, checkpoint policy, one writer, semantic validation, honest hardware assumptions | **Leading experiment candidate** |
| Custom region double-write journal or copy-on-write | Spatial repair and bounded files; complete format control | Must prove allocation/header ordering, replay, free-space recovery, fsync/directory semantics, compaction, and cross-file transactions | Reject for v1 |
| RocksDB WAL + synchronous WriteBatch | Atomic batches, WAL recovery, checksums, strong throughput | Native deployment and LSM tuning; sync is not default; backup/compaction operational complexity remains | Measured fallback |
| Save temp world then atomic directory swap | Conceptually simple complete snapshots | Rewrites the world, pause/space cost, directory atomicity/flush portability, no efficient continuous saves | Suitable only for small export snapshots |
| Periodic raw copies / save-on-exit | Trivial | Live copies can be inconsistent; abrupt exit loses data; no transaction grouping or validation | Reject |

## Evidence

### SQLite guarantees and limits

- In WAL mode, a commit record is appended to the WAL; readers see a stable end mark, and readers can proceed concurrently with the one writer ([official WAL design](https://sqlite.org/wal.html)).
- With `synchronous=FULL`, SQLite syncs the WAL on each transaction commit; `NORMAL` omits that per-commit sync and may roll back recent transactions after power loss, even though it protects database consistency ([official WAL performance/durability discussion](https://sqlite.org/wal.html#performance_considerations)). VibeCraft’s “durable receipt” therefore requires `FULL`.
- WAL is part of the persistent database state. Separating or deleting a hot `-wal` file can lose committed transactions or corrupt the database ([official WAL file documentation](https://www.sqlite.org/wal.html#the_wal_file)).
- `quick_check` is O(N) and skips some index/uniqueness checks that full `integrity_check` performs; full integrity checking is O(N log N) ([official PRAGMA documentation](https://www.sqlite.org/pragma.html#pragma_quick_check)). These are structural checks, not validators for VibeCraft payload semantics.
- SQLite’s online Backup API incrementally creates a consistent destination snapshot while allowing brief source reads between steps ([official Backup API](https://www.sqlite.org/backup.html)). An interrupted destination can itself be corrupt, so completion still needs an application publication protocol.
- Copying a live SQLite file can mix old/new pages. SQLite documents Backup API, `VACUUM INTO`, and `sqlite3_rsync` as safe live-copy mechanisms and warns that hot journals must remain paired with the database ([official corruption guide](https://www.sqlite.org/howtocorrupt.html)).
- A rare WAL-reset race affected SQLite through 3.51.2 and is fixed in 3.51.3, with selected backports ([official 2026 notice](https://www.sqlite.org/wal.html#the_wal_reset_bug)). Pinning a fixed native runtime is part of this decision, not optional maintenance trivia.

### Minecraft

- Mojang changed Java region files to synchronous mode in snapshot 20w14a “to increase reliability” ([official snapshot notes](https://feedback.minecraft.net/hc/en-us/articles/360041405952-Minecraft-Java-Edition-Snapshot-20W14A)) and subsequently made platform-specific adjustments/exposed `sync-chunk-writes` during 1.16 pre-release ([official pre-release notes](https://www.minecraft.net/ru-ru/article/minecraft-1-16-pre-release-3)). This is evidence that merely having region files is not a durability design; flush policy matters.
- Java 1.17 separated entities from terrain chunks ([official release notes](https://feedback.minecraft.net/hc/en-us/articles/4402626897165-Minecraft-Caves-Cliffs-Part-1-1-17-Java)). **Inference:** once coupled state lives in different region files, the application—not the format—must prevent cross-file partial outcomes.

### Luanti

- Luanti’s official guide warns that copying live SQLite files while writes occur likely produces corruption and recommends transactional `VACUUM INTO` for live backups ([official backup guide](https://docs.luanti.org/for-server-hosts/backup-solutions/)). This is directly applicable to server operators and backup UIs.
- Luanti rates SQLite reliability as good and warns that its LevelDB backend can corrupt around power loss/unexpected shutdown ([official backend guide](https://docs.luanti.org/for-server-hosts/database-backends/)). “Fast key-value store” is therefore not automatically “safer saves.”

### Godot Voxel Tools

- Voxel Tools saves asynchronously and warns that Godot’s Stop button sends a kill with no cleanup opportunity: pending saves are lost and a file being written can be corrupted ([official stream documentation](https://voxel-tools.readthedocs.io/en/latest/streams/#closing-the-game)). Correct VibeCraft shutdown should improve UX, but fault tests must assume it never runs.
- The same documentation warns that reusing/switching a stream while old async tasks remain can send writes to the next save. This supports binding every save intent to an immutable world/session identity, not a mutable “current path.”

### Vintage Story

- Vintage Story stores chunk/world data in SQLite, yet its repair guide still describes corrupt chunks and `SQLiteException` recovery ([project wiki; secondary source](https://wiki.vintagestory.at/Repairing_a_corrupt_savegame_or_worldmap)). This is useful negative evidence: a robust database does not validate application serialization, prevent every hardware problem, or replace backups.

### RocksDB

- RocksDB’s `WriteBatch` applies edits atomically, but default writes return before data reaches persistent storage; `sync=true` is required for power-loss durability ([official basic operations](https://github.com/facebook/rocksdb/wiki/Basic-Operations)). The same explicit acknowledgment distinction would be necessary if VibeCraft later changes backend.

## Proposed design

### Persistence vocabulary

- **Accepted mutation:** authoritative in memory and visible to gameplay/network clients; not necessarily durable.
- **Dirty revision:** a record version newer than its last durable receipt.
- **Save intent:** immutable record snapshot plus key, revision, world/session ID, and optional atomic-group ID.
- **Committed:** SQLite returned success for the `FULL`-synchronous transaction.
- **Durable receipt:** storage service notified record owners of that commit and save sequence.
- **Published backup:** destination passed structural/semantic verification and its final manifest was durably published.

UI, logs, metrics, and APIs must use these words accurately. “Saved” means durable receipt, not “queued” or “serialization started.”

### Crash-consistency invariants

1. **Monotonic record revisions.** A mutation at revision N+1 cannot be cleared by completion of save N.
2. **Conditional writes.** A queued stale revision cannot overwrite a newer durable row.
3. **Atomic coupled state.** Inventory/container transfers, entity ownership moves, and similar conservation operations commit all affected rows in one SQLite transaction.
4. **No premature eviction.** A dirty section/entity can unload only after its target revision has a durable receipt or after an explicit user-approved discard on shutdown.
5. **Bounded asynchronous work.** Save queues apply backpressure; they never drop dirty snapshots or accept unbounded memory growth.
6. **Checks before use.** Key/header, stored bytes, uncompressed bytes, payload version, registry references, and semantic bounds validate before a record enters simulation.
7. **No silent regeneration.** A corrupt/imported/modified section remains quarantined/unavailable until restored or explicitly repaired. Only proven generated-unmodified data may be regenerated under a version-pinned admin operation.
8. **WAL is state.** The primary database and its hot WAL are never moved/copied independently by VibeCraft.
9. **Manifest-last backups.** An incomplete snapshot has no valid final manifest and is never offered for restore.
10. **World/session binding.** Every async task captures an immutable world UUID and session UUID; switching worlds cannot retarget queued work.

### Atomicity scope

Making every dirty record in the entire world one transaction would cause huge WAL spikes and commit latency. The design instead defines explicit scopes:

- A single section snapshot is atomic.
- A gameplay operation that conserves identity/value across records is an atomic `WorldStoreTransaction` (for example: chest slot -> player slot; entity old owner -> new owner; piston source/destination block entities).
- Ordinary independent section autosaves may commit in separate bounded transactions. A crash may therefore restore neighboring sections from slightly different simulation times; this is bounded rollback, not structural corruption.
- A user `/save` or singleplayer save action establishes a barrier revision, drains all records dirty at that barrier, then reports completion. It does not claim one giant all-world transaction.
- A backup is a SQLite-consistent database snapshot. If product requirements later demand an exact whole-simulation tick, pause at a tick barrier or implement in-memory MVCC; do not pretend ordinary asynchronous autosave provides it.

The systems that create cross-record operations must declare their atomic groups. Persistence cannot infer semantic coupling from spatial proximity.

### Save state machine

```text
Clean(R)
  -- mutation --> Dirty(R+1)
  -- snapshot --> Encoding(R+1)
  -- encoded --> Queued(R+1)
  -- writer begins --> Committing(R+1)
  -- COMMIT succeeds --> Durable(R+1)
  -- receipt matches current revision --> Clean(R+1)

Any mutation while Encoding/Queued/Committing increments the live revision and leaves it Dirty.
Any encode/commit error returns the target to Dirty and updates SaveHealth.
```

- Snapshot and compression happen off simulation threads.
- The writer uses one connection and `BEGIN IMMEDIATE` for a bounded group, updates `world_info.save_sequence` in the same transaction, commits, then sends receipts.
- `PRAGMA synchronous=FULL`, verified `journal_mode=WAL`, and a fixed SQLite runtime are non-negotiable for the normal safe mode.
- A future explicitly labeled “fast/unsafe” mode may use `NORMAL`, but it must never produce the same durable receipt semantics and is out of v1 scope.
- Checkpointing runs through the persistence owner during lulls/thresholds, with metrics for WAL bytes, oldest read transaction, blocked frames, and duration. Read transactions have deadlines.

### Save health and backpressure

```text
Healthy -> Lagging -> Degraded -> MutationPaused
```

- `Lagging`: queue age/bytes above warning threshold; throttle generation and nonessential persistence producers.
- `Degraded`: repeated commit/encoding/checksum error; visible UI/admin alert, metrics, and retry with bounded exponential backoff.
- `MutationPaused`: disk full, read-only filesystem, persistent I/O error, or queue at hard byte cap. Pause world-changing simulation and reject new joins; retain dirty state in memory. Reads/chat/admin recovery may continue.
- Never keep accepting irreversible gameplay while only logging that saves fail. That converts a storage outage into unbounded rollback and eventual memory loss.
- Dedicated servers exit nonzero if ordered shutdown cannot drain. Singleplayer shows a blocking “world not fully saved” choice; it must not quietly return to the title screen.
- Recovery from `MutationPaused` requires a successful probe transaction plus draining the queue; health then returns through `Degraded`, not instantly to `Healthy`.

Threshold values belong to the WORLD-03 workload benchmark and server memory budget. They are configuration with hard safe defaults, not plugin-controlled knobs.

### Record validation and corruption behavior

On every record load:

1. Validate key ranges, known codec/version, compressed size, and `raw_length` before allocation.
2. Verify CRC32C over canonical key/header + stored payload.
3. Decompress with an exact output cap; reject trailing/short/oversize results.
4. Verify CRC32C over canonical key/header + raw payload.
5. Decode the dedicated storage schema with recursion/collection/string limits.
6. Validate local positions, unique IDs, palette indices, registry references, tick bounds, and block-entity compatibility.

SQLite itself states that random bit-error detection is delegated to hardware/OS ([official assumptions](https://sqlite.org/atomiccommit.html#hardware_assumptions)). Per-record CRCs close part of that gap and localize a damaged record. They do not correct it.

On failure:

- Do not mutate or delete the primary row as part of the failed load.
- Export key, header, raw stored bytes (if readable), error, SQLite version, and world/save sequence to `quarantine/` through a new uniquely named diagnostic file.
- Mark the section unavailable with a server-side barrier/error state; never substitute air, because that could destroy builds and release fluids/entities.
- Continue serving unaffected areas when safe, but refuse joins/spawns whose required area is corrupt and make the degraded state prominent.
- Offer restore from a verified backup. Automatic regeneration is allowed only for provenance `generated_unmodified`, exact generator version availability, and an explicit admin repair command that first preserves the bad row.
- Do not run SQLite `.recover` automatically on the only copy. Recovery tools may salvage rows but can lose constraints/associations; operate on a copied/backup artifact.

### Startup and shutdown

Startup:

1. Refuse symlink/path escapes and unexpected file ownership/permissions according to deployment policy.
2. Open the database through SQLite without moving/deleting `-wal`/`-shm`; let SQLite perform WAL recovery.
3. Verify application ID, world UUID, schema/storage versions, required pragmas, and fixed runtime version.
4. Read `clean_shutdown`. After an unclean session, run `quick_check` and validate critical global rows/registries. Full payload CRC is always checked on record load.
5. If structural checks fail, open no gameplay session; preserve files and offer verified-backup restore/diagnostic export.
6. In one durable transaction, set `clean_shutdown = false` and a new `active_session_uuid` before accepting players.

`quick_check` is O(N), so the exact startup policy for multi-terabyte worlds may need a background/sampled mode after WAL recovery. That optimization may not silently permit structurally failed pages to enter simulation.

Orderly shutdown:

1. Stop joins and new save-producing work; reach a simulation barrier.
2. Snapshot all dirty records and drain encode/write queues.
3. Confirm receipts through the shutdown barrier, run the configured final checkpoint, and set `clean_shutdown = true` in the last transaction.
4. Close every connection and only then report success.

Correctness is tested with steps 1–4 interrupted at every point.

### Backup publication

Use the SQLite Online Backup API to write an incremental snapshot to a unique staging path; do not copy `world.sqlite` while live. The official API creates a consistent snapshot but notes an interrupted destination may be corrupt ([SQLite Backup API](https://www.sqlite.org/backup.html)).

```text
backups/
  .staging-<backup_uuid>.sqlite
  <timestamp>-<save_sequence>/
    world.sqlite
    manifest.json
```

Protocol:

1. Reserve a backup UUID and begin the online Backup API to a new staging file.
2. Finish/close and durably flush the destination. Run `quick_check` (full `integrity_check` on scheduled maintenance backups), validate required global rows, stream-verify every VibeCraft record checksum/schema, and read the snapshot's actual `save_sequence` from the destination.
3. Compute SHA-256 of the completed database and build a manifest containing world UUID, backup UUID, snapshot save sequence, schema/storage versions, UTC timestamp, byte length, hash, verification level, and engine/native SQLite versions.
4. Move the verified database into a new backup directory on the same filesystem and flush the containing directory where the platform supports it. Write/flush `manifest.tmp`, atomically rename it to `manifest.json` **last**, then flush the directory again. A backup without a valid manifest/hash is incomplete.
5. Replicate the published directory off-device. Local snapshots do not protect against disk loss, theft, ransomware, or filesystem-wide damage.
6. Apply retention only to published, verified backups and never delete the newest known-good backup before a newer one is verified.

Default architecture policy is at least three verified generations. Time cadence and byte budget are deployment/product settings because the spec gives no world-size or recovery-point objective. Servers should expose last successful backup age and last test-restore age.

Restore is offline and non-destructive:

1. Copy selected backup to a new restore staging world.
2. Verify manifest SHA-256, structural checks, all record CRCs/semantics, versions, and world UUID.
3. Open read-only and run a headless smoke load of spawn plus sampled sections/entities/players.
4. Only then switch the configured world path/name. Keep the damaged primary until the operator deliberately archives/removes it.

### Schema migration safety

- Every migration requires a verified pre-migration backup.
- Small SQL-only migrations execute transactionally. Payload rewrites use copy/verify/swap or resumable dual-version rows; never mutate millions of sole-copy blobs in place without a restart marker.
- Readers know an explicit finite set of payload versions. Writers emit only the current version.
- A migration records source/target versions, progress, failure, and completion in database state. “Database opened once in a newer binary” is not enough evidence to delete the backup.
- Downgrades are unsupported unless the migration explicitly provides an exporter.

## Public interfaces and events

```csharp
public enum SaveDurability { Queued, Durable }
public enum SaveHealth { Healthy, Lagging, Degraded, MutationPaused }

public sealed record SaveIntent(
    Guid WorldId,
    Guid SessionId,
    RecordKey Key,
    long Revision,
    Guid? AtomicGroupId,
    ImmutableRecordSnapshot Snapshot);

public sealed record DurableReceipt(
    long SaveSequence,
    IReadOnlyList<RecordRevision> Records,
    DateTimeOffset CommittedAt);

public interface IPersistenceCoordinator
{
    SaveHealth Health { get; }
    ValueTask<DurableReceipt> FlushBarrierAsync(FlushBarrier barrier, CancellationToken ct);
    ValueTask<BackupReceipt> CreateVerifiedBackupAsync(BackupRequest request, CancellationToken ct);
    event Action<SaveHealthChanged> HealthChanged;
}
```

Plugins may request bounded storage operations through server APIs but cannot forge `DurableReceipt`, set pragmas, open the DB, bypass record validators, or suppress health transitions.

## Greenlight criteria

- The SQLite native runtime is selected from an exact reviewed allowlist (for example
  3.51.3 or an explicitly documented fixed backport), with loaded-library identity,
  compile options, and provenance asserted at startup and in tests. A loose `>=`
  version range or managed-package version is not sufficient.
- All fault-injection cases below either recover to a valid old/new state or fail closed without modifying the primary further.
- Every durable receipt survives process kill/power-cut simulation on test storage that honors sync; no partially applied atomic gameplay group is observed.
- A mutation racing an older save remains dirty and is eventually durable; eviction never loses a newer revision.
- Disk-full and persistent write errors trigger `MutationPaused` before queue memory exceeds its hard budget, with visible singleplayer and server-operator errors.
- Single-bit corruption of key/header/stored/raw payload is detected before simulation; malformed lengths cannot allocate beyond configured limits.
- A live backup can be made, verified, and restored while gameplay writes continue, and incomplete staging backups are never listed as restorable.
- At least one automated restore test boots the copied world and validates spawn, player inventory, entities, block entities, scheduled ticks, and sampled sections.
- Save/checkpoint/backup latency remains within WORLD-03’s accepted workload budgets.

## Prototype or fault-injection campaign

Required: yes

Smallest useful experiment:

1. Build a headless storage harness around the WORLD-03 prototype with deterministic operations: section revisions, two-section entity moves, chest/player transfers, scheduled ticks, save barriers, checkpoints, and online backups.
2. Record accepted operations and durable receipts in the parent test controller, not in the database under test.
3. In 10,000 seeded runs, terminate the child process at random instruction-level hooks around snapshot, encode, BEGIN, row writes, COMMIT, receipt, checkpoint, clean marker, backup copy, verify, and manifest publication.
4. Inject `SQLITE_FULL`, permission/read-only errors, busy readers, delayed/fsync failures where the test VFS/platform permits, truncated/compressed payloads, wrong keys, unsupported versions/codecs, and random single-bit flips.
5. Reopen with the production recovery path; compare the result against the model’s allowed old/new states and every issued durable receipt.
6. Restore every published backup and verify it independently. Interrupt restore and backup publication repeatedly; the primary and prior good backup must remain untouched.

Success metrics:

- 100% of non-bitrot crash runs open with `quick_check = ok` and satisfy all critical atomic-group invariants.
- 100% of durable receipts are present after simulated abrupt termination on a sync-honoring test path.
- Unacknowledged operations may be absent but never appear as half a declared group or duplicate a unique entity/item.
- 100% of injected single-bit changes in covered record bytes are detected before decode/use; corruption in SQLite pages is either caught by SQLite checks or record validation, otherwise the test fails the design.
- No malformed record causes an allocation beyond its configured bound or crashes the server process.
- No interrupted backup gains a final valid manifest; every manifested backup passes hash, database, record, and smoke-load verification.
- Queue hard-cap tests reach `MutationPaused` without dropping dirty revisions or exceeding the configured memory cap by more than one in-flight bounded batch.

The campaign must run on Linux and Windows because sync/file-lock behavior is platform-sensitive. Before release, add at least one abrupt VM/power-cycle test on each supported filesystem class; process kill alone does not emulate a machine power loss.

## Risks and open questions

- The project has not set a recovery-point objective. FULL sync protects acknowledged transactions, but autosave cadence determines how much accepted in-memory play can still be lost.
- Some filesystems, virtual disks, RAID controllers, and consumer drives may lie about flushes. Document supported storage assumptions and require off-device backups for important servers.
- `quick_check` after every unclean start may be slow on very large worlds. Any reduced startup scan needs metrics and must retain per-record validation.
- CRC32C detects accidental corruption but is not adversarial authentication. If untrusted parties can modify world files, use authenticated signatures/MACs under a separate threat model.
- Global consistency across independently autosaved sections is weaker than an exact tick snapshot. Systems with conservation/uniqueness invariants must declare atomic groups.
- Pausing mutation during storage failure is disruptive, but continuing silently is worse. Product UX for pause/retry/admin intervention needs its own design.
- Backup frequency, retention byte budget, off-site transport, and encryption depend on deployment requirements absent from the current spec.
- The fixed SQLite build must be updated deliberately with its own crash test matrix; “latest” is not a reproducible dependency policy.

## Dependencies

- Requires: WORLD-01 revisioned section ownership; WORLD-03 single-database schema and envelopes; WORLD-05 dirty tracking/eviction; entity/inventory ownership semantics.
- Blocks: ARCH-04 singleplayer shutdown UX; server operations/health monitoring; WORLD-09 migrations; plugin persistence API; release backup/restore tooling.

## Rejected or deferred alternatives

- `File.Replace`/rename per chunk with no journal: rejected; it does not make multi-record gameplay operations atomic and has platform/directory-flush subtleties.
- Raw copying the live database: rejected by SQLite and Luanti guidance.
- Deleting a WAL to “repair” a world: rejected; the WAL may contain committed state required for recovery.
- `synchronous=NORMAL` under the normal safe mode: rejected because recent commits are not power-loss durable; may only return a weaker, explicitly named receipt in a future opt-in mode.
- Automatic regeneration of any bad section: rejected because corruption provenance cannot prove the section contained no player work.
- Automatic SQLite `.recover` on the primary: rejected; salvage must operate on a copy and be verified.
- Full-world transaction every autosave: rejected due to WAL/latency spikes; use explicit atomic groups and save barriers.
- Custom journaling layered beside SQLite: rejected. SQLite is the transaction journal; VibeCraft adds semantic records/checksums, not a second competing commit protocol.
