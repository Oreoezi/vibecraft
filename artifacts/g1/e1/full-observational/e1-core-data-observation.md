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
| adaptive-timing-one-side32 | fail | Maximum upper95 adaptive/dense ratio across random read, linear read, interior edits, and boundary edits is 273.196319 (limit 1.15). |
| adaptive-timing-eight-side16 | fail | Maximum upper95 adaptive/dense ratio across random read, linear read, interior edits, and boundary edits is 68.621421 (limit 1.15). |
| side32-memory | fail | Balanced upper95 side32/side16 retained-memory ratio is 0.928240 (limit 0.80). |
| side32-primary | fail | 0 of five retained-memory/read/edit/snapshot/projection upper95 side32/side16 ratios are <= 0.80; maximum upper95 is 4.671214 (limit 1.15). |
| side32-amplification | fail | Maximum p95 candidate/reference ratio across canonical logical-projection bytes and unique remesh-halo samples is 8.079583 (limit 2.0). |
| side16-primary | pass | Equal-weight geometric mean of five median side16/side32 ratios is 0.775516 (limit 1.15); maximum upper95 is 1.077307 (limit 1.25). |
| side16-amplification | pass | Maximum p95 candidate/reference ratio across canonical logical-projection bytes and unique remesh-halo samples is 1.000276 (limit 2.0). |
| save-and-network-amplification | inconclusive | No G1 save format or network encoding exists to measure. Canonical logical-projection bytes are retained as a representation-neutral republish proxy and are not relabeled as storage or wire evidence. |
| g0-owner-acceptance | blocked | No owner acceptance exists for this host, runtime, GC mode, power mode, or the applicable G0 product budgets. |

## Metric summaries

| Metric | Raw pairs | Independent units | Median ratio | MAD | 95% bootstrap interval | Definition |

| --- | ---: | ---: | ---: | ---: | ---: | --- |

