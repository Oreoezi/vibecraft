# ASSET-05 Procedural asset generation boundary

Status: Proposed

## Decision

Recommended choice: Defer arbitrary runtime procedural assets. Treat the first bounded declarative graph as an **authoring CLI** whose raster outputs are packaged as ordinary resource assets; clients do not need a procedural compiler to join. Later, after the pack compiler is stable, the graph source may become an optional reproducible-build input without becoming required runtime behavior. Keep only a small allowlist of engine-owned runtime shader templates.

One-sentence rationale: Most procedural visual goals do not require executable code during gameplay, and precompilation preserves pack safety, reproducibility, startup time, batching, and renderer control.

## Context and constraints

- The draft wants Perlin/noise-driven textures and possibly procedural materials.
- Packs are external and may be untrusted.
- Multiplayer clients must agree on required content bytes/capabilities.
- Runtime-generated textures can cause frame spikes, GPU memory growth, nondeterminism, and cache invalidation complexity.
- Visual assets must not affect authoritative gameplay.

## Options considered

| Option | Strengths | Costs/risks | Fit |
| --- | --- | --- | --- |
| Arbitrary C#/shader code in packs | Unlimited expression | Full native authority or GPU risk; nonportable; unbounded work | Reject |
| Sandboxed runtime WASM generators | Flexible and constrainable | Startup/runtime cost; complex determinism and GPU upload lifecycle | Defer |
| Declarative bounded graph at import time | Reproducible, cacheable, inspectable | Smaller feature set; compiler needed | Recommended |
| Ship only raster assets | Simplest and safest | Loses compact parameterized authoring | Always-supported fallback |

## Evidence

WASI's capability model starts modules without ambient authority and grants host capabilities explicitly ([WASI introduction](https://wasi.dev/)). Even with memory isolation, a procedural generator still needs fuel, memory, dimensions, output, and execution-time budgets; sandboxing does not make work free or deterministic.

Godot notes that resources/GPU operations from threads can synchronize and stall, so generated image upload should use a bounded publication path rather than arbitrary extension callbacks ([Godot thread-safe APIs](https://docs.godotengine.org/en/stable/tutorials/performance/thread_safe_apis.html)). The glTF ecosystem similarly distinguishes a portable source/interchange contract from engine-specific runtime processing ([glTF 2.0 specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html)).

## Proposed design

### V1 generator graph

Generator source is declarative data with no loops, recursion, file paths outside namespaced inputs, or user code. Initial node allowlist:

- constants and color ramps;
- deterministic seeded value/simplex-style noise implementations pinned by algorithm version;
- arithmetic, clamp, smoothstep, remap;
- transform/sample of declared input textures;
- blend/mask/channel operations;
- normal-map derivation from height;
- tile/wrap and finite blur with a bounded radius;
- outputs for albedo, normal, roughness/metallic/emission/opacity masks.

Graph validation rejects cycles, excessive nodes, dimensions, samples, blur radius, referenced inputs, and output bytes. All math/quantization semantics and noise algorithms are versioned; the same graph/version/seed/input hashes must yield the same canonical output bytes on every supported build platform.

### Build and runtime flow

```text
authoring source + generator graph
    -> validate DAG and budgets
    -> deterministic CPU evaluation in authoring CLI
    -> canonical image outputs
    -> place ordinary PNG/material descriptors into resource-pack source
    -> package validation and normal local compiled cache
    -> ordinary runtime asset references
```

The authoring cache key includes generator-contract version, canonical graph bytes, input hashes, seed, output dimensions/format, color-space choice, and tool version. The distributed package/lock identifies the generated ordinary files; clients do not execute or hash an external graph to reproduce them.

Runtime and the normal pack loader never execute or need the source graph. Generated output is loaded like any other texture. Authoring-tool diagnostics report source node and dependency chains for failures.

### Runtime effects

Moving water/lava, portals, wind, shimmer, and simple noise variation use engine-owned material templates with bounded parameters and textures. Packs may select a template and values but may not inject arbitrary shader source in the default trust tier.

Potential built-in templates:

- frame animation;
- UV scroll/warp;
- two-texture/noise blend;
- emissive pulse;
- deterministic per-instance tint/phase;
- portal/refractive effect selected by renderer capability.

Custom shaders belong to a trusted developer tier or a future separately reviewed shader DSL/capability—not to “resource pack” by default.

### Gameplay and networking

- Generated visual outputs never determine collision, drops, light emission level, visibility authority, or other gameplay values.
- Required clients agree on ordinary package logical bytes, not generator tool availability or platform-specific compressed cache bytes.
- Generator/tool version is provenance in the authoring project and optional build report; it is not a join capability in v1.

## Greenlight criteria

- Graph evaluation is deterministic across supported platforms for golden fixtures.
- Validation bounds CPU operations, dimensions, memory, dependencies, and output size before evaluation.
- Cache keys change for every semantic input/importer change and remain stable for irrelevant archive metadata.
- Runtime frame time is independent of graph complexity after outputs are compiled.
- A malicious graph cannot access filesystem/network/process APIs, recurse, allocate unbounded memory, or trigger unlimited GPU uploads.
- Every generated asset can be replaced by equivalent pre-baked raster inputs.

## Prototype or benchmark

Required: only before procedural generation enters a milestone; not required for the first playable slice.

Build a minimal evaluator for constant, noise, ramp, arithmetic, blend, normal-from-height, and output nodes. Generate tiled 64², 256², and 1024² texture sets; compare output hashes across Windows/Linux and repeated runs. Fuzz malformed graphs, cycles, extreme sizes, deep DAGs, duplicate references, NaN parameters, and cache invalidation.

Initial pass condition: deterministic byte-identical uncompressed outputs; validation under a fixed small time budget; bounded peak memory; cache hit avoids evaluation and upload duplication; a failure yields an actionable graph-node diagnostic.

## Risks and open questions

- Floating-point noise can vary by implementation/platform; use pinned integer/fixed algorithms or canonical quantization where byte identity is required.
- GPU texture compression may differ by platform; it is a derived local cache and should not be the agreement hash.
- A graph language tends to expand into a programming language; keep the v1 node allowlist intentionally small.
- Runtime shader templates still need platform capability fallbacks and material-batching analysis.

## Dependencies

- Requires: `ASSET-01`, `ASSET-02`, `RENDER-06`, `NET-09`.
- Blocks: procedural-pack authoring, not the core gameplay slice.

## Rejected or deferred alternatives

- Arbitrary C# or Godot shader source in untrusted resource packs: rejected.
- Mandatory procedural assets for base-game visuals: rejected; ship compiled raster fallback/base assets.
- Runtime WASM texture generation: deferred until mod sandbox, cache, and upload budgets are proven.
