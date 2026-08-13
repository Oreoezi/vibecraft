# G1/E1 core-data observations

**Disposition: defer.** The predeclared G0 owner acceptance for host, runtime, GC, power, and product budgets is absent; this report cannot freeze compatibility constants.

Fixture set: `VC-G0-FP-0.1.0`, `VC-G1-E1-SECTIONS-0.1.0`, `VC-G1-E1-LOGICAL-PROJECTION-0.1.0`; seed `0x5643424654314531`.

## Protocol

The corpus streams canonical 32-cubed semantic cubes. Ordinal modulo four selects homogeneous (alternating air/stone), layered, mixed, and high-entropy distributions. Every semantic fingerprint hashes the domain, fixture, seed, ordinal, distribution byte, and exactly 32,768 WorldStateId values in X-to-Z-to-Y order. One 32-cubed section and eight 16-cubed sections must match that semantic fingerprint; their logical-projection byte hashes are intentionally not required to match.

This profile fingerprints all 12500 corpus cubes and measures timing/amplification on 256 deterministic round-robin cubes. It applies 1 4x4x4 cluster per measured cube and trace (256 clusters per trace in total), avoiding an unintended corpus-times-cluster cross-product.

Measurements are same-machine paired Stopwatch observations. Orders alternate; raw samples include order, duration, checksum, allocations, and operation counts. One same-cube, same-round candidate/baseline pair is the raw unit. Decision summaries first take one median per cube across that cube's rounds (and traces for a grouped category), then bootstrap those cube-level units so repeated measurements are not treated as independent. No raw outlier is removed. Retained memory, when available, uses fresh child processes per distribution and is explicitly distinct from known logical payload bytes.

## Provisional assessment

Neither candidate satisfies every predeclared metric prerequisite in this observation. Thresholds remain unchanged and no candidate is selected.

Reason for overall defer: G0 owner acceptance of the benchmark host, runtime, GC, power conditions, and applicable product budgets is explicitly absent. The issue protocol therefore requires defer even if any provisional metric rule appears to pass.

| Criterion | Status | Evidence |
| --- | --- | --- |
| fixed-full-profile | pass | The fixed 12,500-cube, six-round, nine-memory-trial, 10,000-resample profile completed. |
| semantic-and-projection-correctness | pass | The report completed exhaustive side16/side32 indexing, equal-volume semantic checks, palette-boundary diagnostics, snapshots, and deterministic canonical logical projections without an invariant failure. |
| zero-allocation-warmed-reads | pass | Every measured warmed random and linear read reported exactly zero thread allocations. |
| positive-measured-durations | pass | Every paired duration was positive; no equality ratio was synthesized. |
| adaptive-memory-one-side32 | pass | homogeneous-layered-mixed upper95=0.090005 (limit 0.50); highentropy upper95=1.000793 (limit 1.10) |
| adaptive-memory-eight-side16 | pass | homogeneous-layered-mixed upper95=0.120902 (limit 0.50); highentropy upper95=1.006346 (limit 1.10) |
| adaptive-timing-one-side32 | fail | Maximum upper95 adaptive/dense ratio across random read, linear read, interior edits, and boundary edits is 273.944647 (limit 1.15). |
| adaptive-timing-eight-side16 | fail | Maximum upper95 adaptive/dense ratio across random read, linear read, interior edits, and boundary edits is 69.232238 (limit 1.15). |
| side32-memory | fail | Balanced upper95 side32/side16 retained-memory ratio is 0.928240 (limit 0.80). |
| side32-primary | fail | 0 of five retained-memory/read/edit/snapshot/projection upper95 side32/side16 ratios are <= 0.80; maximum upper95 is 4.691369 (limit 1.15). |
| side32-amplification | fail | Maximum p95 candidate/reference ratio across canonical logical-projection bytes and unique remesh-halo samples is 8.079583 (limit 2.0). |
| side16-primary | pass | Equal-weight geometric mean of five median side16/side32 ratios is 0.804959 (limit 1.15); maximum upper95 is 1.241194 (limit 1.25). |
| side16-amplification | pass | Maximum p95 candidate/reference ratio across canonical logical-projection bytes and unique remesh-halo samples is 1.000276 (limit 2.0). |
| save-and-network-amplification | inconclusive | No G1 save format or network encoding exists to measure. Canonical logical-projection bytes are retained as a representation-neutral republish proxy and are not relabeled as storage or wire evidence. |
| g0-owner-acceptance | blocked | No owner acceptance exists for this host, runtime, GC mode, power mode, or the applicable G0 product budgets. |

