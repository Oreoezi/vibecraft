# Adversarial world, rendering, and storage review

Status: Complete under hard deadline  
Review date: 2026-08-09  
Scope: current `WORLD-01..09`, `RENDER-01..07`, `ARCH-02/03`, `GAME-01/02`, and `PROTOTYPE_PROGRAM.md`

## Snapshot note: current tree versus first wave

This review is about the **current integrated files**, not the first-wave drafts. Immediately before this file was written, the repository contained 60 Markdown files, 13,650 lines, and 1,212,247 bytes; the SHA-256 of the sorted per-file SHA-256 listing was `85b1d2b49c7377a1a505f207b8d96e658bb369631c18203dd08f99809dd2c24e`.

The earlier integrity audit recorded 58 Markdown files and 13,290 lines at 21:10:27 +03:00. Several first-wave findings had been corrected when this review was written: canonical coordinates are signed 64-bit and 3D; render-local `Vector3I` is explicitly checked/noncanonical; and `SectionSide` is again an experiment. The later owner review in `questions2.md` superseded this snapshot's 20 Hz clock with fixed 60 TPS, set an approximately 10,000-block initial build range, and promoted minimal fog-obscured far terrain to a post-first-playable v1 gate. Treat numeric/current-state claims below as dated evidence unless repeated in the current baseline.

No new browsing was performed for this deadline pass. SQLite conclusions below rely on the primary SQLite sources already captured in `WORLD-03/04`. Claims not rechecked are marked experiment-gated or unresolved.

## Verdict

The packet now has a coherent architectural center, but it is **not an implementation specification yet**. Greenlight the invariants; build the P0/P1 harnesses before freezing formats or numeric limits. The largest remaining risks are conditional durability claims, an unfrozen section edge, untyped revision sketches at the render boundary, and performance thresholds written without target hardware/workload.

> Integration follow-up: after this snapshot, `WORLD-03/04` were changed from a
> prose-level SQLite selection to an E2 candidate with an exact native-build
> allowlist requirement; `RENDER-02` now uses a typed `MeshInputStamp`; and the entire
> rejected `NET-06` multi-profile block has explicit archival start/end warnings.
> Section edge, performance envelope, and every experiment gate remain unresolved by
> design.

| Disposition | Major choices |
| --- | --- |
| **Greenlight candidate** | Sparse `SectionKey(DimensionId, signed-int64 XYZ)`; checked floor arithmetic; one authoritative world writer; immutable worker inputs/outputs; exact revision/epoch publication; one atomic section-state payload; bounded/coalescing queues; block-scale gameplay light; a small fixed terrain-pass set. |
| **Experiment-gated** | 16³ versus 32³ sections; SQLite as the shipped store; 20 Hz movement feel/capacity; direct `RenderingServer` versus node adapter; greedy meshing; shader light pages; texture arrays and the proposed bank size. |
| **Defer** | Mixed-generator seam adapters; huge/site-scale structures; far-terrain LoD; volumetric fog; GI; OIT; general refraction; native/custom renderer. |
| **Reject** | 64³ light samples per block; worker mutation of live chunks/Godot resources; per-block/per-material terrain surfaces; unbounded queues; `synchronous=NORMAL` producing “durable” receipts; copying a live SQLite file; deleting a hot WAL; fog as proof that LoD/streaming is correct. |

## P0 — resolve before production implementation

### P0.1 Freeze the section edge before any durable world or wire format

`WORLD-01` correctly makes 16³ and 32³ prototype candidates, but most rendering, lighting, scheduling, and structure numbers are still evaluated at 16³. That is acceptable for E1, not for a released schema.

Required resolution:

- Run P0 with both edges and freeze `SectionSide`, local indexing, codec fixtures, halo dimensions, and migration version before creating a user world.
- Keep canonical coordinates independent of the edge.
- Recompute every per-section/page/queue budget from the selected edge and a fixed spatial workload. A fixed count of 4,096 sections is not a fair cross-edge comparison because 32³ covers eight times the volume.
- Treat all hard-coded 16 shifts outside the candidate codec as defects.

### P0.2 SQLite is a strong candidate, not a durability proof

The current SQLite reasoning is substantially sound, with these exact limits:

