# G1 core-data gate review

Issue: [#10 — Review and freeze the core-data handoff—or return it for revision](https://github.com/Oreoezi/vibecraft/issues/10)  
Reviewed handoff: [#9 — Execute the core-data benchmark and record a disposition](https://github.com/Oreoezi/vibecraft/issues/9)  
Reviewed implementation: [PR #25](https://github.com/Oreoezi/vibecraft/pull/25), squash commit `7b6605cbe3c6c8b1a7511e026026e48559c9e003`  
Upstream E1 result: `defer`  
Compatibility action: none

## Gate result

`revise`

This is the sole categorical result of the G1 gate review. It returns the handoff
for another bounded evidence iteration. It does not greenlight G1, select a section
side or container, change the compatibility ledger, authorize a persistent user
world, or create a storage, wire, pack, or public-API promise.

The full E1 observational profile completed, but it selected neither 16³ nor 32³.
Both adaptive candidates failed the predeclared adaptive-versus-dense timing rule,
`VC-G0-FP-0.1.0` has no owner acceptance, the implementation does not consistently
use the ledger's named scalar domains, and no accessible immutable semantic read
contract exists for downstream assemblies. Each condition independently prevents a
G1 greenlight. The substantial correctness evidence that passed remains useful and
must be retained; it does not convert a failed gate into a partial freeze.

E1 also reported actual save/network amplification as inconclusive. Formal review
finds that criterion invalid at G1: persistence belongs to G2, transport belongs to
G4A, and both depend on G1. Requiring their formats to greenlight G1 would create a
forbidden reverse dependency. The next E1 protocol must return to G1-owned
memory/read/edit/snapshot/remesh evidence and leave actual save/network measurement
to the owning downstream gates.

## Evidence reviewed

The retained E1 observation was produced from clean, locked-restorable source
commit `bc8117549935cf74d6fa3870e4364bfc05ee24ff`. It is identified by fixture
`VC-G1-E1-SECTIONS-0.1.0`, logical projection fixture
`VC-G1-E1-LOGICAL-PROJECTION-0.1.0`, G0 fixture `VC-G0-FP-0.1.0`, and seed
`0x5643424654314531`.

- [E1 disposition](E1-core-data-disposition.md)
- [Machine-readable full-profile observation](../../../artifacts/g1/e1/full-observational/e1-core-data-observation.json)
- [Human-readable full-profile observation](../../../artifacts/g1/e1/full-observational/e1-core-data-observation.md)
- [Bounded raw observations](../../../artifacts/g1/e1/full-observational/e1-core-data-raw.ndjson), 44,032 rows / 21,012,267 bytes / SHA-256 `2028eacc44cc9c9b3e45bd3b96d0235d7c7bd3f5959ead230ba31deb0131eb5f`
- [G0 acceptance fixture](../G0/acceptance-fixture.md)
- [Provisional compatibility-surface ledger](../COMPATIBILITY_SURFACE.md)
- [G1 source gate and exit criteria](../../research/REVIEW-product-scope-and-sequencing.md#g1--core-data-and-irreversible-format-spike)
- [Gate contracts and planned handoffs](../../research/DEPENDENCY-MAP.md#gate-contracts-and-work-order)
- [Owner decisions](../../OWNER_DECISIONS.md)
- [Coordinate boundary and property tests](../../../tests/VibeCraft.G1.Tests/Coordinates/CoordinateTests.cs)
- [Section-container and allocation tests](../../../tests/VibeCraft.G1.Tests/Sections/SectionBlockStatesTests.cs)
- [E1 deterministic conformance tests](../../../tests/VibeCraft.G1.Tests/Sections/E1CoreDataReportTests.cs)
- [World-state ordering tests](../../../tests/VibeCraft.G1.Tests/Content/WorldStateMapTests.cs)
- [Required-content refusal and recovery tests](../../../tests/VibeCraft.G1.Tests/Content/ContentLockAndRecoveryTests.cs)
- [Logical record-key codec tests](../../../tests/VibeCraft.G1.Tests/LogicalCodecs/LogicalRecordKeyCodecTests.cs)
- [Canonical logical-projection codec tests](../../../tests/VibeCraft.G1.Tests/LogicalCodecs/CanonicalLogicalProjectionCodecTests.cs)
- [Section-revision tests](../../../tests/VibeCraft.G1.Tests/Revisions/SectionRevisionTests.cs)
- [World-clock tests](../../../tests/VibeCraft.G1.Tests/Time/WorldClockTests.cs)
- [Action/publication phase tests](../../../tests/VibeCraft.G1.Tests/Phases/WorldTickPhaseTests.cs)
- [Production dependency-boundary tests](../../../tests/VibeCraft.G1.Tests/Bootstrap/DependencyBoundaryTests.cs)
- [Project and package boundary tests](../../../tests/VibeCraft.G1.Tests/Bootstrap/ProjectBoundaryTests.cs)

Correctness and integration passed on the exact final PR head
`7a647382c17496da32d64cf9a542be68c184ff99`: [.NET 10 on Ubuntu](https://github.com/Oreoezi/vibecraft/actions/runs/31740933223/job/94583842080),
[.NET 10 on Windows](https://github.com/Oreoezi/vibecraft/actions/runs/31740933223/job/94583842115),
[Dependency Review](https://github.com/Oreoezi/vibecraft/actions/runs/31740933224/job/94583842327),
and [CodeQL for C#](https://github.com/Oreoezi/vibecraft/actions/runs/31740933224/job/94583842323).
These runs support cross-platform correctness and deterministic fixture checks;
they are not cross-machine performance comparisons.

Fixture IDs, seeds, source hashes, assembly hashes, corpus fingerprints, raw
digests, and CI run identities identify evidence only. None is a user-facing
compatibility identifier.

## Exit-criterion review

| Criterion | Status | Review |
| --- | --- | --- |
| G0 owner acceptance precedes performance selection | Blocked | The host, runtime, GC and power conditions and applicable product budgets remain unaccepted. The G0 fixture explicitly prohibits a performance-related freeze without that acceptance. |
| Godot-free bounded G1 experiment | Pass | The production graph excludes Godot, SQLite, Steamworks, and GameNetworkingSockets. The corpus and reports use ephemeral fixtures only. |
| Negative, large, and checked-overflow coordinates | Pass | Both candidate geometries cover negative floor boundaries, values beyond signed 32-bit range, signed 64-bit extrema, checked origins/ends/neighbors, and 1,000 property-generated round trips. |
| Select 16³ or 32³ from measured evidence | Fail | E1 explicitly selects neither candidate. Side 32 fails retained-memory, five-primary, and amplification rules. Side 16 passes its relative rules but cannot be selected while adaptive timing and G0 acceptance remain unresolved. |
| Adaptive retained-memory rule | Pass observationally | Both layouts pass the predeclared homogeneous/layered/mixed and high-entropy retained-memory limits on the recorded host. This is one-host observational evidence. |
| Adaptive timing rule | Fail | Side 32's maximum upper interval is 273.196319 and side 16's is 68.621421 against a 1.15 ceiling. Thresholds must not move after observing this result. |
| Snapshot creation measurement | Pass | The retained profile times immutable snapshot creation plus O(1) metadata consumption; complete semantic reconstruction is an untimed correctness check. |
| Zero-allocation warmed reads and no ordinary block objects | Pass | The fixed-runtime raw probes report zero thread allocation for every warmed random and linear read. Candidate storage uses scalar/array representations rather than one object per voxel. |
| X-contiguous, then Z, then Y indexing correctness | Pass as candidate evidence | Exhaustive bijection tests and both candidate layouts agree with the logical projection. It is not frozen because no side/container was selected. |
| Stable semantic names and world IDs | Pass as candidate evidence | Reconciliation, restore, and runtime projection remain deterministic under reordered discovery; ID exhaustion refuses without wrap or reuse. |
| Unknown state remains distinguishable from air | Pass as candidate evidence | Air is explicitly world-state ID zero. Missing required gameplay content refuses activation; bounded recovery data preserves the original state and never substitutes playable air. |
| Named scalar-domain alignment | Blocked | The ledger requires `LocalIndex`, `BlockStateId`, and `NamespacedContentId`; current candidate code uses raw `int`, `WorldStateId`, and `ContentKey` in those roles. Their tested behavior is useful, but width similarity or conceptual overlap cannot substitute for the named domains required by the repository contract. |
| Deterministic record identity and logical projection | Pass as candidate evidence | Extrema goldens, property round trips, canonical ordering, bounded refusal, logical projection re-encoding, semantic hashes, and both target-OS CI checks pass. The codecs remain storage-neutral fixtures. |
| Checked revision domain | Pass as candidate evidence | Section revisions reject negative values and report exhaustion without wrapping. No persistence or wire representation follows from the CLR type. |
| Initial build-range policy | Pass as candidate evidence | The executable policy uses explicit inclusive minimum and exclusive maximum bounds separated by 10,000 blocks, with checked overflow. It is not a frozen dimension-descriptor schema. |
| One clock and phase vocabulary | Pass as candidate evidence | Owner direction and tests use one 60 TPS `WorldTick` and `OwnerStart → Actions → OwnerCommit → Publication`. E3 must still prove capacity; no wire or persistence field is frozen here. |
| E1 actual save/network amplification criterion | Return for protocol revision | No actual save schema or network encoding exists because G2 and G4A own them and depend on G1. The inconclusive observation is retained, but this criterion must be removed from G1 rather than becoming a reverse dependency. Logical-projection bytes remain a storage-neutral diagnostic and remesh-halo samples remain G1-owned proxy evidence. |
| Accessible immutable downstream semantic read contract | Blocked | Current immutable snapshots and candidate containers remain internal implementation evidence. A future G1 greenlight must name an accessible semantic read seam without exposing candidate storage internals. |
| Freeze the G1 compatibility bundle | Blocked | A freeze is allowed only after every applicable G1 prerequisite passes. The ledger remains provisional and unchanged. |

## Compatibility disposition

[`COMPATIBILITY_SURFACE.md`](../COMPATIBILITY_SURFACE.md) remains **Provisional**.
Owner decisions continue to constrain internal architecture, but this review does
not promote their CLR representations or the tested candidates into compatibility
contracts.

The following remain explicitly unselected or candidate-only:

- `SectionSide`: neither 16 nor 32 is selected;
- the one-side-32 and eight-side-16 equal-volume layouts;
- X-contiguous, then Z, then Y local indexing as a compatibility promise;
- `LocalBlock` and `LocalIndex` widths or byte encodings;
- adaptive `Uniform | Paletted | Direct` storage as the selected hot container;
- palette ordering, growth, compaction, packed-index layout, reverse lookup,
  direct-mode cutoffs, and mutable/snapshot object graphs;
- the named `LocalIndex`, `BlockStateId`, and `NamespacedContentId` domains, which
  current candidate code does not implement consistently; raw `int`,
  `WorldStateId`, and `ContentKey` are not approved substitute domains;
- `DimensionId`, `BlockCoord`, `SectionCoord`, `ColumnCoord`, `SectionKey`,
  `SectionRevision`, and `WorldTick` CLR representations at any persistence or
  wire boundary;
- strict required-content lock behavior as a frozen world/handshake schema, despite
  its accepted owner direction and executable refusal tests;
- the 10,000-block initial build range as a frozen dimension-descriptor format;
- `OwnerStart → Actions → OwnerCommit → Publication` as a serialized phase encoding;
- the 30-byte `LogicalRecordKeyCodecV1` fixture as a database key, save record key,
  packet key, migration format, or public ordering contract;
- the canonical logical projection as a save payload, packet payload, database
  record, compression input contract, migration format, or public wire format;
- content registry records, runtime-state tables, save records, database layouts,
  network DTOs, field numbers, framing, compression, retransmission assumptions,
  durability envelopes, and public API signatures;
- every fixture ID, seed, corpus schedule, checksum, and digest as a user-world
  compatibility identifier.

The selected in-memory implementation and the storage-neutral semantic seam are
separate decisions. No in-memory implementation is selected here. Even after a
future selection, CLR arrays, dictionaries, palette order, packed words, object
identity, reflection names, alignment, and raw memory can never become durable or
wire constants. Persistence and transport must define explicit, versioned, bounded
schemas and codecs at their owning gates.

## Scope audit

No persistent user world, Godot integration, generator epoch system, structures,
plugins, transport implementation, renderer, survival feature, database, or save
backend entered G1. Production dependencies and the project graph enforce the
Godot/SQLite/GNS boundary. Logical codecs and benchmark artifacts operate on
ephemeral fixtures and do not establish durability.

No user world may be created against the current candidates. A future G1
greenlight would still not authorize one: G2 must separately greenlight crash-safe
persistence, recovery, refusal, and migration behavior before a persistent user
world can exist.

## Downstream consequences

This review establishes no frozen G1 handoff:

- G2 may not hard-code a section side, CLR container, logical fixture codec, or
  record-key fixture as a durable world format.
- G3 may not treat current scalar layouts, `WorldTick`, revisions, collision views,
  or phase enum values as frozen application or wire fields.
- G4B may not expose internal containers, assume a section side, or persist/render
  against snapshot object layout.
- G4A and G5 receive no new authorization from this review.

G2, G3, and G4B gate progress remains blocked on G1; this review grants no
authorization to start them behind candidate adapters. Work may continue only on
the numbered G1 revision and on independently scheduled work whose declared hard
inputs do not include G1. Only a later G1 greenlight may name the narrow G2
deterministic-persistence projection, G3 collision/time view, and G4B
immutable-render-snapshot handoffs.

## Rejected shortcuts

- Do not freeze side 16 merely because it was the architectural prior or passed its
  relative section-side rule.
- Do not freeze side 32 from lower section-object count.
- Do not ignore the adaptive timing failure because retained memory passed.
- Do not reinterpret the Linux observation as cross-machine or cross-platform
  performance evidence.
- Do not invent placeholder save or network encodings solely to make this gate pass.
- Do not treat logical-projection bytes as save or wire bytes.
- Do not use the one-round CI smoke profile as a performance decision.
- Do not change thresholds after observing the result.
- Do not partially mark the compatibility ledger frozen.

## Required revision

The next numbered G1 revision must:

1. obtain owner acceptance of `VC-G0-FP-0.1.0` or a versioned successor, including
   named benchmark hosts, runtime, GC and power modes, and applicable G1
   memory/read/edit/snapshot/remesh budgets;
2. align implementation and documentation on the exact named scalar domains,
   especially `LocalIndex`, `BlockStateId`, and `NamespacedContentId`; do not retain
   raw `int`, `WorldStateId`, or `ContentKey` as undeclared substitutes merely
   because their current behavior or widths overlap;
3. diagnose and revise or simplify the adaptive mutable representation, then rerun
   the predeclared adaptive-versus-dense read and edit protocol without post-hoc
   threshold changes;
4. version the E1 protocol to remove actual save/network formats as G1
   prerequisites, retain G1-owned remesh evidence and storage-neutral logical
   diagnostics, and defer actual persistence/transport amplification to G2/G4A;
5. define an accessible, candidate-independent immutable semantic read contract for
   the planned G3 collision view and G4B render snapshot, while keeping mutable and
   packed container implementations internal; the future G2 projection must consume
   semantics rather than CLR storage layout;
6. retain deterministic correctness checks, semantic fingerprints, complete raw
   observations, failed attempts, and the unchanged no-outlier-removal policy;
7. run the decision-eligible profile on the owner-accepted fixture and retain
   successful Windows and Linux correctness/hash evidence for the exact reviewed
   implementation; and
8. submit a new G1 gate review. Only a greenlight review may change the
   compatibility ledger or authorize the three narrow downstream handoffs.

Closing issue #10 after this document merges means the requested review completed
and returned the handoff for revision. It does not close the G1 epic, greenlight
E1, freeze compatibility, or authorize downstream implementation.
