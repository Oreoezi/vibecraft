# G1/E1-r1 diagnostic 01

Issue: #30

Disposition: **defer; diagnostic only; no candidate selected**

This is the first full run of `VC-G1-E1-PROTOCOL-1.0.0` after the adaptive
section remediation. It was published from clean source commit
`8670ac2454f8f503a11184b96af6347f63e0a0aa`. G0 owner acceptance is absent, so
neither a passing individual rule nor the side-16 diagnostic result has selection
authority.

## Evidence identity

The evidence set is under
`artifacts/g1/e1-r1/diagnostic/adaptive-remediation-01/`.

| Artifact | SHA-256 |
| --- | --- |
| `e1-r1-core-data-diagnostic.json` | `55093955a779702815e49646fc92b31a9f780efd456403e7df3653644f56a4f2` |
| `e1-r1-core-data-diagnostic.md` | `80d89a8e7296d5f1403ea3ad3a25f5edf33828ce2f9e3857e675594d3ff98d9b` |
| `e1-r1-core-data-raw.ndjson` | `1ad91a3f42f1bec79a5ffd613cbc1e3bffdfe24a152745ef46c0ae8d2e863b0a` |

The raw artifact contains 44,032 newline-delimited observations and its recorded
byte count is 20,976,342. The report completed all 12,500 corpus cubes, 256
performance cubes, six paired rounds, 256 edit clusters per trace, 10,000 bootstrap
resamples, and 108 fresh-process memory trials. Every memory trial was valid; every
measured duration was positive; warmed reads allocated zero bytes; and both semantic
and logical-projection correctness checks passed.

The full corpus fingerprints are:

- one side-32 layout:
  `d7acaa8942bca2e8c66811e5ab85ed77ca136aee7796b72c2f12feaa862804e3`;
- eight side-16 layout:
  `8737251bcb8895e33a6c9b24e365d2818607da14c51ce17fb518b05bf597b17c`.

## Captured host and method

This run is an observation on an AMD Ryzen 7 5800U host with 16 logical processors,
Fedora Linux 43, x64, balanced platform profile, powersave CPU governor, server GC
disabled (workstation GC), and interactive GC latency. It used .NET runtime 10.0.11
and SDK 10.0.400 from a Release assembly with `DOTNET_TieredCompilation=0` and
`DOTNET_TieredPGO=0`. The process affinity was `0-15`; captured physical and managed
memory were 14,480,375,808 bytes. Timing ratios must not be generalized to another
host or compared numerically with revision 0, whose measured addressing work was
different.

## Diagnostic observations

The revised adaptive retained-memory criteria passed on this host. The upper95
adaptive/dense ratios were `0.090005` and `0.120658` for the equal-weight
homogeneous/layered/mixed group at sides 32 and 16, and `1.000793` and `1.006346`
for high entropy. The balanced side32/side16 retained-memory upper ratio was
`0.928737`, still above the unchanged `0.80` rule.

Address-equivalent reads still failed the unchanged adaptive/dense rule. The
random/linear upper95 values were `4.151534`/`4.151678` for side 32 and
`4.725891`/`3.183047` for side 16. Distribution diagnostics show an approximately
`2.18`–`2.66` ratio even for uniform or direct high-entropy reads, after global
coordinate decomposition and section routing were removed from the adaptive side.
That remaining floor is the adaptive storage dispatch, bounds checks, and semantic
decode versus one dense array access. Paletted distributions add packed-index and
palette lookup work; the layered side-16 random-read upper95 reached `8.519936`.

Clustered edits remained the largest adaptive timing failure. Their maximum upper95
adaptive/dense ratios were `718.193683` for side 32 and `329.116594` for side 16.
High-entropy direct edits were close to the dense operation (`1.054508`–`1.195904`
upper95 by side and trace), while homogeneous, layered, and mixed paletted edits
ranged from `34.067232` to `976.787223` by distribution, side, and trace. The
revision removed the former clone on every newly seen state, but each fresh cluster
still crosses several palette bit-width boundaries. Those boundaries repack the
entire section's index words and rebuild the lookup while dense performs a scalar
array write. A side-32 repack covers 32,768 indices; side-16 repacks are spatially
bounded to 4,096 indices. This is the concrete remaining paletted-edit cause.

The same spatial granularity explains the side-32 memory and amplification misses.
A layered side-32 palette indexes the whole 32-cubed section, while four of the eight
side-16 octants remain uniform. For an interior dirty window, one side-32 section
republished 32,768 logical values and sampled a 34-cubed halo, while a localized
side-16 section republished 4,096 values and sampled an 18-cubed halo. The aggregate
side-32 logical/unique-halo p95 amplification ratio was `8.079583`, above `2.0`.

The generated report records side-16 primary and amplification criteria as passing
on this diagnostic host. That is not a selection: the report's overall disposition
is `defer`, `performanceDecisionEligible` is false, G0 acceptance is blocked, and
issue #30 has no authority to freeze a section side or compatibility constant.

## Handoff

The remediation is useful but not threshold-complete. Any later experiment must keep
the E1-r1 thresholds fixed and separately evaluate whether to simplify the adaptive
read path, adopt a different mutable paletted-edit strategy, or introduce a
spatially finer representation such as uniform pages/runs. None of those directions
is greenlit here. A decision run under issue #31 remains forbidden until explicit G0
owner acceptance and a newly bounded implementation issue resolve the remaining
candidate question.
