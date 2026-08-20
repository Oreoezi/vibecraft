# G1/E1 revision-1 protocol

Issue: #30 — version E1 and remediate adaptive section performance

Protocol: `VC-G1-E1-PROTOCOL-1.0.0`

Report schema: `vibecraft.g1.e1.diagnostic.v2`

Status: **predeclared diagnostic protocol; not decision-eligible**

This revision answers the G1-owned section question without inventing work for a
dependent gate. G2 owns persistence projection and G4A owns transport framing, so
actual save throughput, packet throughput, compression, retransmission, and wire
bytes are absent from E1-r1. Canonical logical values and projection length/digest
remain storage-neutral diagnostics. Dirty-section and unique/gross halo counts
remain G1-owned remesh-input diagnostics.

No E1-r1 run may select `SectionSide`, select a container, or freeze a compatibility
surface. Issue #31 may perform that decision only after the owner explicitly accepts
the applicable G0 fixture, benchmark host, runtime, GC mode, power mode, and product
budgets.

## Versioned identities

| Purpose | Identity |
| --- | --- |
| G0 fixture | `VC-G0-FP-0.1.0` — still provisional |
| E1-r1 protocol | `VC-G1-E1-PROTOCOL-1.0.0` |
| Section corpus/workload fixture | `VC-G1-E1-SECTIONS-1.0.0` |
| Semantic fingerprint domain | `VC-G1-E1-SEMANTIC-FP-1.0.0` |
| Corpus fingerprint domain | `VC-G1-E1-CORPUS-FP-1.0.0` |
| Storage-neutral projection fixture | `VC-G1-E1-LOGICAL-PROJECTION-0.1.0` |
| Deterministic seed | `0x5643424654314531` |

These identities identify ephemeral evidence. They are not persistence IDs, wire
versions, world formats, or user compatibility promises.

## Workloads and addressing

Every candidate represents the same canonical 32³ semantic cube as either one 32³
section or eight 16³ sections. The fixed distributions are homogeneous (alternating
air/stone by ordinal), layered, mixed, and high entropy. Palette boundaries 1, 2, 3,
4, 5, 8, 9, 16, 17, 32, 33, 64, 65, 128, 129, 256, and 257 remain correctness
diagnostics.

Measured work comprises:

- equal-volume fresh-process retained memory per distribution;
- random and linear `BlockStateId` reads;
- deterministic interior and boundary 4×4×4 edit clusters containing no-ops,
  existing-state changes, and newly seen states;
- immutable snapshot creation, with full semantic reconstruction outside timing;
- storage-neutral canonical logical projection length/digest and logical values
  republished for dirty sections; and
- unique and gross remesh-halo input samples per edit window.

E1-r1 changes the measured read/edit question from E1 revision 0. Global fixture
positions are converted to a fixture-internal section-array slot and the named
`LocalIndex` domain before a timed interval. Dense and adaptive candidates consume
the same addressed trace and mirrored section layout. The prior E1 ratios included
global coordinate decomposition, `LocalBlock` construction/validation, and section
routing only on the adaptive side; therefore revision-0 and revision-1 timing ratios
are not directly comparable.

## Fixed profiles

| Setting | CI diagnostic | Full diagnostic |
| --- | ---: | ---: |
| Corpus cubes | 8 | 12,500 |
| Performance cubes | 8 | 256 |
| Initial paired rounds | 1 | 6 |
| Additional rounds after an interval crosses a threshold | 0 | 4 |
| Memory trials per mode/distribution | 1 | 9 |
| Edit clusters per measured cube/trace | 2 | 1 |
| Total edit clusters per trace | 16 | 256 |
| Bootstrap resamples | 100 | 10,000 |
| Default managed-memory safety ceiling | 256 MiB | 4 GiB |

The CI profile validates integration and deterministic shape only. A full run under
issue #30 is still diagnostic. The memory ceiling is a captured safety bound, not a
performance threshold; reducing the requested corpus is forbidden by the fixed
profile parser.