| adaptive-vs-dense/clustered-edit/boundary/eight-side16 | 1536 | 256 | 66.418714 | 46.743524 | 54.914297–68.621421 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/clustered-edit/boundary/one-side32 | 1536 | 256 | 195.807926 | 180.854252 | 112.004772–273.196319 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/clustered-edit/interior/eight-side16 | 1536 | 256 | 34.587311 | 24.190602 | 21.268277–47.130220 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/clustered-edit/interior/one-side32 | 1536 | 256 | 206.960606 | 171.558234 | 117.887124–270.880420 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/linear-read/eight-side16 | 1536 | 256 | 19.462155 | 0.520656 | 19.330163–19.622146 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/linear-read/one-side32 | 1536 | 256 | 19.068675 | 0.531358 | 18.822243–19.328918 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/random-read/eight-side16 | 1536 | 256 | 20.154859 | 1.355431 | 19.370626–20.772547 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/random-read/one-side32 | 1536 | 256 | 19.006828 | 0.668162 | 18.753837–19.251405 | paired lower-is-better adaptive/dense duration ratio |
| distribution/highentropy/adaptive-vs-dense/clustered-edit/boundary/eight-side16 | 384 | 64 | 2.045725 | 0.181451 | 1.983008–2.160279 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/clustered-edit/boundary/one-side32 | 384 | 64 | 1.711652 | 0.062679 | 1.666490–1.728469 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/clustered-edit/interior/eight-side16 | 384 | 64 | 1.866343 | 0.098631 | 1.811086–1.889681 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/clustered-edit/interior/one-side32 | 384 | 64 | 1.696534 | 0.067235 | 1.643634–1.727610 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/linear-read/eight-side16 | 384 | 64 | 18.767242 | 0.126597 | 18.725718–18.837053 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/linear-read/one-side32 | 384 | 64 | 18.228913 | 0.135579 | 18.160955–18.283133 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/random-read/eight-side16 | 384 | 64 | 18.681949 | 0.122867 | 18.640834–18.737304 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/random-read/one-side32 | 384 | 64 | 18.205626 | 0.144398 | 18.168973–18.262760 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side-reciprocal/clustered-edit/boundary | 384 | 64 | 1.232443 | 0.082945 | 1.202062–1.247257 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side-reciprocal/clustered-edit/interior | 384 | 64 | 1.094268 | 0.062914 | 1.072362–1.135442 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side-reciprocal/linear-read | 384 | 64 | 1.026925 | 0.003423 | 1.025668–1.027728 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side-reciprocal/logical-projection | 384 | 64 | 1.064271 | 0.013074 | 1.058954–1.071885 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side-reciprocal/random-read | 384 | 64 | 1.027928 | 0.006695 | 1.026492–1.030839 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side-reciprocal/snapshot | 384 | 64 | 6.084631 | 0.348489 | 5.907095–6.177118 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side/clustered-edit/boundary | 384 | 64 | 0.814904 | 0.054887 | 0.803633–0.835609 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side/clustered-edit/interior | 384 | 64 | 0.918078 | 0.053480 | 0.882226–0.936431 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side/linear-read | 384 | 64 | 0.973786 | 0.003270 | 0.973181–0.974977 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side/logical-projection | 384 | 64 | 0.940099 | 0.011438 | 0.933089–0.944235 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side/random-read | 384 | 64 | 0.972855 | 0.006321 | 0.970089–0.974198 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side/snapshot | 384 | 64 | 0.164364 | 0.008845 | 0.161928–0.169530 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/clustered-edit/boundary/eight-side16 | 384 | 64 | 177.408152 | 3.263484 | 176.534464–179.572572 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/clustered-edit/boundary/one-side32 | 384 | 64 | 463.115097 | 6.562219 | 459.688017–465.444062 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/clustered-edit/interior/eight-side16 | 384 | 64 | 63.346936 | 1.308289 | 62.742320–64.121371 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/clustered-edit/interior/one-side32 | 384 | 64 | 456.857921 | 8.009853 | 453.293333–464.070457 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/linear-read/eight-side16 | 384 | 64 | 19.238884 | 0.106544 | 19.196737–19.285323 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/linear-read/one-side32 | 384 | 64 | 18.699318 | 0.120335 | 18.673532–18.773326 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/random-read/eight-side16 | 384 | 64 | 19.143008 | 0.227297 | 19.073205–19.228903 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/random-read/one-side32 | 384 | 64 | 18.666930 | 0.120944 | 18.584824–18.721934 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side-reciprocal/clustered-edit/boundary | 384 | 64 | 0.383430 | 0.004314 | 0.381316–0.386118 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side-reciprocal/clustered-edit/interior | 384 | 64 | 0.135629 | 0.002012 | 0.134896–0.137412 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side-reciprocal/linear-read | 384 | 64 | 1.024515 | 0.004832 | 1.022859–1.026601 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side-reciprocal/logical-projection | 384 | 64 | 0.835587 | 0.070645 | 0.806880–0.895864 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side-reciprocal/random-read | 384 | 64 | 1.029453 | 0.004755 | 1.026951–1.031623 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side-reciprocal/snapshot | 384 | 64 | 0.953817 | 0.073098 | 0.841898–0.991542 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side/clustered-edit/boundary | 384 | 64 | 2.608084 | 0.029695 | 2.589962–2.622501 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side/clustered-edit/interior | 384 | 64 | 7.373149 | 0.106199 | 7.278399–7.412473 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side/linear-read | 384 | 64 | 0.976087 | 0.004610 | 0.974090–0.977664 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side/logical-projection | 384 | 64 | 1.196861 | 0.099173 | 1.117771–1.239553 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side/random-read | 384 | 64 | 0.971392 | 0.004510 | 0.969472–0.973793 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side/snapshot | 384 | 64 | 1.048772 | 0.078520 | 1.008667–1.193937 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/clustered-edit/boundary/eight-side16 | 384 | 64 | 69.454799 | 1.076683 | 68.991782–70.230382 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/clustered-edit/boundary/one-side32 | 384 | 64 | 276.718999 | 4.589702 | 275.503884–279.682937 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/clustered-edit/interior/eight-side16 | 384 | 64 | 48.081169 | 0.864934 | 47.630133–48.488409 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/clustered-edit/interior/one-side32 | 384 | 64 | 276.615923 | 4.765549 | 273.447419–279.031888 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/linear-read/eight-side16 | 384 | 64 | 19.693599 | 0.100414 | 19.657744–19.727810 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/linear-read/one-side32 | 384 | 64 | 19.533345 | 0.115956 | 19.478385–19.578230 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/random-read/eight-side16 | 384 | 64 | 24.276614 | 0.142274 | 24.187254–24.311313 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/random-read/one-side32 | 384 | 64 | 19.548981 | 0.133417 | 19.490874–19.615732 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side-reciprocal/clustered-edit/boundary | 384 | 64 | 0.251883 | 0.003785 | 0.249880–0.254663 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side-reciprocal/clustered-edit/interior | 384 | 64 | 0.172429 | 0.001985 | 0.172035–0.173608 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side-reciprocal/linear-read | 384 | 64 | 1.009343 | 0.006602 | 1.006335–1.010839 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side-reciprocal/logical-projection | 384 | 64 | 0.848978 | 0.018087 | 0.839142–0.859100 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side-reciprocal/random-read | 384 | 64 | 1.237811 | 0.006798 | 1.234744–1.241799 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side-reciprocal/snapshot | 384 | 64 | 0.621226 | 0.013083 | 0.614125–0.628079 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side/clustered-edit/boundary | 384 | 64 | 3.970451 | 0.059146 | 3.928758–4.002329 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side/clustered-edit/interior | 384 | 64 | 5.799932 | 0.066270 | 5.760224–5.813245 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side/linear-read | 384 | 64 | 0.990768 | 0.006477 | 0.989504–0.993719 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side/logical-projection | 384 | 64 | 1.177912 | 0.024996 | 1.164450–1.191919 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side/random-read | 384 | 64 | 0.807880 | 0.004439 | 0.805297–0.809933 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side/snapshot | 384 | 64 | 1.610161 | 0.034287 | 1.593514–1.628916 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/clustered-edit/boundary/eight-side16 | 384 | 64 | 51.560807 | 2.591954 | 50.421368–52.451734 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/clustered-edit/boundary/one-side32 | 384 | 64 | 108.496156 | 4.822000 | 105.316411–109.491307 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/clustered-edit/interior/eight-side16 | 384 | 64 | 19.656477 | 1.090759 | 19.146861–20.150850 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/clustered-edit/interior/one-side32 | 384 | 64 | 109.932792 | 6.365252 | 106.727996–112.702960 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/linear-read/eight-side16 | 384 | 64 | 20.469003 | 0.115207 | 20.418647–20.531012 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/linear-read/one-side32 | 384 | 64 | 19.671541 | 0.234681 | 19.530183–19.775864 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/random-read/eight-side16 | 384 | 64 | 20.912934 | 0.110409 | 20.835803–20.964806 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/random-read/one-side32 | 384 | 64 | 20.245232 | 0.190621 | 20.183217–20.325590 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side-reciprocal/clustered-edit/boundary | 384 | 64 | 0.452871 | 0.015182 | 0.445879–0.457496 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side-reciprocal/clustered-edit/interior | 384 | 64 | 0.116160 | 0.005719 | 0.114448–0.119134 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side-reciprocal/linear-read | 384 | 64 | 1.041430 | 0.009601 | 1.038356–1.046199 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side-reciprocal/logical-projection | 384 | 64 | 1.042653 | 0.011394 | 1.038461–1.046715 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side-reciprocal/random-read | 384 | 64 | 1.030423 | 0.012538 | 1.023495–1.035410 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side-reciprocal/snapshot | 384 | 64 | 1.043252 | 0.018179 | 1.039371–1.055624 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side/clustered-edit/boundary | 384 | 64 | 2.209145 | 0.070475 | 2.190300–2.247980 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side/clustered-edit/interior | 384 | 64 | 8.619926 | 0.426346 | 8.401119–8.784483 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side/linear-read | 384 | 64 | 0.960325 | 0.008871 | 0.955855–0.963103 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side/logical-projection | 384 | 64 | 0.959230 | 0.010668 | 0.955371–0.963150 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side/random-read | 384 | 64 | 0.970576 | 0.011812 | 0.965815–0.977097 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side/snapshot | 384 | 64 | 0.958559 | 0.016419 | 0.947967–0.962832 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| section-side-reciprocal/clustered-edit | 3072 | 256 | 0.251255 | 0.035096 | 0.246382–0.254860 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/clustered-edit/boundary | 1536 | 256 | 0.391071 | 0.122021 | 0.385527–0.430577 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/clustered-edit/interior | 1536 | 256 | 0.161684 | 0.029472 | 0.139047–0.170583 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/linear-read | 1536 | 256 | 1.025582 | 0.009823 | 1.023651–1.026849 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/logical-projection | 1536 | 256 | 0.989846 | 0.082228 | 0.925678–1.030720 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/random-read | 1536 | 256 | 1.033409 | 0.012602 | 1.031291–1.036761 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/read | 3072 | 256 | 1.030283 | 0.009523 | 1.028587–1.034560 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/snapshot | 1536 | 256 | 1.016192 | 0.311075 | 0.992190–1.034565 | paired lower-is-better side16/side32 duration ratio |
| section-side/clustered-edit | 3072 | 256 | 4.600988 | 0.316175 | 4.520308–4.671214 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/clustered-edit/boundary | 1536 | 256 | 2.557234 | 1.160769 | 2.320773–2.594030 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/clustered-edit/interior | 1536 | 256 | 6.189421 | 1.375199 | 5.862258–7.196523 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/linear-read | 1536 | 256 | 0.975087 | 0.009343 | 0.973853–0.976904 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/logical-projection | 1536 | 256 | 1.010349 | 0.082340 | 0.969692–1.080676 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/random-read | 1536 | 256 | 0.967711 | 0.011709 | 0.964516–0.969794 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/read | 3072 | 256 | 0.970608 | 0.009042 | 0.966595–0.972244 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/snapshot | 1536 | 256 | 0.984451 | 0.434806 | 0.966929–1.008932 | paired lower-is-better one-side32/eight-side16 duration ratio |
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
Source: bc8117549935cf74d6fa3870e4364bfc05ee24ff; dirty=False; source hash=361d986978a647882cee6c6c68d5cbddc05a947b7244920a0797337b8e42e74c.

The companion JSON contains summary metadata and a SHA-256 reference to the bounded raw NDJSON observation artifact.