## Metric summaries

| Metric | Raw pairs | Independent units | Median ratio | MAD | 95% bootstrap interval | Definition |

| --- | ---: | ---: | ---: | ---: | ---: | --- |

| adaptive-vs-dense/clustered-edit/boundary/eight-side16 | 1536 | 256 | 65.547190 | 48.493638 | 53.586161–69.232238 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/clustered-edit/boundary/one-side32 | 1536 | 256 | 213.647947 | 181.449228 | 121.793491–271.095617 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/clustered-edit/interior/eight-side16 | 1536 | 256 | 35.994483 | 24.200297 | 21.067881–47.614927 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/clustered-edit/interior/one-side32 | 1536 | 256 | 191.150188 | 175.224449 | 113.041888–273.944647 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/linear-read/eight-side16 | 1536 | 256 | 19.408974 | 0.551989 | 19.282472–19.579878 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/linear-read/one-side32 | 1536 | 256 | 18.975470 | 0.541980 | 18.804413–19.217671 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/random-read/eight-side16 | 1536 | 256 | 20.011714 | 1.190521 | 19.295787–20.664687 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/random-read/one-side32 | 1536 | 256 | 19.085876 | 0.625964 | 18.769581–19.414247 | paired lower-is-better adaptive/dense duration ratio |
| distribution/highentropy/adaptive-vs-dense/clustered-edit/boundary/eight-side16 | 384 | 64 | 2.288541 | 0.169845 | 2.238606–2.407211 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/clustered-edit/boundary/one-side32 | 384 | 64 | 1.761338 | 0.036752 | 1.740721–1.777837 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/clustered-edit/interior/eight-side16 | 384 | 64 | 2.002295 | 0.137181 | 1.973328–2.047202 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/clustered-edit/interior/one-side32 | 384 | 64 | 1.760456 | 0.042117 | 1.740721–1.769375 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/linear-read/eight-side16 | 384 | 64 | 18.733119 | 0.116935 | 18.658257–18.776488 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/linear-read/one-side32 | 384 | 64 | 18.199767 | 0.133281 | 18.138322–18.233247 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/random-read/eight-side16 | 384 | 64 | 18.705166 | 0.176910 | 18.635990–18.772544 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/random-read/one-side32 | 384 | 64 | 18.241540 | 0.112937 | 18.217596–18.308536 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side-reciprocal/clustered-edit/boundary | 384 | 64 | 1.243065 | 0.084108 | 1.208028–1.271438 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side-reciprocal/clustered-edit/interior | 384 | 64 | 1.127953 | 0.056937 | 1.084983–1.145921 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side-reciprocal/linear-read | 384 | 64 | 1.026336 | 0.004139 | 1.023572–1.028095 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side-reciprocal/logical-projection | 384 | 64 | 1.064553 | 0.015987 | 1.058216–1.072205 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side-reciprocal/random-read | 384 | 64 | 1.028901 | 0.004823 | 1.026325–1.030384 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side-reciprocal/snapshot | 384 | 64 | 5.863100 | 0.358490 | 5.628167–6.041159 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side/clustered-edit/boundary | 384 | 64 | 0.807658 | 0.056297 | 0.786854–0.828603 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side/clustered-edit/interior | 384 | 64 | 0.893543 | 0.042937 | 0.872218–0.926962 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side/linear-read | 384 | 64 | 0.974348 | 0.003944 | 0.972877–0.976975 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side/logical-projection | 384 | 64 | 0.939425 | 0.014403 | 0.933300–0.944850 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side/random-read | 384 | 64 | 0.971912 | 0.004650 | 0.970522–0.974364 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side/snapshot | 384 | 64 | 0.171225 | 0.010521 | 0.166111–0.178296 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/clustered-edit/boundary/eight-side16 | 384 | 64 | 177.348414 | 2.367481 | 176.933398–178.808172 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/clustered-edit/boundary/one-side32 | 384 | 64 | 461.094783 | 7.011638 | 458.452884–462.972296 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/clustered-edit/interior/eight-side16 | 384 | 64 | 62.806806 | 0.938453 | 62.291411–63.291470 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/clustered-edit/interior/one-side32 | 384 | 64 | 462.829598 | 7.987969 | 457.926564–467.066475 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/linear-read/eight-side16 | 384 | 64 | 19.188821 | 0.102304 | 19.140663–19.259629 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/linear-read/one-side32 | 384 | 64 | 18.745100 | 0.102952 | 18.666877–18.785552 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/random-read/eight-side16 | 384 | 64 | 19.156973 | 0.133408 | 19.099659–19.215637 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/random-read/one-side32 | 384 | 64 | 18.694209 | 0.073329 | 18.654264–18.715007 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side-reciprocal/clustered-edit/boundary | 384 | 64 | 0.386404 | 0.003704 | 0.384701–0.388324 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side-reciprocal/clustered-edit/interior | 384 | 64 | 0.135783 | 0.001532 | 0.135040–0.136483 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side-reciprocal/linear-read | 384 | 64 | 1.026439 | 0.004344 | 1.024291–1.029590 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side-reciprocal/logical-projection | 384 | 64 | 0.969174 | 0.066996 | 0.925078–1.017619 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side-reciprocal/random-read | 384 | 64 | 1.027287 | 0.005173 | 1.024866–1.029580 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side-reciprocal/snapshot | 384 | 64 | 1.317518 | 0.109662 | 1.276427–1.414148 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side/clustered-edit/boundary | 384 | 64 | 2.588333 | 0.025180 | 2.575350–2.599713 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side/clustered-edit/interior | 384 | 64 | 7.365991 | 0.082838 | 7.328857–7.405071 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side/linear-read | 384 | 64 | 0.974319 | 0.004117 | 0.971281–0.976294 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side/logical-projection | 384 | 64 | 1.034351 | 0.069113 | 0.982740–1.081042 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side/random-read | 384 | 64 | 0.973440 | 0.004907 | 0.971279–0.975781 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side/snapshot | 384 | 64 | 0.760520 | 0.061854 | 0.709720–0.796663 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/clustered-edit/boundary/eight-side16 | 384 | 64 | 69.871627 | 0.926253 | 69.487317–70.204800 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/clustered-edit/boundary/one-side32 | 384 | 64 | 276.464619 | 5.487905 | 274.403276–279.949405 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/clustered-edit/interior/eight-side16 | 384 | 64 | 48.092964 | 0.636760 | 47.843742–48.403785 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/clustered-edit/interior/one-side32 | 384 | 64 | 280.235086 | 4.332109 | 276.747558–281.975345 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/linear-read/eight-side16 | 384 | 64 | 19.673639 | 0.102901 | 19.622175–19.724200 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/linear-read/one-side32 | 384 | 64 | 19.505032 | 0.090895 | 19.470471–19.533722 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/random-read/eight-side16 | 384 | 64 | 24.219124 | 0.134141 | 24.183435–24.308668 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/random-read/one-side32 | 384 | 64 | 19.531051 | 0.105703 | 19.510737–19.581365 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side-reciprocal/clustered-edit/boundary | 384 | 64 | 0.251812 | 0.003198 | 0.249823–0.253797 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side-reciprocal/clustered-edit/interior | 384 | 64 | 0.172146 | 0.001713 | 0.171316–0.173394 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side-reciprocal/linear-read | 384 | 64 | 1.008521 | 0.005171 | 1.005846–1.010487 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side-reciprocal/logical-projection | 384 | 64 | 0.847534 | 0.011759 | 0.842047–0.851156 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side-reciprocal/random-read | 384 | 64 | 1.239240 | 0.005996 | 1.236693–1.242706 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side-reciprocal/snapshot | 384 | 64 | 0.828599 | 0.023904 | 0.819794–0.840612 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side/clustered-edit/boundary | 384 | 64 | 3.971228 | 0.050601 | 3.941729–4.004265 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side/clustered-edit/interior | 384 | 64 | 5.809029 | 0.058013 | 5.767658–5.837165 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side/linear-read | 384 | 64 | 0.991555 | 0.005113 | 0.989653–0.994191 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side/logical-projection | 384 | 64 | 1.183425 | 0.016608 | 1.176925–1.195428 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side/random-read | 384 | 64 | 0.806963 | 0.003908 | 0.804708–0.808641 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side/snapshot | 384 | 64 | 1.207168 | 0.030752 | 1.191345–1.221084 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/clustered-edit/boundary/eight-side16 | 384 | 64 | 51.358764 | 1.688361 | 50.850396–52.567869 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/clustered-edit/boundary/one-side32 | 384 | 64 | 112.746057 | 6.599662 | 109.631907–114.943840 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/clustered-edit/interior/eight-side16 | 384 | 64 | 19.352069 | 0.914619 | 18.879886–19.735675 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/clustered-edit/interior/one-side32 | 384 | 64 | 108.756227 | 4.239746 | 106.791661–110.268072 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/linear-read/eight-side16 | 384 | 64 | 20.521787 | 0.135404 | 20.431734–20.582394 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/linear-read/one-side32 | 384 | 64 | 19.557546 | 0.233161 | 19.486500–19.673763 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/random-read/eight-side16 | 384 | 64 | 20.789698 | 0.174320 | 20.744951–20.889274 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/random-read/one-side32 | 384 | 64 | 20.088640 | 0.121892 | 20.027814–20.138498 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side-reciprocal/clustered-edit/boundary | 384 | 64 | 0.385172 | 0.031253 | 0.373578–0.393554 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side-reciprocal/clustered-edit/interior | 384 | 64 | 0.170837 | 0.005472 | 0.168287–0.172484 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side-reciprocal/linear-read | 384 | 64 | 1.043046 | 0.005794 | 1.040506–1.045782 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side-reciprocal/logical-projection | 384 | 64 | 1.017646 | 0.033334 | 1.003857–1.035092 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side-reciprocal/random-read | 384 | 64 | 1.037007 | 0.005803 | 1.034927–1.038944 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side-reciprocal/snapshot | 384 | 64 | 1.152064 | 0.024935 | 1.141756–1.158143 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side/clustered-edit/boundary | 384 | 64 | 2.662257 | 0.228916 | 2.614556–2.750533 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side/clustered-edit/interior | 384 | 64 | 5.854166 | 0.182070 | 5.799110–5.943082 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side/linear-read | 384 | 64 | 0.958762 | 0.005317 | 0.956264–0.961141 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side/logical-projection | 384 | 64 | 0.987709 | 0.031623 | 0.967138–1.001098 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side/random-read | 384 | 64 | 0.964330 | 0.005399 | 0.962519–0.966257 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side/snapshot | 384 | 64 | 0.868269 | 0.019041 | 0.863497–0.875946 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| section-side-reciprocal/clustered-edit | 3072 | 256 | 0.255568 | 0.040105 | 0.250146–0.257845 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/clustered-edit/boundary | 1536 | 256 | 0.386014 | 0.122342 | 0.383441–0.388939 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/clustered-edit/interior | 1536 | 256 | 0.171732 | 0.034385 | 0.171011–0.172879 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/linear-read | 1536 | 256 | 1.026244 | 0.011046 | 1.024065–1.028291 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/logical-projection | 1536 | 256 | 1.005504 | 0.068679 | 0.982938–1.028190 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/random-read | 1536 | 256 | 1.035127 | 0.010520 | 1.032453–1.037339 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/read | 3072 | 256 | 1.034254 | 0.009056 | 1.032273–1.037241 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/snapshot | 1536 | 256 | 1.180358 | 0.287726 | 1.156839–1.241194 | paired lower-is-better side16/side32 duration ratio |
| section-side/clustered-edit | 3072 | 256 | 4.619872 | 0.334297 | 4.531604–4.691369 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/clustered-edit/boundary | 1536 | 256 | 2.601029 | 1.191603 | 2.583584–2.622783 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/clustered-edit/interior | 1536 | 256 | 5.823192 | 1.458138 | 5.785806–5.848755 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/linear-read | 1536 | 256 | 0.974462 | 0.010561 | 0.972608–0.976508 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/logical-projection | 1536 | 256 | 0.996611 | 0.068990 | 0.974210–1.017692 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/random-read | 1536 | 256 | 0.966069 | 0.009830 | 0.963951–0.968621 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/read | 3072 | 256 | 0.966882 | 0.008466 | 0.964102–0.968736 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/snapshot | 1536 | 256 | 0.847202 | 0.204149 | 0.808843–0.864560 | paired lower-is-better one-side32/eight-side16 duration ratio |
| fresh-process-memory/homogeneous/one-side32-vs-dense | 9 | 9 | 0.000793 | 0.000000 | 0.000793–0.000793 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/homogeneous/eight-side16-vs-dense | 9 | 9 | 0.004638 | 0.000000 | 0.004638–0.004638 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/homogeneous/section-side | 9 | 9 | 0.171081 | 0.000000 | 0.171081–0.171081 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/homogeneous/section-side-reciprocal | 9 | 9 | 5.845200 | 0.000000 | 5.845200–5.845200 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/layered/one-side32-vs-dense | 9 | 9 | 0.066329 | 0.000000 | 0.066329–0.066329 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/layered/eight-side16-vs-dense | 9 | 9 | 0.045643 | 0.000000 | 0.045643–0.045643 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/layered/section-side | 9 | 9 | 1.453207 | 0.000000 | 1.453207–1.453207 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/layered/section-side-reciprocal | 9 | 9 | 0.688133 | 0.000000 | 0.688133–0.688133 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/mixed/one-side32-vs-dense | 9 | 9 | 0.202892 | 0.000000 | 0.202892–0.202892 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/mixed/eight-side16-vs-dense | 9 | 9 | 0.312424 | 0.000000 | 0.312424–0.312424 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/mixed/section-side | 9 | 9 | 0.649414 | 0.000000 | 0.649414–0.649414 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/mixed/section-side-reciprocal | 9 | 9 | 1.539850 | 0.000000 | 1.539850–1.539850 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/highentropy/one-side32-vs-dense | 9 | 9 | 1.000793 | 0.000000 | 1.000793–1.000793 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/highentropy/eight-side16-vs-dense | 9 | 9 | 1.006346 | 0.000000 | 1.006346–1.006346 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/highentropy/section-side | 9 | 9 | 0.994482 | 0.000000 | 0.994482–0.994482 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/highentropy/section-side-reciprocal | 9 | 9 | 1.005548 | 0.000000 | 1.005548–1.005548 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/homogeneous-layered-mixed/one-side32-vs-dense | 9 | 9 | 0.090005 | 0.000000 | 0.090005–0.090005 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/homogeneous-layered-mixed/eight-side16-vs-dense | 9 | 9 | 0.120902 | 0.000000 | 0.120902–0.120902 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/balanced/section-side | 9 | 9 | 0.928240 | 0.000000 | 0.928240–0.928240 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/balanced/section-side-reciprocal | 9 | 9 | 1.077307 | 0.000000 | 1.077307–1.077307 | paired fresh-process retained-memory ratio; lower is better |

