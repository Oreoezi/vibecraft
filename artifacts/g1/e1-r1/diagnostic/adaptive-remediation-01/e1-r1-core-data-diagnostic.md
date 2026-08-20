# G1/E1-r1 core-data diagnostics

**Disposition: defer; evidence classification: diagnostic.** The predeclared G0 owner acceptance for host, runtime, GC, power, and product budgets is absent; this report cannot select a candidate or freeze compatibility constants.

Protocol `VC-G1-E1-PROTOCOL-1.0.0`; fixture set: `VC-G0-FP-0.1.0`, `VC-G1-E1-SECTIONS-1.0.0`, `VC-G1-E1-LOGICAL-PROJECTION-0.1.0`; seed `0x5643424654314531`.

## Protocol

The corpus streams canonical 32-cubed semantic cubes. Ordinal modulo four selects homogeneous (alternating air/stone), layered, mixed, and high-entropy distributions. Every semantic fingerprint hashes the domain, fixture, seed, ordinal, distribution byte, and exactly 32,768 BlockStateId values in X-to-Z-to-Y order. One 32-cubed section and eight 16-cubed sections must match that semantic fingerprint; their logical-projection byte hashes are intentionally not required to match.

This profile fingerprints all 12500 corpus cubes and measures timing/amplification on 256 deterministic round-robin cubes. It applies 1 4x4x4 cluster per measured cube and trace (256 clusters per trace in total), avoiding an unintended corpus-times-cluster cross-product.

Read and edit routing is resolved into layout-specific SectionIndex/LocalIndex traces before any timed interval. Adaptive and dense candidates consume the same addressed trace and the same section layout, so the ratio isolates storage behavior rather than global-coordinate decomposition. Measurements are same-machine paired Stopwatch diagnostics. Orders alternate; raw samples include order, duration, checksum, allocations, and operation counts. One same-cube, same-round candidate/baseline pair is the raw unit. Summaries first take one median per cube across that cube's rounds (and traces for a grouped category), then bootstrap those cube-level units so repeated measurements are not treated as independent. No raw outlier is removed. Retained memory, when available, uses fresh child processes per distribution and is explicitly distinct from known logical payload bytes.

## Diagnostic assessment

The full diagnostic profile records each predeclared threshold result without selecting a candidate. Thresholds remain unchanged; only issue #31 may run the owner-accepted decision profile.

Reason for overall defer: G0 owner acceptance of the benchmark host, runtime, GC, power conditions, and applicable product budgets is explicitly absent. The issue protocol therefore requires defer even if a diagnostic metric rule appears to pass.

| Criterion | Status | Evidence |
| --- | --- | --- |
| fixed-full-profile | pass | The fixed 12,500-cube, six-round, nine-memory-trial, 10,000-resample profile completed. |
| semantic-and-projection-correctness | pass | The report completed exhaustive side16/side32 indexing, equal-volume semantic checks, palette-boundary diagnostics, snapshots, and deterministic canonical logical projections without an invariant failure. |
| zero-allocation-warmed-reads | pass | Every measured warmed random and linear read reported exactly zero thread allocations. |
| positive-measured-durations | pass | Every paired duration was positive; no equality ratio was synthesized. |
| adaptive-memory-one-side32 | pass | homogeneous-layered-mixed upper95=0.090005 (limit 0.50); highentropy upper95=1.000793 (limit 1.10) |
| adaptive-memory-eight-side16 | pass | homogeneous-layered-mixed upper95=0.120658 (limit 0.50); highentropy upper95=1.006346 (limit 1.10) |
| adaptive-timing-one-side32 | fail | Maximum upper95 adaptive/dense ratio across random read, linear read, interior edits, and boundary edits is 718.193683 (limit 1.15). |
| adaptive-timing-eight-side16 | fail | Maximum upper95 adaptive/dense ratio across random read, linear read, interior edits, and boundary edits is 329.116594 (limit 1.15). |
| side32-memory | fail | Balanced upper95 side32/side16 retained-memory ratio is 0.928737 (limit 0.80). |
| side32-primary | fail | 0 of five retained-memory/grouped-read/grouped-edit/snapshot/projection upper95 side32/side16 ratios are <= 0.80; maximum upper95 is 4.435248 (limit 1.15). |
| side32-amplification | fail | Maximum p95 candidate/reference ratio across canonical logical-projection bytes and unique remesh-halo samples is 8.079583 (limit 2.0). |
| side16-primary | pass | Equal-weight geometric mean of five median side16/side32 ratios is 0.809173 (limit 1.15); maximum upper95 is 1.076731 (limit 1.25). |
| side16-amplification | pass | Maximum p95 candidate/reference ratio across canonical logical-projection bytes and unique remesh-halo samples is 1.000276 (limit 2.0). |
| storage-neutral-logical-diagnostics | pass | Canonical logical values republished, projection byte length/digest, dirty-section count, and unique/gross remesh-halo samples were recorded. None is labeled as persistence or wire throughput. |
| g0-owner-acceptance | blocked | No owner acceptance exists for this host, runtime, GC mode, power mode, or the applicable G0 product budgets. |

