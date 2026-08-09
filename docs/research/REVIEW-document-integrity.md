# Independent documentation-integrity audit

Status: Complete against the frozen snapshot below  
Audit date: 2026-08-09  
Scope: every Markdown file under `docs/` plus `design_doc.md`  
Method: read-only inspection and machine checks; this file is the only audit write

## Snapshot and verdict

The audited pre-review corpus contained 58 Markdown files (57 under `docs/` plus `design_doc.md`), 13,290 lines, and 1,172,469 bytes. The aggregate path-and-content SHA-256 was `4efb9a4f96f930b2361e106c65a0b4bb9d3c384b0565bf9349fb766f668feee9` at 2026-08-09 21:10:27 +03:00.

The packet is mechanically healthy: all 41 decision IDs are unique and resolvable, fenced blocks are balanced, all ten JSON examples parse, and no impossible arithmetic was found. It is not yet safe to treat as one implementation contract. The remaining blockers are document authority, stale review text after integration, incompatible coordinate/tick/revision types, and a `Requires` graph that cannot provide an implementation order.

> Integration follow-up: the audited hash intentionally remains a frozen snapshot.
> After that snapshot, the packet added an explicit authority order and review-snapshot
> notices; normalized coordinate, tick, revision, render-stamp, and lock-schema
> domains; made section side explicitly E1-gated; isolated the NET-06 archival block;
> added the missing world/render/storage review; and created an acyclic
> [`DEPENDENCY-MAP.md`](DEPENDENCY-MAP.md). Mutable branch-relative citations remain
> a P2 cleanup at greenlight, and all mechanism experiments remain unrun.

## P0 — resolve before deriving implementation contracts

### 1. There is no unambiguous document-precedence rule

- `docs/READ_ME_FIRST.md:17-30` calls its list the “Current integrated baseline,” and `docs/decisions/README.md:76` says the integrated requirements/checklist takes precedence.
- `docs/research/PROPOSED-REQUIREMENTS-BASELINE.md:6` simultaneously says that the baseline does **not** modify or supersede `design_doc.md`, while `:8-12` declares normative MUST/SHOULD/MAY language.
- The untouched source sheet still requires configurable 32/64/128 ticks (`design_doc.md:15`), square/no-maximum-height chunks (`:20`), DDoS safety and client-mod assertion (`:21-22`), per-1/64 fully client-side lighting (`:33-34`), and scoped native mods (`:40`). The integrated baseline rejects or materially narrows each of these at `docs/READ_ME_FIRST.md:19-30`.
- There is also a direct current-contract conflict: `docs/research/PROPOSED-REQUIREMENTS-BASELINE.md:59` says section edge/indexing is not frozen and 16³ is only a candidate; `docs/decisions/WORLD-01-chunk-coordinate-and-memory-model.md:88-90` declares `SectionSide = 16` and its indexing order to be v1 world-format constants.

Impact: two implementers can follow the documented reading order and still select opposite requirements. Integrity correction: state one explicit authority order and label unchanged source material as historical input where applicable; do not rely on “integrated” to imply precedence.

### 2. Recommended adversarial reviews describe the pre-integration tree as current

`docs/READ_ME_FIRST.md:10-13` directs readers through these reviews without a snapshot/superseded warning:

