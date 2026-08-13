# G1/E1 core-data disposition

Issue: #9 — G1/E1 execute benchmark and record disposition  
Fixture: `VC-G1-E1-SECTIONS-0.1.0`  
Logical projection fixture: `VC-G1-E1-LOGICAL-PROJECTION-0.1.0`  
G0 fixture: `VC-G0-FP-0.1.0`  
Evidence seed: `0x5643424654314531`

## Outcome

`defer`

This is the only categorical outcome recorded by this document. It selects neither
16³ nor 32³ sections and authorizes no compatibility freeze. The fixed full
observational profile completed, but neither candidate satisfies every predeclared
prerequisite. The owner has not accepted the G0 host, runtime, GC mode, power mode,
or applicable product budgets, and actual save/network formats do not yet exist to
measure. Either absence prevents a compatibility freeze.

## Evidence represented

The retained full-profile evidence is:

- [machine-readable observation](../../../artifacts/g1/e1/full-observational/e1-core-data-observation.json)
- [human-readable observation](../../../artifacts/g1/e1/full-observational/e1-core-data-observation.md)
- [bounded raw observations](../../../artifacts/g1/e1/full-observational/e1-core-data-raw.ndjson)

The `full` profile ran from clean commit
`f84544935abf98cc066356002188cf89e114eb0b`. It fingerprinted all 12,500 canonical
32³ cubes; measured 256 deterministic round-robin cubes over six alternating paired
rounds; executed 256 interior and 256 boundary clusters per layout; completed nine
fresh-process trials for each of three representations and four distributions (108
trials); and performed 10,000 bootstrap resamples over cube-level units. It started
at `2026-08-13T19:08:05Z` and completed at `2026-08-13T19:29:22Z`.

The run used Release binaries on Fedora Linux 43, .NET 10.0.11 / SDK 10.0.400, an
AMD Ryzen 7 5800U, workstation GC, `platform-profile=balanced`, and
`cpu-governor=powersave`. The source-tree SHA-256 is
`142f0283c509438095ca7eeca5153603ef81f6a9bf32eef19ba3fa10e06dfc98`;
the benchmark-assembly SHA-256 is
`5ff11a01e9be790863c31d5ffe4cebee5ca6751e4d61afe6484d6d71fb397031`.
The raw artifact contains 44,032 observations / 21,012,284 bytes and hashes to
`b64ec2eccff4be3c47b0a973da09af7fbcff4017e761bacd6b998d02299788a3`.

## Criterion status

| Criterion | Status | Evidence and consequence |
| --- | --- | --- |
| Full accepted profile | Pass | The fixed 12,500-cube corpus, 256 measured cubes/clusters per trace, six rounds, nine trials per mode/distribution, and 10,000 resamples completed from a clean commit. |
| Semantic and projection correctness | Pass | The report completed equal-volume semantic checks, exhaustive collision-free X→Z→Y indexing for side 16 and side 32, palette-boundary diagnostics, snapshots, and deterministic canonical logical projections without an invariant failure. |
| Warmed read allocations | Pass | Every measured warmed random and linear read in the artifact reported exactly zero thread allocations. |
| Positive measured durations | Pass | Every paired duration was positive; the harness did not synthesize an equality ratio for a zero duration. |
| Adaptive retained memory, one side-32 section | Pass | Equal-weight homogeneous/layered/mixed upper95 is 0.090005 (limit 0.50); high-entropy upper95 is 1.000793 (limit 1.10). |
| Adaptive retained memory, eight side-16 sections | Pass | Equal-weight homogeneous/layered/mixed upper95 is 0.120902 (limit 0.50); high-entropy upper95 is 1.006346 (limit 1.10). |
| Adaptive timing, one side-32 section | Fail | Maximum upper95 adaptive/dense ratio across read/edit categories is 273.944647 (limit 1.15). |
| Adaptive timing, eight side-16 sections | Fail | Maximum upper95 adaptive/dense ratio across read/edit categories is 69.232238 (limit 1.15). |
| Side-32 retained-memory rule | Fail | Balanced upper95 side32/side16 retained-memory ratio is 0.928240 (limit 0.80). |
| Side-32 five-primary rule | Fail | Zero of memory/read/edit/snapshot/projection upper intervals are at most 0.80; maximum upper95 is 4.691369 (limit 1.15). |
| Side-32 amplification rule | Fail | Maximum p95 candidate/reference ratio across canonical logical bytes and unique halo samples is 8.079583 (limit 2.0). |
| Side-16 five-primary rule | Pass | Equal-weight geometric mean of memory/read/edit/snapshot/projection medians is 0.804959 (limit 1.15); maximum upper95 is 1.241194 (limit 1.25). |
| Side-16 amplification rule | Pass | Maximum p95 candidate/reference ratio across canonical logical bytes and unique halo samples is 1.000276 (limit 2.0). |
| Real save and network amplification | Inconclusive | No G1 save format or network encoding exists to measure. Logical-projection bytes are a representation-neutral republish proxy, not storage or wire bytes. |
| G0 owner acceptance | Blocked | `VC-G0-FP-0.1.0` remains provisional. There is no owner acceptance for this host, runtime, GC mode, power mode, or the applicable product budgets. |

