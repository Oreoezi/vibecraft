# G1/E1 core-data disposition

Issue: #9 — G1/E1 execute benchmark and record disposition  
Fixture: `VC-G1-E1-SECTIONS-0.1.0`  
Logical projection fixture: `VC-G1-E1-LOGICAL-PROJECTION-0.1.0`  
G0 fixture: `VC-G0-FP-0.1.0`  
Evidence seed: `0x5643424654314531`

## Outcome

`defer`

This is the only categorical outcome recorded by this document. It selects neither
16³ nor 32³ sections and authorizes no compatibility freeze. The available run is
an integration-oriented CI smoke observation, not the full accepted benchmark.
Moreover, the owner has not accepted the G0 host, runtime, GC mode, power mode, or
applicable product budgets. The issue protocol makes that acceptance a prerequisite
for a performance decision regardless of what provisional measurements suggest.

## Evidence represented

The retained evidence is:

- [machine-readable observation](../../../artifacts/g1/e1/observational-ci/e1-core-data-observation.json)
- [human-readable observation](../../../artifacts/g1/e1/observational-ci/e1-core-data-observation.md)
- [bounded raw observations](../../../artifacts/g1/e1/observational-ci/e1-core-data-raw.ndjson)

Only the `ci` profile has run. It completes 8 canonical 32³ cubes, one paired
round, one fresh-process memory trial per mode and distribution, two deterministic
edit clusters per measured cube and trace (16 per trace in total), and 100
bootstrap resamples. The generated artifact, rather than this narrative, is
authoritative for its exact source commit/hash, binary hashes, host, runtime, GC,
power, timestamps, evidence counts, and raw NDJSON digest.

The accepted full profile is **not represented**. No run has supplied its fixed
12,500-cube corpus, 256 stratified performance cubes, six alternating paired rounds,
nine fresh-process memory trials per mode/distribution, 256 clusters per trace, or
10,000 bootstrap resamples. The CI
numbers must therefore remain observations and must not be promoted into a section
side or adaptive-container decision.

## Criterion status

| Criterion | Status | Evidence and consequence |
| --- | --- | --- |
| Full accepted profile | Inconclusive | Only the bounded CI smoke profile ran; the 12,500-cube accepted corpus is absent. |
| Semantic and projection correctness | Pass | The report completed equal-volume semantic checks, exhaustive collision-free X→Z→Y indexing for side 16 and side 32, palette-boundary diagnostics, snapshots, and deterministic canonical logical projections without an invariant failure. |
| Warmed read allocations | Pass | Every measured warmed random and linear read in the artifact reported exactly zero thread allocations. |
| Positive measured durations | Pass | Every paired duration was positive; the harness did not synthesize an equality ratio for a zero duration. |
| Adaptive retained memory, one side-32 section | Inconclusive | The smoke has only one fresh-process trial per distribution and is not performance-decision-eligible. It cannot establish the homogeneous/layered/mixed or high-entropy thresholds. |
| Adaptive retained memory, eight side-16 sections | Inconclusive | The smoke has only one fresh-process trial per distribution and is not performance-decision-eligible. It cannot establish the homogeneous/layered/mixed or high-entropy thresholds. |
| Adaptive timing, one side-32 section | Inconclusive | One smoke round cannot establish the predeclared upper-interval limits for reads and clustered edits. |
| Adaptive timing, eight side-16 sections | Inconclusive | One smoke round cannot establish the predeclared upper-interval limits for reads and clustered edits. |
| Side-32 retained-memory rule | Inconclusive | The smoke cannot establish the required upper retained-memory ratio. |
| Side-32 five-category timing rule | Inconclusive | The smoke cannot establish the required count of strong wins or maximum primary upper interval across random read, linear read, clustered edit, snapshot, and logical projection. |
| Side-32 amplification rule | Inconclusive | The reported logical republish and mesh-halo observations are diagnostic only. |
| Side-16 five-category timing rule | Inconclusive | The smoke cannot establish the equal-weight geometric mean or maximum primary upper interval. |
| Side-16 amplification rule | Inconclusive | The reported logical republish and mesh-halo observations are diagnostic only. |
| Real save and network amplification | Inconclusive | No G1 save format or network encoding exists to measure. Logical-projection bytes are a representation-neutral republish proxy, not storage or wire bytes. |
| G0 owner acceptance | Blocked | `VC-G0-FP-0.1.0` remains provisional. There is no owner acceptance for this host, runtime, GC mode, power mode, or the applicable product budgets. |

The protocol thresholds remain test rules, not achieved claims: adaptive
homogeneous/layered/mixed retained memory is compared with 50% of dense,
high-entropy retained memory with 110% of dense, and adaptive timing upper intervals
with 1.15. Side 32 can overturn the side-16 prior only under its 0.80 memory rule,
three-of-five 0.80 timing rule, 1.15 primary maximum, and 2× p95 amplification
rule. Otherwise side 16 still requires its 1.15 equal-weight geometric mean, 1.25
primary maximum, and 2× p95 amplification rule. An initially threshold-crossing
interval adds four paired rounds. The CI profile evaluates none of these rules to a
selection.

## Evidence limitations and statistical unit

One raw timing sample is a same-cube, same-round, ordered candidate/baseline pair.
Candidate order alternates. Decision summaries first take one median per cube over
that cube's rounds and grouped traces, then bootstrap cube-level units; repeated
measurements of one cube are not treated as independent. No raw outlier is removed.
With only one CI round, the resulting intervals are useful for detecting broken
plumbing, not for estimating product performance.