- `docs/research/REVIEW-architecture-networking.md:11`, `:49-54`, and `:71-77` say the current briefs still use 32/64/128 Hz, dual action paths, selected GNS, and unbounded collision history. The integrated decisions now specify 20 Hz (`NET-01:29`, `NET-02:12`, `NET-06:71-80`), GNS as a prototype candidate (`NET-03:10`), and a byte-capped journal (`NET-02:149`). Its exact edit map at `REVIEW-architecture-networking.md:321-337` therefore no longer describes the current files.
- `docs/research/REVIEW-assets-modding-security.md:19-29`, `:103-107`, and `:375-410` still assert unresolved package-class, digest, GLB, WIT, principal, and hostile-compilation conflicts. Many listed corrections have landed: the class enum is now `resource_pack`/`data_pack`/`sandbox_component`/`native_plugin`; `NET-09:57-73` uses the integrated digest terms; `MOD-01:99-102` uses exact WIT syntax; and `ASSET-04` delegates the GLB contract to `ASSET-03`. The review remains useful evidence, but its verdict and correction matrix are stale as statements about the current tree.
- `docs/research/REVIEW-product-scope-and-sequencing.md:315-323` still calls the old clock/hosting/action/worldgen issues contradictions. In particular `:322` says `WORLD-02` omits Y in publication examples, but `WORLD-02:149` now explicitly keys randomness with the full `(dimension,x,y,z)` `SectionKey`.
- `docs/research/INTEGRATION-RESOLUTIONS.md:8-27` records the resolutions, but it does not version or mark the earlier review findings as resolved. Readers are asked to reconcile contradictory present-tense verdicts themselves.

Impact: these are not merely historical notes; they are in the prescribed reading path and contain “must edit” instructions against already-edited files. Add reviewed-snapshot metadata and per-finding resolved/unresolved status, or clearly mark the old verdict/correction sections as pre-integration evidence.

### 3. Canonical 3D coordinate contracts still have incompatible names and widths

The canonical contract is `BlockCoord(long,long,long)`, `SectionCoord(long,long,long)`, and `SectionKey(DimensionId, SectionCoord)` at `docs/decisions/WORLD-01-chunk-coordinate-and-memory-model.md:77-94`; network coordinates must use `sint64` per axis at `:134`.

Remaining incompatible uses:

- `docs/decisions/NET-04-block-interaction-lag-compensation.md:98-101` defines a global block target with `sint32 x/y/z`. It cannot encode the canonical signed-64-bit space.
- `docs/decisions/WORLD-07-structure-generation.md:138` uses undefined `BlockCoordinate`, and `:233` uses undefined `LocalBlockCoordinate`; the canonical types are `BlockCoord` and `LocalBlock`.
- `docs/decisions/NET-05-interest-management.md:74` says all spatial interest is indexed by `SectionCoord`, omitting `DimensionId`/`SectionKey` from the identity statement.
- `docs/decisions/RENDER-02-mesh-job-pipeline.md:92-95` and `RENDER-03-far-terrain-lod.md:116-119` use Godot `Vector3I` as section/tile coordinates. The files do not state that these are rebased/local-only values or define a checked conversion from the signed-64-bit canonical key. `Vector3I` therefore appears to narrow the world coordinate and leaks an engine type into a cross-document identity.

Impact: these discrepancies affect wire compatibility, hashing, lookup identity, and large/negative-coordinate correctness. Each occurrence needs either the canonical type or an explicit, bounded local/render type with a checked conversion.

## P1 — contract and sequencing defects

### 4. `WorldTick` and revision domains are not type-consistent

- `WORLD-08:90` and `NET-06:73` define `WorldTick` as unsigned 64-bit. `NET-01:121-136` instead uses `uint ClientTick`, `uint LastReceivedServerTick`, and `uint CollisionRevision`; `ARCH-01:115-126` uses generic `client_tick`/`server_tick`; `ARCH-04:186,233` uses `effective_server_tick`; and `NET-04:87,110` uses `client_tick`/`committed_server_tick`. `NET-02:81,112-133` intentionally uses wrapping `uint32` client ordering, but no shared type/name separates `ClientPredictionSequence` from authoritative `WorldTick` across the other contracts.
- Persisted section revision is a checked nonnegative signed `long` at `WORLD-01:112`, `WORLD-03:207-217`, and `WORLD-05:157-173`. `WORLD-06:218-224` persists `GeneratedRevision` as `ulong`. `NET-01:136` uses a 32-bit collision revision, while `NET-04:101,113` uses unsigned-64 cell/inventory revisions without declaring whether these are section revisions or separate domains.