## Metric summaries

| Metric | Raw pairs | Independent units | Median ratio | MAD | 95% bootstrap interval | Definition |

| --- | ---: | ---: | ---: | ---: | ---: | --- |

| adaptive-vs-dense/clustered-edit/boundary/eight-side16 | 1536 | 256 | 228.045613 | 169.871649 | 127.617377–329.116594 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/clustered-edit/boundary/one-side32 | 1536 | 256 | 462.749745 | 371.856347 | 250.398596–717.375739 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/clustered-edit/interior/eight-side16 | 1536 | 256 | 78.705372 | 48.620913 | 34.202117–124.020325 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/clustered-edit/interior/one-side32 | 1536 | 256 | 462.010534 | 383.631534 | 241.955320–718.193683 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/linear-read/eight-side16 | 1536 | 256 | 2.848813 | 0.590390 | 2.597327–3.183047 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/linear-read/one-side32 | 1536 | 256 | 3.347729 | 0.832105 | 2.607758–4.151678 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/random-read/eight-side16 | 1536 | 256 | 3.717816 | 1.350412 | 2.671585–4.725891 | paired lower-is-better adaptive/dense duration ratio |
| adaptive-vs-dense/random-read/one-side32 | 1536 | 256 | 3.407925 | 0.999926 | 2.675068–4.151534 | paired lower-is-better adaptive/dense duration ratio |
| distribution/highentropy/adaptive-vs-dense/clustered-edit/boundary/eight-side16 | 384 | 64 | 1.175678 | 0.030745 | 1.164246–1.195904 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/clustered-edit/boundary/one-side32 | 384 | 64 | 1.043964 | 0.030823 | 1.030504–1.054508 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/clustered-edit/interior/eight-side16 | 384 | 64 | 1.122696 | 0.034921 | 1.111956–1.141778 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/clustered-edit/interior/one-side32 | 384 | 64 | 1.046971 | 0.030733 | 1.031591–1.059698 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/linear-read/eight-side16 | 384 | 64 | 2.590028 | 0.004841 | 2.588577–2.592276 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/linear-read/one-side32 | 384 | 64 | 2.589785 | 0.010086 | 2.585574–2.596731 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/random-read/eight-side16 | 384 | 64 | 2.653478 | 0.013071 | 2.648285–2.657997 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/adaptive-vs-dense/random-read/one-side32 | 384 | 64 | 2.656444 | 0.010448 | 2.651627–2.663831 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side-reciprocal/clustered-edit/boundary | 384 | 64 | 1.179991 | 0.037995 | 1.167700–1.202451 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side-reciprocal/clustered-edit/interior | 384 | 64 | 1.049453 | 0.032939 | 1.038827–1.073842 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side-reciprocal/linear-read | 384 | 64 | 1.002981 | 0.005017 | 1.000192–1.005836 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side-reciprocal/logical-projection | 384 | 64 | 1.023226 | 0.029250 | 1.008978–1.032524 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side-reciprocal/random-read | 384 | 64 | 0.998345 | 0.003816 | 0.997125–1.000411 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side-reciprocal/snapshot | 384 | 64 | 0.798847 | 0.041880 | 0.782218–0.818154 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side/clustered-edit/boundary | 384 | 64 | 0.847542 | 0.026918 | 0.833384–0.856689 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side/clustered-edit/interior | 384 | 64 | 0.953711 | 0.030876 | 0.930626–0.962925 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side/linear-read | 384 | 64 | 0.997030 | 0.004992 | 0.994200–0.999808 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side/logical-projection | 384 | 64 | 0.977416 | 0.028383 | 0.969918–0.991227 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side/random-read | 384 | 64 | 1.001678 | 0.003847 | 0.999774–1.002902 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/highentropy/section-side/snapshot | 384 | 64 | 1.251912 | 0.069918 | 1.225995–1.280192 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/clustered-edit/boundary/eight-side16 | 384 | 64 | 421.429459 | 3.105684 | 420.092342–422.886366 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/clustered-edit/boundary/one-side32 | 384 | 64 | 964.583006 | 15.660443 | 952.744926–970.386316 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/clustered-edit/interior/eight-side16 | 384 | 64 | 127.110910 | 1.310737 | 126.336914–127.764770 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/clustered-edit/interior/one-side32 | 384 | 64 | 971.135131 | 15.966750 | 966.462506–976.787223 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/linear-read/eight-side16 | 384 | 64 | 2.186116 | 0.005482 | 2.184153–2.188006 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/linear-read/one-side32 | 384 | 64 | 2.186098 | 0.007583 | 2.183362–2.189327 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/random-read/eight-side16 | 384 | 64 | 2.185485 | 0.004870 | 2.182553–2.187355 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/adaptive-vs-dense/random-read/one-side32 | 384 | 64 | 2.182117 | 0.007007 | 2.178143–2.185680 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side-reciprocal/clustered-edit/boundary | 384 | 64 | 0.439589 | 0.003141 | 0.437977–0.440722 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side-reciprocal/clustered-edit/interior | 384 | 64 | 0.126911 | 0.000530 | 0.126562–0.127046 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side-reciprocal/linear-read | 384 | 64 | 0.999659 | 0.004874 | 0.996920–1.002122 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side-reciprocal/logical-projection | 384 | 64 | 0.998237 | 0.016155 | 0.984859–1.003371 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side-reciprocal/random-read | 384 | 64 | 1.002117 | 0.004909 | 0.999209–1.004521 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side-reciprocal/snapshot | 384 | 64 | 2.750000 | 0.182143 | 2.666667–2.828571 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side/clustered-edit/boundary | 384 | 64 | 2.275183 | 0.016087 | 2.269080–2.283266 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side/clustered-edit/interior | 384 | 64 | 7.879622 | 0.032994 | 7.871876–7.897378 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side/linear-read | 384 | 64 | 1.000347 | 0.004936 | 0.997912–1.003120 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side/logical-projection | 384 | 64 | 1.002104 | 0.016649 | 0.996983–1.015374 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side/random-read | 384 | 64 | 0.997890 | 0.004901 | 0.995555–1.000834 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/homogeneous/section-side/snapshot | 384 | 64 | 0.393027 | 0.029105 | 0.382197–0.409562 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/clustered-edit/boundary/eight-side16 | 384 | 64 | 333.465776 | 3.462709 | 331.156687–334.938412 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/clustered-edit/boundary/one-side32 | 384 | 64 | 727.927527 | 11.339021 | 721.813044–732.615886 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/clustered-edit/interior/eight-side16 | 384 | 64 | 127.539311 | 1.377232 | 126.686021–127.868317 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/clustered-edit/interior/one-side32 | 384 | 64 | 724.959650 | 9.148558 | 721.660904–730.943703 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/linear-read/eight-side16 | 384 | 64 | 3.231446 | 0.042617 | 3.190066–3.235574 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/linear-read/one-side32 | 384 | 64 | 4.161597 | 0.014138 | 4.156550–4.170493 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/random-read/eight-side16 | 384 | 64 | 8.501530 | 0.031906 | 8.486340–8.519936 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/adaptive-vs-dense/random-read/one-side32 | 384 | 64 | 4.170398 | 0.017924 | 4.160358–4.176970 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side-reciprocal/clustered-edit/boundary | 384 | 64 | 0.458675 | 0.002808 | 0.456558–0.459588 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side-reciprocal/clustered-edit/interior | 384 | 64 | 0.167670 | 0.000566 | 0.167466–0.167862 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side-reciprocal/linear-read | 384 | 64 | 0.775783 | 0.010269 | 0.770228–0.777749 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side-reciprocal/logical-projection | 384 | 64 | 0.874182 | 0.018209 | 0.867871–0.883909 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side-reciprocal/random-read | 384 | 64 | 2.036283 | 0.010781 | 2.030646–2.040806 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side-reciprocal/snapshot | 384 | 64 | 0.520204 | 0.007242 | 0.516570–0.524315 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side/clustered-edit/boundary | 384 | 64 | 2.180197 | 0.013499 | 2.176132–2.190314 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side/clustered-edit/interior | 384 | 64 | 5.964135 | 0.020133 | 5.957425–5.971456 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side/linear-read | 384 | 64 | 1.289041 | 0.017187 | 1.285928–1.298320 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side/logical-projection | 384 | 64 | 1.144513 | 0.023878 | 1.131369–1.153120 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side/random-read | 384 | 64 | 0.491101 | 0.002601 | 0.490003–0.492460 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/layered/section-side/snapshot | 384 | 64 | 1.923920 | 0.027935 | 1.908145–1.935963 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/clustered-edit/boundary/eight-side16 | 384 | 64 | 124.935730 | 2.792172 | 123.804052–125.948844 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/clustered-edit/boundary/one-side32 | 384 | 64 | 246.449788 | 4.104255 | 244.612133–248.555345 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/clustered-edit/interior/eight-side16 | 384 | 64 | 33.823968 | 0.317565 | 33.698159–34.067232 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/clustered-edit/interior/one-side32 | 384 | 64 | 239.193560 | 2.765703 | 237.632573–240.032963 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/linear-read/eight-side16 | 384 | 64 | 4.212485 | 0.024862 | 4.201134–4.235371 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/linear-read/one-side32 | 384 | 64 | 4.194897 | 0.019263 | 4.184762–4.205119 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/random-read/eight-side16 | 384 | 64 | 4.750855 | 0.047647 | 4.739141–4.769765 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/adaptive-vs-dense/random-read/one-side32 | 384 | 64 | 4.710985 | 0.022613 | 4.701618–4.720086 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side-reciprocal/clustered-edit/boundary | 384 | 64 | 0.523292 | 0.003959 | 0.521470–0.524558 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side-reciprocal/clustered-edit/interior | 384 | 64 | 0.136593 | 0.001024 | 0.135865–0.137137 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side-reciprocal/linear-read | 384 | 64 | 1.003681 | 0.007102 | 1.001967–1.007048 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side-reciprocal/logical-projection | 384 | 64 | 1.072480 | 0.009568 | 1.069107–1.076214 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side-reciprocal/random-read | 384 | 64 | 1.005180 | 0.007677 | 1.003852–1.008315 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side-reciprocal/snapshot | 384 | 64 | 1.047930 | 0.024356 | 1.029845–1.061606 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side/clustered-edit/boundary | 384 | 64 | 1.910988 | 0.014189 | 1.906979–1.917035 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side/clustered-edit/interior | 384 | 64 | 7.321339 | 0.055336 | 7.292028–7.360544 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side/linear-read | 384 | 64 | 0.996364 | 0.007080 | 0.992783–0.998045 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side/logical-projection | 384 | 64 | 0.932506 | 0.008318 | 0.929884–0.935486 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side/random-read | 384 | 64 | 0.994847 | 0.007597 | 0.991773–0.996171 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| distribution/mixed/section-side/snapshot | 384 | 64 | 0.954283 | 0.022385 | 0.941969–0.971128 | distribution-scoped observation; remaining path components use the corresponding aggregate definition |
| section-side-reciprocal/clustered-edit | 3072 | 256 | 0.312101 | 0.030599 | 0.308839–0.315040 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/clustered-edit/boundary | 1536 | 256 | 0.463524 | 0.041538 | 0.459618–0.519057 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/clustered-edit/interior | 1536 | 256 | 0.153203 | 0.022055 | 0.137563–0.166963 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/linear-read | 1536 | 256 | 0.998553 | 0.011122 | 0.996587–1.000792 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/logical-projection | 1536 | 256 | 1.004349 | 0.064229 | 0.998178–1.013577 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/random-read | 1536 | 256 | 1.004933 | 0.009594 | 1.003742–1.007330 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/read | 3072 | 256 | 1.003659 | 0.006435 | 1.002708–1.004458 | paired lower-is-better side16/side32 duration ratio |
| section-side-reciprocal/snapshot | 1536 | 256 | 1.024083 | 0.497056 | 0.979431–1.056834 | paired lower-is-better side16/side32 duration ratio |
| section-side/clustered-edit | 3072 | 256 | 4.085404 | 0.756198 | 4.000252–4.435248 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/clustered-edit/boundary | 1536 | 256 | 2.157405 | 0.190751 | 1.924207–2.175720 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/clustered-edit/interior | 1536 | 256 | 6.568282 | 1.056887 | 5.989343–7.271113 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/linear-read | 1536 | 256 | 1.001455 | 0.010986 | 0.999216–1.003242 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/logical-projection | 1536 | 256 | 0.995739 | 0.060274 | 0.987228–1.001836 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/random-read | 1536 | 256 | 0.995093 | 0.009498 | 0.992576–0.996316 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/read | 3072 | 256 | 0.996359 | 0.006426 | 0.995564–0.997300 | paired lower-is-better one-side32/eight-side16 duration ratio |
| section-side/snapshot | 1536 | 256 | 0.978131 | 0.478282 | 0.950535–1.027004 | paired lower-is-better one-side32/eight-side16 duration ratio |
| fresh-process-memory/homogeneous/one-side32-vs-dense | 9 | 9 | 0.000793 | 0.000000 | 0.000793–0.000793 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/homogeneous/eight-side16-vs-dense | 9 | 9 | 0.004638 | 0.000000 | 0.004638–0.004638 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/homogeneous/section-side | 9 | 9 | 0.171081 | 0.000000 | 0.171081–0.171081 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/homogeneous/section-side-reciprocal | 9 | 9 | 5.845200 | 0.000000 | 5.845200–5.845200 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/layered/one-side32-vs-dense | 9 | 9 | 0.066329 | 0.000000 | 0.066329–0.066329 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/layered/eight-side16-vs-dense | 9 | 9 | 0.048084 | 0.000000 | 0.048084–0.048084 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/layered/section-side | 9 | 9 | 1.379440 | 0.000000 | 1.379440–1.379440 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/layered/section-side-reciprocal | 9 | 9 | 0.724932 | 0.000000 | 0.724932–0.724932 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/mixed/one-side32-vs-dense | 9 | 9 | 0.202892 | 0.000000 | 0.202892–0.202892 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/mixed/eight-side16-vs-dense | 9 | 9 | 0.309251 | 0.000000 | 0.309251–0.309251 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/mixed/section-side | 9 | 9 | 0.656078 | 0.000000 | 0.656078–0.656078 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/mixed/section-side-reciprocal | 9 | 9 | 1.524210 | 0.000000 | 1.524210–1.524210 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/highentropy/one-side32-vs-dense | 9 | 9 | 1.000793 | 0.000000 | 1.000793–1.000793 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/highentropy/eight-side16-vs-dense | 9 | 9 | 1.006346 | 0.000000 | 1.006346–1.006346 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/highentropy/section-side | 9 | 9 | 0.994482 | 0.000000 | 0.994482–0.994482 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/highentropy/section-side-reciprocal | 9 | 9 | 1.005548 | 0.000000 | 1.005548–1.005548 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/homogeneous-layered-mixed/one-side32-vs-dense | 9 | 9 | 0.090005 | 0.000000 | 0.090005–0.090005 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/homogeneous-layered-mixed/eight-side16-vs-dense | 9 | 9 | 0.120658 | 0.000000 | 0.120658–0.120658 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/balanced/section-side | 9 | 9 | 0.928737 | 0.000000 | 0.928737–0.928737 | paired fresh-process retained-memory ratio; lower is better |
| fresh-process-memory/balanced/section-side-reciprocal | 9 | 9 | 1.076731 | 0.000000 | 1.076731–1.076731 | paired fresh-process retained-memory ratio; lower is better |

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
| homogeneous | eight-side16 / BoundaryClusters | 64 | 66727 | 39304 | 46656 |
| homogeneous | eight-side16 / InteriorClusters | 64 | 9016 | 5832 | 5832 |
| homogeneous | one-side32 / BoundaryClusters | 64 | 66360 | 39304 | 39304 |
| homogeneous | one-side32 / InteriorClusters | 64 | 66360 | 39304 | 39304 |
| layered | eight-side16 / BoundaryClusters | 64 | 66766 | 39304 | 46656 |
| layered | eight-side16 / InteriorClusters | 64 | 9039 | 5832 | 5832 |
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
| EightSide16Adaptive | Layered | 3125 | 9 / 0 | 19700064 | 13050000 |
| EightSide16Adaptive | Mixed | 3125 | 9 / 0 | 126700064 | 83200000 |
| EightSide16Adaptive | HighEntropy | 3125 | 9 / 0 | 412300064 | 409600000 |

## Manifest

Observed host/runtime: Fedora Linux 43 (KDE Plasma Desktop Edition) / .NET 10.0.11 / SDK 10.0.400.
CPU/power/GC: AMD Ryzen 7 5800U with Radeon Graphics / platform-profile=balanced; cpu-governor=powersave / server GC=False, latency=Interactive.
Source: 8670ac2454f8f503a11184b96af6347f63e0a0aa; dirty=False; source hash=4a23daf2b88890ee4977e04428ae543d0bb376c414f630fc490301ad9b45582d.

The companion JSON contains summary metadata and a SHA-256 reference to the bounded raw NDJSON observation artifact.