## Amplification

Each natural window is one deterministic 4x4x4 (64-operation) cluster. Logical-projection bytes are bytes of the #8 canonical logical fixture for dirty semantic records; they are neither save nor wire bytes. Gross halo samples sum (side+2)^3 for dirty sections; unique halo samples deduplicate world-space sample coordinates inside a window.

| Distribution | Layout / trace | Windows | Logical bytes p95 | Unique halo p95 | Gross halo p95 |

| --- | --- | ---: | ---: | ---: | ---: |

| all (equal weight) | eight-side16 / BoundaryClusters | 256 | 1243244 | 39304 | 46656 |
| all (equal weight) | eight-side16 / InteriorClusters | 256 | 153839 | 5832 | 5832 |
| all (equal weight) | one-side32 / BoundaryClusters | 256 | 1242901 | 39304 | 39304 |
| all (equal weight) | one-side32 / InteriorClusters | 256 | 1242955 | 39304 | 39304 |
| highentropy | eight-side16 / BoundaryClusters | 64 | 1243244 | 39304 | 46656 |
| highentropy | eight-side16 / InteriorClusters | 64 | 153841 | 5832 | 5832 |
| highentropy | one-side32 / BoundaryClusters | 64 | 1242901 | 39304 | 39304 |
| highentropy | one-side32 / InteriorClusters | 64 | 1242957 | 39304 | 39304 |
| homogeneous | eight-side16 / BoundaryClusters | 64 | 67295 | 39304 | 46656 |
| homogeneous | eight-side16 / InteriorClusters | 64 | 9636 | 5832 | 5832 |
| homogeneous | one-side32 / BoundaryClusters | 64 | 66980 | 39304 | 39304 |
| homogeneous | one-side32 / InteriorClusters | 64 | 66980 | 39304 | 39304 |
| layered | eight-side16 / BoundaryClusters | 64 | 66734 | 39304 | 46656 |
| layered | eight-side16 / InteriorClusters | 64 | 9007 | 5832 | 5832 |
| layered | one-side32 / BoundaryClusters | 64 | 66383 | 39304 | 39304 |
| layered | one-side32 / InteriorClusters | 64 | 66383 | 39304 | 39304 |
| mixed | eight-side16 / BoundaryClusters | 64 | 70393 | 39304 | 46656 |
| mixed | eight-side16 / InteriorClusters | 64 | 10998 | 5832 | 5832 |
| mixed | one-side32 / BoundaryClusters | 64 | 68374 | 39304 | 39304 |
| mixed | one-side32 / InteriorClusters | 64 | 68374 | 39304 | 39304 |