Impact: the documents do not define safe conversion, wrap comparison, or whether identically named revisions share a domain. Introduce explicit shared aliases/domain names in the docs before schemas or persisted records are frozen.

### 5. The `Requires` graph is circular and too broad to sequence implementation

The 41 decisions contain 179 explicit `Requires` edges. Strongly connected-component analysis finds three cycles containing 28 decisions:

- 23-node component: `ARCH-01`, `ARCH-02`, `ARCH-03`, `ARCH-05`, `ASSET-01`, `ASSET-02`, `ASSET-03`, `GAME-01`, `MOD-01`, `MOD-02`, `MOD-03`, `NET-09`, `RENDER-01`, `RENDER-02`, `RENDER-04`, `WORLD-01`, `WORLD-02`, `WORLD-03`, `WORLD-04`, `WORLD-05`, `WORLD-06`, `WORLD-08`, `WORLD-09`.
- 3-node component: `NET-04`, `NET-05`, `NET-06`.
- 2-node component: `RENDER-03`, `RENDER-07`.

Representative direct cycles are `WORLD-01:201` ↔ `WORLD-06:370`, `WORLD-04:318` ↔ `WORLD-05:285`, `ARCH-02:118` ↔ `GAME-01:243`, `RENDER-01:237` ↔ `RENDER-04:303`, `NET-04:221` ↔ `NET-06:248`, and `RENDER-03:279` ↔ `RENDER-07:181`.

Impact: `Requires` currently means a mixture of prerequisite, interface owner, related decision, and future coordination. It cannot topologically order work. Classify hard prerequisite versus interface/reference/validation dependency, and remove hard cycles or state the minimal jointly frozen contract.

### 6. Digest/length vocabulary is almost integrated but still has schema contradictions

- `docs/research/INTEGRATION-RESOLUTIONS.md:18` defines `logical_content_sha256`, `artifact_sha256` + `artifact_length`, `lock_sha256`, and `compiled_cache_key` as distinct purposes.
- `docs/decisions/ASSET-02-manifest-and-overrides.md:228-231` includes both `artifact_length?` and `archive_byte_length?` in the same package record without distinguishing them; both appear to mean literal archive length.
- `docs/decisions/NET-09-client-content-agreement.md:23` still says hash comparison “requires canonical package bytes,” while `:73` correctly defines compatibility identity over the logical map and reserves artifact hash/length for literal bytes.
- The `ContentLock` sketch at `NET-09:50-70` omits a `lock_sha256` field, although `:73` defines that field and says the hash excludes itself.

Impact: generators can emit two incompatible lock schemas from one document. Remove or define the duplicate length and make the lock sketch match its prose.

### 7. Superseded tick-profile prose remains internally contradictory

`docs/decisions/NET-06-tick-and-simulation-rates.md:71-84` clearly labels the 20 Hz section normative and the multi-profile sketch superseded. Retaining the comparison is understandable, but stale statements escape that archival section: `:259` rejects tick-count gameplay delays because worlds would run at different speeds under different profiles, while the normative contract at `:73-80` has one fixed-rate `WorldTick` and explicitly persists `DueWorldTick`.

Impact: a reader scanning “Rejected or deferred alternatives” receives the opposite persistence rule. Move the rejection under the archival boundary or rewrite it as historical rationale; keep one current rule in the Proposed decision.

### 8. The packet claims completeness while linking to a missing required review

`docs/READ_ME_FIRST.md:3` says the pre-implementation research pass is complete, and `:13` includes `research/REVIEW-world-render-storage.md` in the adversarial review set. That file does not exist in the audited snapshot. This is the one confirmed broken local Markdown link.

Impact: both the completeness claim and prescribed review path are false in the current tree. Add the referenced artifact or remove/defer the link and completeness wording.