Read timing uses `Stopwatch.GetTimestamp` after four unmeasured warmups. Thread
allocation uses a separate checksum-matched probe after four additional warmups.
The report relaunches itself in a fresh process with tiered compilation and tiered
PGO disabled, records that JIT configuration, and passes it to memory children; this
prevents runtime recompilation bookkeeping from being misreported as a data-path
allocation.
Retained-memory observations use fresh child processes, retained roots, explicit
full compacting collections before and after allocation, and deterministic rotating
mode order, but this smoke contains only one trial per mode/distribution. Known
logical payload bytes and managed retained-memory deltas are distinct quantities.

All timing comparisons apply only to the captured machine, Release binary, runtime,
GC configuration, process affinity, and power state. This is one Linux observation,
not cross-platform performance evidence. The dirty working tree also prevents the
source state from being identified by the recorded commit alone; the artifact's
source and diff hashes are required to identify it.

The logical projection fixture verifies deterministic, representation-neutral
semantics. Its byte count is not a save payload, packet payload, compression result,
transport envelope, retransmission cost, database record, durability unit, or public
wire contract. The halo counts are a remesh-work proxy, not an end-to-end renderer
measurement. Consequently this run does not satisfy real save/network amplification
evidence and cannot be relabeled to appear to do so.

## Constants and contracts left provisional

The fixture IDs and seed identify this experiment; they do not freeze a user-facing
format. The following remain provisional:

- `SectionSide`, including the choice between one 32³ section and eight 16³
  sections for the same semantic volume;
- `LocalBlock` and `LocalIndex` widths and the candidate X→Z→Y local indexing
  order, despite the smoke's correctness validation;
- adaptive `Uniform | Paletted | Direct` representation policy, palette growth and
  direct-mode cutoffs, and mutable hot-path implementation details;
- `SectionKey` canonical byte ordering and codec;
- the persistent `BlockStateId` distinction and registry encoding;
- `SectionRevision` representation at the compatibility boundary;
- persistence layouts, save records, network DTOs, compression, framing, and every
  storage or wire codec.

No CLR type, object graph, in-memory container, raw memory layout, or logical
projection is promoted to a persistence or wire format. No user world may rely on
these candidates before the required gate is accepted.

## Rejected alternatives

- **Freeze side 16 from the WORLD-01 architectural prior.** Rejected for this gate:
  a prior is not a substitute for the full paired corpus, G0 owner acceptance, and
  real save/network evidence.
- **Freeze side 32 from its lower section-object count.** Rejected: object count
  alone does not satisfy the paired retained-memory, timing, snapshot/projection,
  and amplification rules.
- **Invent placeholder save or network encodings for this report.** Rejected: doing
  so would conflate the representation-neutral logical fixture with formats owned
  by later gates and would create accidental compatibility surface.
- **Treat the CI smoke's one-round intervals or one-trial memory values as a
  benchmark decision.** Rejected: the profile intentionally validates integration
  and invariants only and is not statistically or procedurally decision-eligible.

## Reproduction

Run from the repository root with the .NET 10 SDK. The following reproduces the
bounded profile and output location represented here:

```bash
dotnet restore VibeCraft.slnx --locked-mode
dotnet build VibeCraft.slnx --configuration Release --no-restore
dotnet benchmarks/VibeCraft.G1.Benchmarks/bin/Release/net10.0/VibeCraft.G1.Benchmarks.dll \
  --e1-report \
  --profile ci \
  --output artifacts/g1/e1/observational-ci
```

The decision-eligible profile command is deliberately recorded but has **not** been
run for this disposition:

```bash
dotnet benchmarks/VibeCraft.G1.Benchmarks/bin/Release/net10.0/VibeCraft.G1.Benchmarks.dll \
  --e1-report \
  --profile full \
  --output artifacts/g1/e1/full
```

The executable records its exact command, binary hashes, source identity, runtime,
SDK, OS, CPU, memory, power mode, GC mode, affinity, timestamps, and raw observations
in `e1-core-data-observation.json`. A rerun must retain both generated files without
editing them.

## Next actions

Before a rerun can support a selection:

1. The owner must fill and accept `VC-G0-FP-0.1.0` (or a versioned successor) for
   the benchmark host, .NET runtime, GC mode, power mode, and applicable load,
   remesh, save, and network budgets.
2. Commit the benchmark harness and run from a clean tree so commit and binary
   hashes identify the exact implementation without relying on an uncommitted diff.
3. Run and retain the fixed full profile: fingerprint all 12,500 equal-volume
   canonical cubes; measure 256 deterministic round-robin performance cubes with
   one edit cluster each per trace; execute six alternating paired rounds, nine
   fresh-process memory trials per mode/distribution, and 10,000 cube-level
   bootstrap resamples; add four rounds whenever an initial applicable interval
   crosses its threshold.
4. Retain the capped, hashed raw NDJSON observations and verify semantic fingerprints, exhaustive X→Z→Y
   indexing, palette boundaries, warmed zero-allocation reads, snapshot semantics,
   and canonical logical projections on each declared platform; do not discard
   failed attempts or outliers.
5. Define actual bounded save and network schemas at their owning gates, then
   measure their encoded bytes and end-to-end amplification separately from the
   logical projection proxy. Record compression, framing, retransmission, and
   durability assumptions where applicable.
6. Apply the predeclared criteria without changing thresholds after seeing the
   data, preserve any ambiguity, and obtain the required owner acceptance before
   changing the compatibility ledger.

Issue #10 may freeze nothing on this evidence. It may prepare or review gate
materials, but it must leave every G1 compatibility entry provisional until the
missing full evidence and owner acceptance exist.