Broad read-path warmup is 64 repetitions over uniform, paletted, and direct
representations. Each read timing probe receives four further warmups. Its separate,
checksum-matched allocation probe receives four further warmups. Snapshot,
projection, edit, and retained-memory corpus paths each receive one unmeasured
execution. The runner uses Release binaries, `Stopwatch.GetTimestamp`,
`GC.GetAllocatedBytesForCurrentThread`, and a fresh process with
`DOTNET_TieredCompilation=0` and `DOTNET_TieredPGO=0`. The report child validates
all three runtime controls before opening a staging directory, and the manifest
captures the observed assembly configuration and both environment values.

## Statistical and invalid-sample policy

One raw timing unit is one same-cube, same-round ordered candidate/reference pair;
order alternates by round. Summaries first reduce repeated observations to one median
per cube and then bootstrap cube-level units. No raw timing or amplification outlier
is removed. A nonpositive duration remains in raw evidence, gets no synthesized
ratio, and makes its diagnostic inconclusive. A failed or negative retained-memory
trial remains in raw evidence, is excluded from ratios, and makes its affected
criterion inconclusive.

Host metadata must include OS, architecture, CPU/count, affinity, machine model,
physical and managed memory, power mode, runtime/SDK, GC mode/latency, source-tree
identity, diff identity, and benchmark binary identities. Cross-host timing values
are never combined.

## Frozen diagnostic thresholds

The following rules are retained unchanged for the later owner-accepted run:

- homogeneous/layered/mixed adaptive retained-memory upper95 ≤ 0.50 of dense;
- high-entropy adaptive retained-memory upper95 ≤ 1.10 of dense;
- addressed adaptive/dense random-read, linear-read, and clustered-edit upper95
  ≤ 1.15, with warmed reads exactly 0 allocated bytes;
- side 32 may overturn the side-16 prior only with retained-memory upper95 ≤ 0.80,
  at least three of five retained-memory/grouped-read/grouped-edit/snapshot/projection
  primary upper95 values ≤ 0.80, no primary upper95 > 1.15, and
  logical/unique-halo p95 amplification ≤ 2.0;
- otherwise side 16 still requires an equal-weight primary geometric mean ≤ 1.15,
  no primary upper95 > 1.25, and logical/unique-halo p95 amplification ≤ 2.0.

Thresholds cannot move in response to diagnostic results. Ambiguity, missing owner
acceptance, missing metadata, invalid evidence, or a failed prerequisite yields
`defer`, never an inferred selection.

An output path names the whole evidence set and must not already exist. The runner
writes all three artifacts into a unique sibling staging directory, then publishes
the complete set with one same-filesystem directory rename. A failed run removes its
unpublished staging directory; a completed evidence set is never overwritten.

## Immutable failed baseline

The retained E1 revision-0 observation came from clean commit
`bc8117549935cf74d6fa3870e4364bfc05ee24ff`. It remains untouched:

| Artifact | SHA-256 |
| --- | --- |
| `artifacts/g1/e1/full-observational/e1-core-data-observation.json` | `239f7fcf0423de12f156f73fabaaa6bfda34dbc185988336dab9ed424ee99d06` |
| `artifacts/g1/e1/full-observational/e1-core-data-observation.md` | `971205d601e1dc4c0fcfacb8b47a014c795ecb8542eaa026c6e5205456ad8ab1` |
| `artifacts/g1/e1/full-observational/e1-core-data-raw.ndjson` | `2028eacc44cc9c9b3e45bd3b96d0235d7c7bd3f5959ead230ba31deb0131eb5f` |

E1-r1 atomically publishes a new evidence-set directory and refuses to overwrite an
existing path. Diagnostic evidence belongs under `artifacts/g1/e1-r1/diagnostic/`;
a future accepted decision run must use a separate issue-31 path.