## P2 — reproducibility and maintenance

### 9. Mutable source links weaken reproducibility

There are 64 occurrences of GitHub `blob/main`, `blob/master`, `tree/main`, `tree/master`, or `blob/dev` links, representing 45 distinct mutable URLs. Several nearby claims include version language such as “current” while their branch targets can change without this packet changing.

Primary-source spot checks found no clearly unsupported nearby claim in the sampled set: SQLite release-history/WAL wording, Godot thread-safe server caveats, WIT package/world syntax, Luanti MapBlock/light layout, Valve GNS ownership/lane behavior, and the cited Minecraft Bedrock rendering slides all supported the limited claims made from them. This is not an exhaustive validation of all 584 external-link occurrences. Pin mutable implementation links to inspected commit SHAs or record the inspected commit/date beside the claim.

### 10. Placeholder language is intentional, not unresolved architecture

The placeholder scan found three matches:

- `docs/prompts/master.md:3,23` uses `[INSERT TOPIC]` as an intentional prompt-template slot.
- `docs/decisions/WORLD-02-chunk-job-scheduling.md:60` quotes an upstream Minestom `TODO` as evidence and immediately explains the VibeCraft boundary.

No actionable local `TODO`, `TBD`, `FIXME`, or placeholder was found outside those contexts.

## Arithmetic review

No impossible arithmetic was found. Checked examples include:

- `27 × 256 KiB = 7,077,888 bytes = 56.623104 Mbit`, requiring about `28.31 s` at `2 Mbit/s` before overhead, as stated in the architecture/network review.
- `(16 × 64)^3 × 2 = 2,147,483,648 bytes = 2 GiB` in the lighting-resolution analysis.
- `18³ × 2 = 11,664 bytes`; multiplied by 4,096 pages this is 47,775,744 bytes, approximately 45.56 MiB.
- A wrapping `uint32` client counter at 20 Hz lasts about 6.81 years; wrapping is therefore plausible, but still requires the documented wrap-comparison contract noted above.

Many performance values remain research fixtures rather than release thresholds; that is a fixture/authority issue, not an arithmetic error.

## Machine-check summary

| Check | Result |
| --- | --- |
| Markdown scope | 58 files; 13,290 lines; 1,172,469 bytes |
| Decision files/headings | 41 files; 41 unique IDs; 0 duplicate IDs; 0 malformed headings; 0 filename/heading mismatches |
| Decision references | 1,304 ID occurrences; 0 unknown IDs |
| Decision statuses | 41 `Proposed`; no missing status in a decision brief |
| Markdown links | 232 genuine local-link occurrences and 584 external-link occurrences; 1 broken local path; 0 confirmed missing anchors |
| Link-scanner false positive | `ASSET-01:147` contains regex text `[a-z0-9](?:...)`, not a Markdown link |
| Fenced blocks | 146; 0 unclosed/mismatched fences |
| JSON fences | 10; all 10 parse as JSON; 0 malformed examples |
| Placeholder scan | 3 contextual matches; 0 actionable local placeholders |
| Explicit dependency graph | 179 `Requires` edges; 3 cyclic SCCs; 28 decisions in cycles |
| Mutable GitHub citations | 64 occurrences; 45 distinct branch-relative URLs |
| Impossible arithmetic | 0 found in checked numeric derivations |
| Clearly unsupported sampled citations | 0 found in the primary-source spot sample |

## Correction order

1. Establish document authority and resolve the section-size contradiction.
2. Mark review findings as pre-integration/resolved/unresolved so the recommended reading path has one current verdict.
3. Normalize coordinate, tick, and revision domains before freezing wire/storage schemas.
4. Split hard prerequisites from reference/validation dependencies and make the hard graph orderable.
5. Repair the lock schema/digest wording and isolate the NET-06 archival material.
6. Repair the missing review link, then pin mutable implementation citations as decisions are greenlit.