## Retained memory

Each mode/distribution/trial is a fresh process with a retained root and explicit full compacting collections before and after allocation. Mode launch order rotates deterministically within trial/distribution, and the entire memory phase has a fixed 04:00:00 deadline. The fixed corpus contributes equal counts of homogeneous, layered, mixed, and high-entropy cubes. GC deltas are not storage, save, network, or wire bytes.
| Mode | Distribution | Cubes/trial | Valid / invalid trials | Median retained bytes | Median known payload bytes |
| --- | --- | ---: | ---: | ---: | ---: |
| DenseCanonical | Homogeneous | 3125 | 9 / 0 | 409700064 | 409600000 |
| DenseCanonical | Layered | 3125 | 9 / 0 | 409700064 | 409600000 |
| DenseCanonical | Mixed | 3125 | 9 / 0 | 409700064 | 409600000 |
| DenseCanonical | HighEntropy | 3125 | 9 / 0 | 409700064 | 409600000 |
| OneSide32Adaptive | Homogeneous | 3125 | 9 / 0 | 325064 | 12500 |
| OneSide32Adaptive | Layered | 3125 | 9 / 0 | 27175064 | 25650000 |
| OneSide32Adaptive | Mixed | 3125 | 9 / 0 | 83125064 | 77600000 |
| OneSide32Adaptive | HighEntropy | 3125 | 9 / 0 | 410025064 | 409600000 |
| EightSide16Adaptive | Homogeneous | 3125 | 9 / 0 | 1900064 | 100000 |
| EightSide16Adaptive | Layered | 3125 | 9 / 0 | 18700064 | 13050000 |
| EightSide16Adaptive | Mixed | 3125 | 9 / 0 | 128000144 | 83200000 |
| EightSide16Adaptive | HighEntropy | 3125 | 9 / 0 | 412300064 | 409600000 |

## Manifest

Observed host/runtime: Fedora Linux 43 (KDE Plasma Desktop Edition) / .NET 10.0.11 / SDK 10.0.400.
CPU/power/GC: AMD Ryzen 7 5800U with Radeon Graphics / platform-profile=balanced; cpu-governor=powersave / server GC=False, latency=Interactive.
Source: f84544935abf98cc066356002188cf89e114eb0b; dirty=False; source hash=142f0283c509438095ca7eeca5153603ef81f6a9bf32eef19ba3fa10e06dfc98.

The companion JSON contains summary metadata and a SHA-256 reference to the bounded raw NDJSON observation artifact.
