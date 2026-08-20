# G1/E1-r1 adaptive remediation

Issue: #30

Status: **implemented candidate revision; diagnostic only; no selection**

## Revision-0 failures and concrete causes

The immutable baseline reported adaptive timing upper intervals of `273.196319` for
one side-32 section and `68.621421` for eight side-16 sections against the fixed
`1.15` ceiling. It also reported a balanced side32/side16 retained-memory upper ratio
of `0.928240` against `0.80`.

Those observations exposed three separate causes:

1. The read comparison was not address-equivalent. Adaptive reads decomposed a
   global linear position with division/remainder, routed a section, constructed and
   validated `LocalBlock`, recomputed a local index, and dispatched through container
   abstractions. Dense read timing indexed one flat array. E1-r1 precomputes typed
   `LocalIndex` addresses and mirrors the dense section layout before measurement.
2. Adding a newly seen palette state cloned the palette array, the entire packed
   voxel-index buffer, and the reverse dictionary on every edit. A revision-0
   homogeneous side-32 64-edit window allocated 580,752 bytes, while an already
   direct high-entropy window allocated zero. The revised mutable palette edits
   palette/index/dictionary structures in place while capacity and bit width are
   unchanged; only a bit-width boundary stages replacement structures. Revision
   exhaustion is checked before mutation.
3. The side-32 memory miss is spatial granularity, not dictionary overhead. The
   deterministic known payloads are:

   | Distribution | One 32³ | Eight 16³ |
   | --- | ---: | ---: |
   | Homogeneous | 4 B | 32 B |
   | Layered | 8,208 B | 4,176 B |
   | Mixed | 24,832 B | 26,624 B |
   | High entropy | 131,072 B | 131,072 B |

   In the layered cube, four 16³ octants are uniform and need no packed index
   payload, while one 32³ palette indexes the full cube. Object-header or dictionary
   trimming cannot credibly close the `0.928240` to `0.80` gap. Passing that rule
   would require a separately evaluated within-section spatial representation such
   as uniform pages or runs. Issue #30 does not silently introduce one.

## Candidate changes

- Internal mutable reads and writes accept the exact `LocalIndex` domain directly;
  `LocalBlock` entry points delegate through `SectionGeometry` for callers that own
  coordinates.
- Equal-volume fixtures build deterministic `(section slot, LocalIndex)` traces and
  mirrored dense layouts outside measured paths.
- Mutable palette growth reuses its storage object and backing structures within one
  bit-width capacity. Boundary transitions stage palette, packed-index, and lookup
  replacements before publishing them.
- Immutable uniform snapshots safely share the immutable uniform storage object.
  High-entropy direct snapshots first prove that the palette limit is exceeded and
  then clone the direct voxel array exactly once. Direct sections that have collapsed
  to ≤256 values retain canonical snapshot compaction.
- Standalone BenchmarkDotNet read/edit/growth diagnostics use `LocalIndex` or the
  same addressed equal-volume traces. Their edit matrix includes homogeneous,
  layered, mixed, and high-entropy behavior.

The public `ISectionBlockStateSnapshot` seam remains semantic-only and unchanged.
No array, palette, dictionary, packed word, storage kind, persistence format, or wire
format escapes it.

## Evidence guardrails

Focused tests cover every palette boundary through direct promotion, randomized
dense/adaptive equivalence, zero-allocation warmed existing-state edits, zero
allocation for a newly seen 3→4 palette entry at sides 16 and 32, atomic revision
exhaustion at that transition, addressed read/edit equivalence, immutable snapshot
ownership/compaction, public-seam zero-allocation reads, and the four known-payload
rows above.

E1-r1 report schema `vibecraft.g1.e1.diagnostic.v2` embeds its structured workload,
warmup, repetitions, thresholds, invalid-sample policy, runtime controls, and host
metadata requirements. Both top-level and manifest classifications are `diagnostic`;
`performanceDecisionEligible` is false; the removed save/network criterion is absent;
and output atomically publishes one complete, never-overwritten evidence directory.

Any E1-r1 numbers are observations on their captured host and revised workload. They
must not be compared numerically with revision-0 timing ratios as though the measured
addressing work were unchanged. No candidate, section side, indexing compatibility
promise, persistence layout, or wire format is selected here.
