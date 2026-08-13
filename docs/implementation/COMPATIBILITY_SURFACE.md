# G1 compatibility surface ledger

Status: **Provisional.** Nothing in this ledger is frozen, persisted for users, or a
public wire promise until G1 receives a written greenlight. G1 uses ephemeral
fixtures only. Its exit freezes the section key/side/indexing, persistent-ID
distinction, revision representation, and canonical record-key encoding.

Sources: [G1 gate](../research/REVIEW-product-scope-and-sequencing.md),
[integration resolutions](../research/INTEGRATION-RESOLUTIONS.md),
[WORLD-01](../decisions/WORLD-01-chunk-coordinate-and-memory-model.md),
[WORLD-03](../decisions/WORLD-03-world-storage-layout.md),
[ARCH-02](../decisions/ARCH-02-simulation-data-model.md), and
[NET-07](../decisions/NET-07-protocol-versioning.md).

`Proposed` below means a research direction, not permission to serialize a CLR
type. Each persistence or wire use must have an explicit schema/codec and bounded
decode. Named domains are not interchangeable merely because their storage widths
match.

| Domain | Owner | Proposed width and meaning | Ordering | Overflow / wrap | Persistence / wire status | Freeze gate |
| --- | --- | --- | --- | --- | --- | --- |
| `DimensionId` | WORLD-01 / WORLD-03 | `uint32`; world/dimension identity | Part of a section key; canonical byte order unselected | Reject out-of-range; no wrap | Candidate SQL/key and schema field; no public encoding | G1 persistent-ID and key freeze |
| `BlockCoord` | WORLD-01 | Three independent signed `int64` values: X, Y, Z | Logical component order is X, Y, Z | Checked conversion and neighbor arithmetic; no wrap | Candidate payload/wire fields; never a packed persistent key | G1 scalar/key freeze |
| `SectionCoord` | WORLD-01 | Three independent signed `int64` section values | Logical component order is X, Y, Z | Checked origin/end and neighbor arithmetic; no wrap | Candidate payload/wire fields; never a packed persistent key | G1 scalar/key freeze |
| `ColumnCoord` | WORLD-01 / WORLD-03 | Two independent signed `int64` values: X, Z | X, Z | Checked arithmetic; no wrap | Derived/index candidate, not an independent public format | G1 key freeze |
| `SectionKey` | WORLD-01 / WORLD-03 | `DimensionId` + `SectionCoord` | Logical tuple is proposed as dimension, X, Y, Z; current SQL primary-key order is dimension, X, Z, Y. The canonical byte order is **unresolved**. | Component rules apply; no packing or wrap | Candidate persistent key and schema identity | G1 canonical record-key encoding |
| `SectionSide` | WORLD-01 | One selected constant: 16 or 32; storage width intentionally unselected | Defines local-coordinate/index order | Reject values other than the selected profile | Candidate section-format parameter only | G1 measured 16³/32³ decision |
| `LocalBlock` | WORLD-01 | Three bounded unsigned local components; `byte` is a candidate representation | X, Y, Z; valid range is `0..SectionSide-1` | Reject out-of-range; no wrap | In-memory and logical-section candidate; encoding waits for side/index freeze | G1 side/indexing freeze |
| `LocalIndex` | WORLD-01 | Derived index into one section; width unselected | For 16³ candidate: X contiguous, then Z, then Y | Checked derivation; no wrap | Internal/logical payload candidate, not a cross-domain ID | G1 side/indexing freeze |
| `BlockStateId` | GAME-01 / WORLD-01 | World-local `uint32` state ID | Registry mapping, not runtime discovery order | Reject invalid/unmapped values; no wrap | Candidate persisted section/registry value; schema field allocation not frozen | G1 persistent-ID distinction |
| `NamespacedContentId` | GAME-01 / WORLD-09 | Stable namespaced semantic identifier; byte/string encoding unselected | Canonical registry ordering must be deterministic, not discovery/load order | Invalid syntax or unknown required content rejects; no substitute placeholder | Candidate registry/persistence identity; not a numeric mod-ID band | G1 stable-name/world-ID round trip |
| `WorldTick` | WORLD-08 / WORLD-01 | `uint64` authoritative logical tick | One server-owned total timeline | Do not reinterpret as elapsed time; end-of-range policy unselected | Candidate wire/snapshot field; not a G1 persisted record commitment | G1 selects one owner and phase vocabulary |
| `ClientInputSequence` | WORLD-01 / ARCH-01 | `uint32`, connection-local input ordering | Serial arithmetic within one connection only | Intentional wrap; never converted directly to authority time | Candidate wire field; never persistent world state | G3 application-semantics freeze |
| `ClientPredictionStep` | WORLD-01 / ARCH-01 | `uint32`, connection-local prediction ordering | Serial arithmetic within one connection only | Intentional wrap; never converted directly to authority time | Candidate wire field; never persistent world state | G3 application-semantics freeze |
| `SectionRevision` | WORLD-01 / WORLD-03 | Nonnegative signed `int64` (`long`) | Monotonic within its owning section | Checked increment; a valid world never wraps | Candidate persisted and wire field | G1 revision-representation freeze |
| `SaveSequence` | WORLD-03 | Nonnegative signed `int64` | Persistence-writer order only | Checked increment; no wrap | Proposed persistence metadata only; no wire use | G2 record-envelope/durability freeze |
| `PayloadVersion` | WORLD-03 / WORLD-09 | `uint16` per record family | Header field order is not yet canonicalized | Reject unsupported values; no wrap | Proposed persistence envelope only | G2 record-envelope freeze |
| `GeneratorVersion` | WORLD-01 / WORLD-03 | `uint32` | Semantic header field; canonical header ordering unselected | Reject unsupported values; no wrap | Proposed persistence metadata; no general wire use | G2 record-envelope freeze |
| `RuntimeEntityHandle` | ARCH-02 | Opaque generational runtime handle; width/layout unselected | No semantic order | Stale/reused handles must be rejected; no persistence or wrap contract | Runtime-only; never serialize its CLR layout | Later entity-store gate |
| `PersistentEntityId` | ARCH-02 / WORLD-03 | Stable persistent identity; exact width/codec unselected | No numeric ordering promise | No reuse policy is frozen | Proposed persistence identity; wire projection unselected | Later entity/save schema gate |

## Format rules carried into G1

- Canonical world coordinates are engine-independent integers. Godot vectors are
  client-local presentation values only.
- Persistence and wire schemas name fields explicitly. They never inherit CLR field
  order, alignment, endianness, object identity, reflection metadata, or raw-memory
  layout.
- The section body and network DTOs are separate schemas. A future Protobuf payload
  is not canonical by byte reserialization; checksum/hash inputs use an explicit
  VibeCraft envelope/key encoding.
- No G1 dependency is permitted on Godot, SQLite, or GameNetworkingSockets. SQLite
  is evaluated at G2; GNS is evaluated at G4A.
- Benchmarks decide `SectionSide`, indexing, and storage representation only with a
  named G0 acceptance fixture. They do not create a compatibility claim by themselves.

## G1 greenlight evidence

Greenlight requires the G1 exit evidence from the source gate: coordinate boundary
and checked-overflow property tests; a measured 16³/32³ decision; zero-allocation hot
reads with no per-block objects; stable-name/world-ID round trips independent of
registration order; unknown-state-versus-air distinction; deterministic logical
serialization and canonical hashes across declared platforms; and one selected
`WorldTick` owner plus action/publication phase vocabulary. Record `greenlight`,
`revise`, `defer`, or `reject` beside the evidence before changing any status above.
