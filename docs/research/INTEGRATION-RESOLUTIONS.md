# Integration resolutions

Status: Working synthesis after adversarial review  
Date: 2026-08-09

This log records how cross-brief contradictions were resolved. It does not greenlight unrun prototypes.

| Conflict | Resolution | Remaining gate |
| --- | --- | --- |
| 20 Hz world versus 32/64/128 Hz master simulation | `WORLD-08` owns one 20 Hz v1 `WorldTick`. Packet/render rates are independent. Only a failed 20 Hz movement test unlocks one exactly nested 40 Hz controller branch. The old NET-06 profile sketch is explicitly archival comparison. | Movement/capacity prototype and owner approval |
| Child-process singleplayer versus in-process hosting | One host-agnostic `ServerCore`; child-loopback and embedded adapters must produce equivalent protocol/authority traces. The packaged target-platform comparison selects the default. | Host/lifecycle/package gate |
| GNS written as a choice versus missing trust/packaging evidence | The transport interface and traffic semantics are greenlight candidates. GNS is only the first candidate; public direct IP waits for authenticated server identity/channel binding, admission, native lifetime, lane/backpressure, and packaging tests. | Transport/trust gate |
| Actions inside input frames versus reliable control commands | Movement-sensitive world intents carry `ActionId`, reference an included input sequence, and repeat in bounded unreliable input bundles until a reliable result acknowledges them. Inventory/chat/admin use control. Cross-lane arrival order is never causality. | Protocol fixture/fault tests |
| Four seconds of section COW collision history and 500 ms wait | V1 uses confirmed collision plus a nearby cell-change journal bounded by duration and bytes; a missing dependency resets immediately to authority. Speculative block collision is off. | Journal representation/cap measurement |
| Combat rewind and support-loss grace as mandatory lag compensation | V1 greenlights current-time idempotent block actions only. Combat rewind and support grace are independent, off-by-default experiments; neither blocks movement or transport. | Later gameplay A/B tests |
| “Content hash proves possession” | A cooperating client locally verifies and declares `lock_sha256` on an authenticated transcript. This is compatibility/local integrity, never hostile-client attestation. | Authenticated session plus pack fixtures |
| `.vcpak` as inert art versus one package containing data/Wasm/native code | `.vcpak`/`pack.json` is resource-only. Future `data_pack`, `.vcmod` sandbox component, and native-plugin directory are distinct artifact classes, manifests, parsers, and trust UI. Cross-kind overrides/embedding are forbidden. | Resource pack gate; later separate mod/data designs |
| Logical package equality versus literal artifact bytes | Use `logical_content_sha256`, `artifact_sha256` + `artifact_length`, `lock_sha256`, and `compiled_cache_key` for four different purposes. V1 locks use RFC 8785 canonical JSON. | Cross-platform canonical fixtures |
| ASSET-03 versus ASSET-04 GLB/animation contracts | ASSET-03 is normative: GLB only, one embedded BIN, no URIs/images, material names as slot labels, strict compiled plain data, initially STEP/LINEAR TRS. ASSET-04 owns texture animation/separation only; conflicting GLB clauses are superseded. | Importer/backend conformance gate |
| Optional WIT imports left unlinked | Use an exact versioned WIT package plus an always-linked capability broker returning typed grants/denials, or a small finite set of complete worlds. Capability ID and selected version are separate. | Wasm host/toolchain spike |
| Package ID used as storage/security principal | `PackageId` is a dependency name. A host-created `PrincipalId` records component/side and approved update lineage; unsigned replacement inherits storage only with explicit approval. Server-scoped storage uses authenticated server identity. | Install/update/storage tests |
| Wasm runtime quotas omitted hostile compilation/cache | Accept standard Wasm/component bytes only; bound validation/compilation (prefer a killable helper unless equivalent containment is proven); never native-deserialize package bytes; private caches are locally produced, keyed, protected, and invalidated on runtime upgrades. | Hostile compile/cache corpus |
| Staged plugin commands described as automatically atomic | Validate the entire batch against one immutable revision, reserve all resources, and use non-failing commit primitives before live mutation. Database transactions persist the committed revision; they do not roll back already-mutated memory. | Server plugin transaction prototype |
| “Unlimited height” versus finite computing | Save/addressing is sparse 3D with signed coordinates and no small baked-in height stack. Dimensions still have explicit build/generation/interest borders and bounded jobs. | Core coordinate/section benchmark |
| Per-1/64-block lighting ambiguity | World/gameplay light is block-scale. 64×64 texture detail receives per-fragment shading. A persistent 64³ light field per block is rejected. | Later lighting-page/material benchmark |
| Far LoD and volumetrics treated as first-playable renderer needs | V1 uses finite full-detail terrain and ordinary fog. 3D far summaries, volumetric fog, GI, advanced transparency/refraction, and procedural visuals are later experiments. | Post-slice advanced-visual gate |
| Canonical world key/revision/store metadata drift | Canonical addressing is dimension plus signed-64-bit 3D `SectionKey`; persisted revisions are checked nonnegative signed `long`; the transactional world store owns authoritative metadata; a section payload groups terrain, owned ticks, and block entities at one revision. | World/render/storage review and storage prototype |
| Original sheet versus integrated proposal authority | Owner decisions are highest authority; the proposed requirements and integration log govern prototype derivation; individual briefs supply owned detail; dated reviews are snapshot evidence; the untouched source sheet is the original vision/hypothesis input. | Owner greenlight card |
| `SectionSide = 16` written as frozen while E1 compares 16³/32³ | Signed-64 coordinate and identity semantics are fixed as a proposal; section side, local indexing, and codec remain prototype-profile candidates until E1, then freeze before any user world. | E1 core-data benchmark |
| Coordinate/tick/revision type drift | World identity uses signed-64 `BlockCoord`/`SectionCoord` plus `DimensionId`; render-local narrowing is checked and noncanonical. `WorldTick` is unsigned-64 authority time; client prediction ordering is wrapping unsigned-32; `SectionRevision` is checked nonnegative signed-64; other revision domains are named and nonconvertible. | G1 contract tests and schema fixtures |
| Cyclic `Requires` graph | A brief's `Requires` list mixes hard, reference, validation, and future coordination links. [`DEPENDENCY-MAP.md`](DEPENDENCY-MAP.md) is normative for G0–G5 scheduling and defines an acyclic hard graph; whole briefs are not wholesale prerequisites. | Owner approves G0, then each gate records disposition |
| SQLite recommendation versus conditional durability | The `IWorldStore`, single-writer, atomic-group, receipt, fail-closed, and backup contracts are greenlight candidates. SQLite WAL/FULL remains the first experiment backend; release requires exact patched native-build provenance and target OS/filesystem fault tests. `NORMAL` never emits the same durable receipt. | E2 persistence and packaging campaign |
| Raw render revision counters | Mesh publication compares one typed `MeshInputStamp`: signed `SectionRevision` plus distinct light, render-registry, LoD-source, and materialization-epoch domains. Exact whole-stamp equality is required; no raw cross-domain casts. | G4B stale-result and teardown tests |

## Still open by design

- Product envelope: target platforms/hardware, player/stress load, view radii, network envelope, exposure model, and autosave rollback promise.
- Section side and exact in-memory palette representation.
- SQLite versus a measured fallback if its durability/performance gate fails.
- GNS versus one focused alternative if its trust/native gate fails.
- Child versus embedded singleplayer default.
- Hidden-face versus greedy meshing and Godot renderer backend.
- Whether the Wasm host can meet the promised sandbox on every supported platform.
- Generator stage vocabulary, structure scope, and advanced-visual budgets after the first playable.

The owner-facing dispositions are in [`GREENLIGHT-CHECKLIST.md`](GREENLIGHT-CHECKLIST.md); implementation sequencing is in [`REVIEW-product-scope-and-sequencing.md`](REVIEW-product-scope-and-sequencing.md).