The protocol thresholds remain test rules, not achieved claims: adaptive
homogeneous/layered/mixed retained memory is compared with 50% of dense,
high-entropy retained memory with 110% of dense, and adaptive timing upper intervals
with 1.15. Side 32 can overturn the side-16 prior only under its 0.80 memory rule,
three-of-five 0.80 timing rule, 1.15 primary maximum, and 2× p95 amplification
rule. Otherwise side 16 still requires its 1.15 equal-weight geometric mean, 1.25
primary maximum, and 2× p95 amplification rule. An initially threshold-crossing
interval adds four paired rounds; none crossed an applicable threshold here, so the
six fixed rounds were retained. Side 16 passes its relative primary/proxy rule, but
its adaptive hot representation misses the dense timing ceiling and the actual
save/network criterion remains unavailable. No candidate is selected.

## Evidence limitations and statistical unit

One raw timing sample is a same-cube, same-round, ordered candidate/baseline pair.
Candidate order alternates. Decision summaries first take one median per cube over
that cube's rounds and grouped traces, then bootstrap cube-level units; repeated
measurements of one cube are not treated as independent. No raw outlier is removed.
The full report contains both raw-pair counts and the number of independent
cube/trial units used by each interval.

Read timing uses `Stopwatch.GetTimestamp` after four unmeasured warmups. Thread
allocation uses a separate checksum-matched probe after four additional warmups.
The report relaunches itself in a fresh process with tiered compilation and tiered
PGO disabled, records that JIT configuration, and passes it to memory children; this
prevents runtime recompilation bookkeeping from being misreported as a data-path
allocation. Retained-memory observations use fresh child processes, retained roots,
explicit full compacting collections before and after allocation, and deterministic
rotating mode order. All 108 full-profile trials were valid. Known logical payload
bytes and managed retained-memory deltas are distinct quantities.

All timing comparisons apply only to the captured machine, Release binary, runtime,
GC configuration, process affinity, and power state. This is one Linux observation,
not cross-platform performance evidence. This run used a clean tree, so its commit,
source hash, and binary hashes identify the implementation; it remains one-host
observational evidence because G0 acceptance is absent.

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
  order, despite the full profile's correctness validation;
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
full profile and output location represented here:

```bash
dotnet restore VibeCraft.slnx --locked-mode
dotnet build VibeCraft.slnx --configuration Release --no-restore
dotnet benchmarks/VibeCraft.G1.Benchmarks/bin/Release/net10.0/VibeCraft.G1.Benchmarks.dll \
  --e1-report \
  --profile full \
  --output artifacts/g1/e1/full-observational
```

The executable records its exact command, binary hashes, source identity, runtime,
SDK, OS, CPU, memory, power mode, GC mode, affinity, timestamps, and raw observations
in `e1-core-data-observation.json`. A rerun must retain the JSON summary, Markdown
summary, and hashed raw NDJSON without editing them.

## Next actions

Before any follow-up evidence can support a selection:

1. The owner must fill and accept `VC-G0-FP-0.1.0` (or a versioned successor) for
   the benchmark host, .NET runtime, GC mode, power mode, and applicable load,
   remesh, save, and network budgets.
2. Define actual bounded save and network schemas at their owning gates, then
   measure encoded bytes and end-to-end amplification separately from the logical
   projection proxy. Record compression, framing, retransmission, and durability
   assumptions where applicable.
3. Address or explicitly revise the adaptive-vs-dense timing requirement. Both
   candidate adaptive containers missed its 1.15 upper bound by large margins; do
   not select side16 merely because its relative five-primary rule passed.
4. Retain the capped, hashed raw NDJSON observations and verify semantic
   fingerprints, exhaustive X→Z→Y indexing, palette boundaries, warmed
   zero-allocation reads, snapshot semantics, and canonical logical projections on
   each declared platform; do not discard failed attempts or outliers.
5. Apply the predeclared criteria without changing thresholds after seeing the
   data, preserve any ambiguity, and obtain the required owner acceptance before
   changing the compatibility ledger.

Issue #10 may freeze nothing on this evidence. It may prepare or review gate
materials, but it must leave every G1 compatibility entry provisional until the
missing save/network evidence, adaptive timing disposition, and owner acceptance
exist.