1. WAL permits concurrent readers and one writer; it does **not** provide multiple concurrent writers, cross-host operation, or a reason to shard early. WAL requires processes to share the same host and is unsuitable for network filesystems ([SQLite WAL](https://sqlite.org/wal.html)).
2. In WAL mode, `synchronous=FULL` syncs the WAL at transaction commit. `NORMAL` can preserve database consistency while losing recently committed transactions after power loss; therefore it cannot produce the same durable receipt ([WAL durability](https://sqlite.org/wal.html#performance_considerations)).
3. A hot `-wal` file is part of the committed database state. Never copy, move, or delete it independently ([WAL file](https://www.sqlite.org/wal.html#the_wal_file)).
4. A live backup must use the Online Backup API or another documented transactional mechanism, then be verified and published manifest-last; raw file copy during writes is invalid ([Backup API](https://www.sqlite.org/backup.html), [corruption guidance](https://www.sqlite.org/howtocorrupt.html)).
5. SQLite reported a WAL-reset race through 3.51.2, fixed in 3.51.3 with selected backports. Pin and assert an exact patched **native** runtime; “NuGet package version” and a loose `>=3.51.3` policy are insufficient provenance ([SQLite notice](https://www.sqlite.org/wal.html#the_wal_reset_bug)).
6. Commit durability remains conditional on the OS, filesystem, controller, and device honoring flush/atomic-write assumptions. CRC32C detects covered corruption but supplies neither redundancy nor recovery ([atomic-commit assumptions](https://sqlite.org/atomiccommit.html)).

The v1 receipt contract should be: a receipt for revision `R` is emitted only after the single writer successfully commits the transaction under verified `WAL` + `FULL` settings; recovery on supported storage must contain `R` or fail closed. No receipt promises a whole-world same-tick snapshot, off-device survival, or recovery from lying hardware.

Greenlight SQLite only after P1 proves kill/reopen, disk-full, busy-reader/checkpoint, backup/restore, and patched-runtime packaging on each supported OS/filesystem class. Until then, `IWorldStore` is greenlit and SQLite is experiment-gated.

### P0.3 Define a reference performance envelope

There is no approved target CPU/GPU/RAM, renderer, player count, view/simulation radius, movement speed, worldgen rate, or storage device. Consequently, these are **test seeds, not product requirements**: 2 ms uploads, 8 commits/frame, 128 MiB completed meshes, 2,048 jobs, 512 MiB generation scratch, 4,096 light pages, 64/256 MiB GPU pools, 2,048 texture layers, 65,536 circuit nodes, and 100,000 updates/tick.

Before a performance greenlight, publish at least low/reference/high client profiles plus one dedicated-server workload. Every threshold must name hardware, scene/workload, warm-up, percentile, duration, and failure response. A falsifiable arbitrary number is still arbitrary.

### P0.4 Replace raw render counters with typed stamps

`WORLD-01` now defines `SectionRevision` as checked nonnegative signed 64-bit and distinct named revision domains. `RENDER-02` still sketches `MeshRevision` as four raw `ulong` values plus a `uint Lifetime`. That invites accidental cross-domain assignment and narrows lifetime relative to the world lifecycle epoch.

Before implementing the interface, use a typed composite stamp such as:

```text
MeshInputStamp =
  SectionRevision content
  LightRevision lighting
  RenderRegistryRevision registry
  LodSourceRevision lod
  MaterializationEpoch lifetime
```

Commit requires exact equality of every field. Epoch/revision widths and serialization must be owned once; no unchecked casts to Godot or network integers are allowed.

## P1 — preserve the good design while cutting accidental complexity

### P1.1 Canonical v1 world-state ownership

Adopt this as the single contract:

- `SectionKey = DimensionId + signed-int64 SectionCoord(X,Y,Z)`; arithmetic is checked and negative block division uses shared floor helpers.
- One section revision owns terrain, section-owned scheduled ticks, and section-owned block entities in one bounded payload.
- Free entities, players, dimension metadata, registries, structure plans/indexes, and structure receipts are separate records only where ownership/lifetime differs.
- Any operation coupling records is one SQLite transaction. A structure receipt is separate metadata but commits with the resulting section payload/revision.
- Persisted revisions are nonnegative signed 64-bit. `WorldTick` is unsigned 64-bit. Client prediction sequences are wrapping unsigned 32-bit. They are not interchangeable.
- SQL `INTEGER` values must validate `DimensionId` range and nonnegative revision constraints at decode/API boundaries.

This is a greenlight candidate. Do not duplicate a block entity in both a section payload and a second authoritative table.

### P1.2 One writer and one publication owner

The server contract is coherent: workers consume immutable snapshots, produce bounded private results, and the simulation thread alone publishes resident world state at a phase barrier. Worldgen and structures must return target-section-only patches; no worker writes a loaded neighbor.

Use the same strictness client-side. Meshing/light workers produce plain owned buffers. The **Godot main thread** is the v1 publication owner: validate the exact input stamp, create/swap resources, then retire old RIDs under a budget. “Main/render thread” must not be interpreted as a user-created render thread. Direct `RenderingServer` calls versus `MeshInstance3D` remain a benchmark choice behind one adapter.

Greenlight ownership/stale-result rules. Experiment-gate worker count, snapshot strategy, upload backend, and all time/byte limits.

### P1.3 Meshing and lighting invalidation are mostly coherent

Geometry edits invalidate the edited section and every section whose one-cell halo contains the changed sample. Neighbor arrival/removal invalidates both sides. Lighting invalidation comes from the solver's changed bounds, not guessed radius. Registry reloads use a new immutable registry epoch. Stage A remeshes when baked light changes; Stage B uploads a light page without changing mesh revision.

Required test invariant: after arbitrary edits, neighbor churn, unload/reload, light propagation, and registry swaps, no result with a stale content/light/registry/LoD/epoch tuple may publish. Collision and selection always read voxel state, never resident mesh state.

### P1.4 The “64-resolution lighting” rejection is mathematically correct

For a 16³ section at 64 samples per block edge:

```text
(16 × 64)^3 = 1,073,741,824 samples
RG8 = 2,147,483,648 bytes = 2 GiB per section
```

The proposed block-resolution halo page is also correct:

```text
18^3 × 2 bytes = 11,664 bytes = 11.39 KiB
4,096 pages = 45.56 MiB logical payload
```

Greenlight the semantic split: block-scale gameplay/world light plus per-fragment material shading. Reject 64³ persistent world light. Experiment-gate the 18³ page backend: alignment, staging duplication, partial uploads, descriptor limits, filtering leaks, and GPU cost are unresolved. If P0 selects 32³, recalculate the pool before any claim survives.

### P1.5 Four material passes are a useful ceiling, not proof of cheap rendering

One shared bank and at most opaque, alpha-test, translucent, and fluid surfaces prevents content variety from multiplying surfaces. This is a good v1 invariant. It does not make 1,000 visible sections cheap: each populated surface is still submission/culling/GPU work, transparency still sorts approximately, and texture-array limits are hardware/backend dependent.

Greenlight fixed render classes, compiled data-only materials, no arbitrary pack shaders, and explicit fallbacks. Experiment-gate `Texture2DArray`, 2,048 layers, 256 MiB, direct RIDs, and greedy merging. Defer OIT, broad refraction, and full near-PBR behavior at far LoD.

### P1.6 Worldgen provenance is right; its upgrade system is too large for the first slice

Greenlight exact generator fingerprints, keyed randomness, immutable target-only patches, section generation stamps, and “missing exact generator fails rather than silently regenerates.” Greenlight coordinate-owned bounded structure plans, target-section clipping, deterministic conflict order, stable child IDs, and application receipts committed with section revisions.

Defer mixed-epoch seam adapters, 128×128 ownership tiles as a frozen format, a 256-block blend width, and million-operation/64 MiB structures until the minimal pinned-generator pipeline passes. Those numbers are ceilings invented by the draft, not evidence-based content needs. A first playable needs one pinned profile and small deterministic cross-section fixtures, not a general worldgen-upgrade platform.

### P1.7 The 20 Hz/32 Hz contradiction is resolved in current files

Current authority is one 20 Hz `WorldTick`; packet/render cadence is independent, and a nested 40 Hz controller is permitted only after measured 20 Hz failure. This is coherent and experiment-gated for feel/capacity.

`NET-06` still contains an archival 32/64/128 profile table and related prose. It is no longer normative but remains easy to misread. Mark the entire archival block as historical or move it to an appendix before implementation derives constants from that file.

## P2 — later cleanup and advanced features

- Ship the first renderer with finite full-detail terrain and ordinary depth/height fog coupled to the **safe resident radius**. If residency falls below playable visibility, show recovery/loading state rather than blinding the player.
- Every streamer and LoD test must pass a no-fog diagnostic. Fog may soften a frontier; it may not hide holes, duplicate faces, invalid transitions, or unauthorized client-generated terrain.
- Defer 3D far summaries until near streaming, persistence, material fallback, and bandwidth are measured. The proposed 256 MiB CPU/GPU and 1 MiB/s limits are placeholders.
- Volumetric fog is an optional Forward+ effect, never correctness. GI, SSR, and refraction remain independently disableable presentation terms.
- Pin mutable source links to inspected commits/releases before these documents become long-lived engineering records. Under this deadline, non-SQLite factual claims were not exhaustively revalidated.

## Coherent v1 contract

1. Freeze section edge after P0; retain sparse signed-64 3D identity regardless of the result.
2. Run one deterministic 20 Hz simulation writer; parallelism ends at immutable proposals and ordered barriers.
3. Store one revisioned atomic section payload; use explicit transactions for cross-record conservation.
4. Use one local SQLite store/writer in verified WAL/FULL mode only after the durability campaign passes.
5. Pin one generator profile; persist provenance; use small bounded coordinate-owned structure plans.
6. Publish meshes/light pages only on the Godot main thread after exact typed-stamp validation.
7. Start with finite full-detail sections, four material classes, block-scale light, and ordinary fog.
8. Keep vertex-baked light and a simple node/`ArrayMesh` adapter as correctness fallbacks.
9. Do not implement far LoD, volumetrics, GI, arbitrary shaders, mixed generator epochs, or a native renderer in the first playable.

## Prioritized benchmark and fault-injection matrix

| Priority | Harness | Required adversary | Pass/fail decision |
| --- | --- | --- | --- |
| 0 | Core data | 16³/32³; negative and near-overflow coordinates; high-entropy palettes | Freeze edge/codec only if keys never alias/overflow and memory/CPU win is measured. |
| 0 | Persistence | Kill before/after BEGIN, row writes, COMMIT, receipt, checkpoint; disk full; busy readers; corrupt blobs | Every receipt survives on supported sync-honoring storage; atomic groups are old-or-new; otherwise reject current backend/receipt contract. |
| 0 | Native packaging | Load the pinned SQLite binary on every target; assert compile/runtime options and patched version | Any unpinned or vulnerable runtime blocks release. |
| 1 | Lifecycle/jobs | Teleport/edit/unload storms; reordered duplicate completions; stalled storage; hard memory pressure | Zero stale publication, dirty eviction, lost ownership, or unbounded queue growth. |
| 1 | Rendering | Node versus direct RID; edit/light/pack churn; origin rebase; device/resource teardown | Choose backend and budgets from recorded reference hardware; all resources return to baseline. |
| 1 | Lighting/materials | Poison page reuse; missing halos; 16/32 edge; 8/256/1,024 materials; transparent torture scene | No stale/leaking light; surface count stays class-bounded; fallback remains correct if pages/arrays fail. |
| 1 | Worldgen/structures | 1/2/4 workers; reverse/random generation; duplicate jobs; restart at plan/receipt boundaries; edited prior piece | Identical hashes/IDs, target-only writes, idempotent receipts, and zero rewrite of edited sections. |
| 2 | Fog/LoD | No-fog captures; stalled frontier; teleport; cave/water/thin structures; missing/corrupt far cache | Fog never earns correctness; visible holes or stale parents defer LoD. |

## Final recommendation

Approve the coherent invariants above and immediately run P0 core data plus P1 persistence. Do **not** greenlight numeric budgets, SQLite durability, direct `RenderingServer`, light pages, texture-array capacity, mixed worldgen epochs, or far LoD from prose alone. The current packet is strong enough to drive focused prototypes; it is not strong enough to justify broad implementation in parallel.
