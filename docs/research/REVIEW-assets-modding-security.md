# Adversarial review: asset, modding, and content-agreement security

Status: Review complete  
Review date: 2026-08-09  
Role: adversarial cross-system reviewer, not original author

> Snapshot note: this review preserves findings against the first-wave briefs. Its
> accepted corrections have since been applied. Present-tense conflict descriptions
> and the edit matrix are historical red-team evidence; current authority is
> [`INTEGRATION-RESOLUTIONS.md`](INTEGRATION-RESOLUTIONS.md) plus the proposed
> requirements baseline.

## Scope and verdict

This review treats the following as one proposed system rather than as independent briefs:

- [`ASSET-01`](../decisions/ASSET-01-packaging-and-namespaces.md) through [`ASSET-05`](../decisions/ASSET-05-procedural-assets.md);
- [`MOD-01`](../decisions/MOD-01-client-mod-runtime.md) through [`MOD-03`](../decisions/MOD-03-extension-api-stability.md);
- [`NET-09`](../decisions/NET-09-client-content-agreement.md);
- [`ARCH-05`](../decisions/ARCH-05-server-plugin-boundary.md);
- [`FOUNDATION-00`](../decisions/FOUNDATION-00-spec-risk-audit.md) and the original [`design_doc.md`](../../design_doc.md).

The broad direction is sound: resource data must not be mounted into `res://`; native .NET must be called fully trusted; untrusted executable extensions need a real sandbox; gameplay authority remains on the server; package resolution and override order must be explicit. Those principles are worth preserving.

The documents are **not yet one implementable contract**. Four P0 conflicts must be resolved first:

1. `.vcpak` is declared resource-only in `ASSET-01`, while `ASSET-02`, `MOD-01`, and `ARCH-05` imply that the same package vocabulary or artifact may contain data, Wasm, and native code.
2. `ASSET-01` defines a logical-content digest, while `NET-09` alternates between literal artifact bytes and a canonical archive and incorrectly calls a client hash response proof of possession.
3. `ASSET-03` and `ASSET-04` specify incompatible glTF profiles, marker ownership, material behavior, and missing-clip policy.
4. `MOD-01`/`MOD-02` assume optional WIT imports can simply remain unlinked. A component's imports must be fulfilled; the current WIT contract does not make an imported capability optional in that way.

Two additional P0 security gaps are not explicit contradictions but would create ambient authority:

- mod storage is keyed mainly by package ID, allowing a different artifact that later claims the same ID to inherit another mod's data unless update lineage is separately authorized;
- Wasm execution is metered, but compilation and deserialization are not. A small hostile component can attack compile-time CPU/memory, and a downloaded precompiled Wasmtime artifact must never be deserialized as if it were ordinary validated Wasm.

No implementation should begin by trying to support all package classes. Greenlight the resource-only baseline first, then gate sandboxed Wasm on a hostile prototype. Keep native plugins in a separate, unmistakably trusted installation path.

## Decision classification

“Greenlight candidate” below means the core choice can be approved after the named contract corrections; it does not mean the current prose can be implemented verbatim.

| Brief | Classification | Adversarial disposition |
| --- | --- | --- |
| `ASSET-01` | **Greenlight candidate** | Keep ZIP/directory equivalence, typed names, custom VFS, logical digest, and disposable cache. Add a normative ZIP profile, developer-directory symlink policy, parser isolation rules, and resource-only artifact scope. |
| `ASSET-02` | **Experiment-gated** | Keep explicit dependencies and whole-asset overrides. Formalize the resolver and total order, collapse overlapping version axes, choose one canonical lock encoding, and stop implying executable components fit the resource manifest. |
| `ASSET-03` | **Experiment-gated** | Keep cuboid descriptors and validated GLB-to-private-runtime compilation. It needs a measured importer/backend spike and must become the sole model/animation source of truth. |
| `ASSET-04` | **Reject as a separate decision** | Its animation separation is good, but it duplicates and contradicts `ASSET-03`. Merge its texture-animation material into a narrower brief and delete/supersede the conflicting GLB/runtime clauses. |
| `ASSET-05` | **Defer** | Built-in shader templates are reasonable later. For v1, generators should be authoring tools whose raster outputs are packaged, not client-side required-content compilers. |
| `MOD-01` | **Experiment-gated** | The trust split is correct. Wasmtime Component Model hosting from C#, transitive imports, compile-time containment, platform coverage, and actual resource limits are unresolved gates. |
| `MOD-02` | **Experiment-gated** | The capability principles are strong. Optional imports, capability/version identity, storage ownership, UI spoofing, capability composition, and host-call accounting need executable tests. |
| `MOD-03` | **Greenlight candidate** | Greenlight immutable views, commands, host scheduling, lifecycle freeze, and no mixins. Do not freeze ABI 1.0; align handler order and remove v1 wall-clock/I/O implications. |
| `NET-09` | **Greenlight candidate after a P0 rewrite** | Greenlight exact lock comparison for cooperating clients. Reject the words “prove possession,” unify digest fields with `ASSET-01`/`02`, and make native-mod policy explicitly non-enforceable. |
| `ARCH-05` | **Experiment-gated** | Greenlight the owner-aware command/event boundary in principle. The Wasm host and all-or-nothing world/plugin transaction model must be demonstrated before promising safe server plugins. |

## Evidence audit

The original briefs generally use good primary sources, but some sourced facts, project inferences, and prototype hypotheses are blended together in conclusions. The following distinctions should be carried into the revised decisions.

### Source-backed facts

- Microsoft states that `AssemblyLoadContext` provides **no security features** and loaded code has full process permissions. This conclusively places native .NET outside the capability sandbox ([Microsoft API documentation](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext)).
- Wasmtime's boundary is explicit imports/exports plus bounds-checked linear memory; this does not make host functions, runtime versions, compilation, queues, or embedder allocations automatically safe ([Wasmtime security model](https://docs.wasmtime.dev/security.html)).
- WASI describes a component as starting without ambient authority, but authority appears as soon as the host links an interface or provides a resource ([WASI security introduction](https://wasi.dev/)).
- WIT versions the **package** with a full SemVer value, while a world is a named contract inside that package. A spelling such as `vibecraft:client-mod@0.1.x` is not a valid exact WIT package version. World imports describe dependencies that must be fulfilled ([WIT reference](https://component-model.bytecodealliance.org/design/wit.html), [WIT worlds](https://component-model.bytecodealliance.org/design/worlds.html)).
- The current WIT specification text discusses optional exports as future work; it does not justify treating arbitrary imported capability interfaces as optional and leaving them unresolved ([WIT specification](https://github.com/WebAssembly/component-model/blob/main/design/mvp/WIT.md)).
- The public `wasmtime-dotnet` API documentation inspected for this review is centered on core `Module`/`Linker` APIs and does not document the Rust-style Component Model host surface. Meanwhile, the official C# component toolchain remains preview tooling. This is evidence for a prototype gate, not proof that no viable bridge exists ([wasmtime-dotnet API](https://bytecodealliance.github.io/wasmtime-dotnet/), [C# component tooling](https://component-model.bytecodealliance.org/language-support/building-a-simple-component/csharp.html), [Bytecode Alliance NuGet profile](https://www.nuget.org/profiles/bytecodealliance)).
- Wasmtime is an actively patched security dependency. Its 2026 release history includes sandbox-escape, component string-transcoding, table, and host-data-leakage fixes; a pinned runtime plus update process is mandatory ([Wasmtime 43.0.1 release notes](https://github.com/bytecodealliance/wasmtime/releases/tag/v43.0.1)).
- Wasmtime's native serialized module/component form is not an untrusted interchange format. The Rust API marks deserialization unsafe because malicious serialized native code can lead to arbitrary code execution. VibeCraft must compile standard Wasm/component bytes itself and treat any compiled cache as private trusted cache state ([Wasmtime `Module` documentation](https://docs.rs/wasmtime/latest/wasmtime/struct.Module.html#method.deserialize)).
- glTF defines a right-handed, +Y-up, +Z-forward, meter-based system; names are not guaranteed unique; GLB can still reference external resources; and GLB permits zero or one BIN chunk. A VibeCraft GLB subset therefore needs explicit restrictions rather than the phrase “supports glTF” ([glTF 2.0 specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html)).
- Godot can parse glTF from a buffer, uses a base path for external resources, and can generate a scene. That is useful functionality but also demonstrates why the original untrusted bytes must not be passed to a broad Godot importer after only superficial validation ([Godot `GLTFDocument`](https://docs.godotengine.org/en/stable/classes/class_gltfdocument.html), [runtime loading](https://docs.godotengine.org/en/stable/tutorials/io/runtime_file_loading_and_saving.html)).
- .NET's archive APIs do not supply application-specific aggregate size, count, path, or work policy for untrusted archives; entry names, comments, and extra fields remain untrusted metadata ([Microsoft archive guidance](https://learn.microsoft.com/en-us/dotnet/standard/io/zip-tar-best-practices)).
- Hashing/signing JSON requires one invariant encoding. RFC 8785 is one concrete option and rejects duplicate properties, invalid Unicode, NaN, and infinity while defining deterministic property order and UTF-8 output ([RFC 8785](https://www.rfc-editor.org/rfc/rfc8785.html)).

### Engineering inferences

- A client sending the expected hash does **not** prove it possesses or runs those bytes. A hostile client can echo a public digest. The handshake is useful agreement checking for a cooperating client, not attestation.
- Feeding a validated-but-original GLB to `GLTFDocument.GenerateScene` creates a parser differential and reintroduces broad scene/import semantics. Compile the accepted subset into owned arrays/tables and publish those artifacts through narrow Godot adapters.
- A single archive schema spanning inert resources, Wasm, and native DLLs creates accidental execution paths and confusing UI. Shared package IDs and dependency vocabulary do not require a shared artifact parser or extension.
- Package ID alone is not a durable security principal. Storage inheritance needs an installation/update lineage or publisher identity approved by the user/operator.
- Logical content equality and literal artifact equality are both useful, but for different jobs. Using one field name for both is a security and diagnostics bug.

### Prototype hypotheses, not decisions

- The concrete 1 ms, 2 ms, 4 ms, 5 ms, memory, handle, and package-count limits throughout the briefs are starting test values.
- A Wasmtime C-API bridge, direct future .NET component host, core-Wasm fallback ABI, or out-of-process host could each win the runtime spike.
- Cross-platform byte-identical procedural texture generation, 1,000-entity animation within 2 ms, 100,000-asset resolution within 250 ms, and 32-mod aggregate budgets remain benchmark hypotheses.
- Fuel can bound deterministic Wasm work units, but it cannot promise a wall-clock duration across machines or runtime versions.

## P0 findings and actionable resolutions

### P0-1: Package taxonomy is contradictory and can turn an asset install into code execution

`ASSET-01` says `.vcpak` is not executable and rejects DLLs and new top-level roots. `ASSET-02` nevertheless reserves `components` values for `data`, `sandboxed_mod`, and `trusted_native`. `MOD-01` refers to “the package manifest” for Wasm/native artifacts, and `ARCH-05` says one package may contain data plus client/server Wasm.

This is not merely terminology. If a resource-pack loader and mod loader share an extension or permissive manifest, a pack selected for its textures can cross into an executable path after a schema/version change. It also makes server lock policy, installation warnings, and file-association behavior ambiguous.

**Resolution: use one resolver vocabulary with distinct artifact classes and parsers.** The minimum contract is:

| Artifact class | Extension and manifest | Permitted payload | Execution/trust | Session lock treatment |
| --- | --- | --- | --- | --- |
| `resource_pack` | `.vcpak`, `pack.json` | Strict `assets/` tree only | Inert data, though parsers remain attack surface | Exact logical digest when required |
| `sandbox_component` | `.vcmod`, `mod.json` | One standard Wasm component plus bounded metadata; no native/precompiled code | Untrusted, only after sandbox gate | Exact logical digest, side, selected ABI, and required grants |
| `data_pack` | Future separate contract owned with `GAME-01` | Declarative authoritative registries/recipes/etc. | Server-validated data | Separate gameplay-content lock rules |
| `native_plugin` | Operator/user-controlled directory and `native-plugin.json` | .NET assemblies and dependencies | Full process trust | Never a remotely enforceable client requirement; local diagnostic hash only |

One resolver may understand `(artifact_kind, package_id, version, digest, requires)` across these classes. It must dispatch to a class-specific parser and fixed root allowlist before reading payloads. Cross-kind overrides are forbidden. A `.vcmod` that needs art depends on a `.vcpak`; it does not embed a resource tree in v1.

Use one canonical enum everywhere. Current terms drift among `resources`, `resource`, `data_pack`, `data`, `sandboxed_mod`, `sandboxed_wasm`, `sandboxed`, `native_plugin`, `trusted_native`, and `trusted_native_dotnet`. The table above is the recommended v1 vocabulary. Likewise, use singular `AssetKind` values (`texture`, `material`, `model`, `geometry`) in manifests even though physical directories are plural.

### P0-2: Content identity is internally inconsistent, and hash comparison is not proof

`ASSET-01` deliberately hashes a sorted logical map of uncompressed files, excluding ZIP order, timestamps, compression, and extra fields. `ASSET-02` correctly calls that `content_digest_sha256` and separately allows `archive_sha256`. `NET-09` instead uses `content_sha256`, calls it a hash of “canonical package bytes,” includes `byte_length`, and says the client “proves possession of the exact required package bytes.”

Adopt these names and never alias them:

| Field | Definition | Purpose |
| --- | --- | --- |
| `logical_content_sha256` | `ASSET-01` domain-separated digest over the validated logical map | Compatibility, override resolution, cache source identity |
| `artifact_sha256` | SHA-256 of the literal downloaded `.vcpak`/`.vcmod` bytes | Download integrity, repository metadata, support diagnostics |
| `artifact_length` | Literal artifact byte length | Download integrity and anti-truncation only |
| `lock_sha256` | Hash of one normative canonical lock encoding, excluding this field | Session/profile identity |
| `compiled_cache_key` | Logical digest plus compiler/runtime/platform/profile versions | Local cache only; never package identity |

For v1, use RFC 8785 canonical JSON for the lock, constrain every integer to the interoperable safe range, use strings for version/ID/digest values, and include no floats. This is more actionable than “a specified deterministic binary or canonical JSON encoding.” A later schema may choose another encoding under a new domain/version.

The handshake language should be: **“The cooperating client resolves and validates its selected artifacts, reports the resulting lock digest, and the server compares it with the required lock.”** The transcript must bind that digest to the authenticated session. It detects ordinary mismatch; it does not prove possession, execution, an unmodified client, absence of extra mods, or honest capability enforcement.

Sign repository metadata over both the literal artifact hash/length and logical digest when distribution is designed. This prevents two different containers with the same intended map from becoming an unnoticed parser-differential substitution while retaining logical compatibility semantics.

### P0-3: `ASSET-03` and `ASSET-04` define two incompatible animation formats

The conflicts are direct:

| Topic | `ASSET-03` | `ASSET-04` | Resolution |
| --- | --- | --- | --- |
| Container | GLB only | GLB preferred; `.gltf` accepted | GLB only in v1 |
| URIs/images | All external/data/file/http URIs and images rejected | Package-relative URIs and texture references allowed | One embedded BIN, no URIs/images |
| Materials | GLB materials are unique slot labels; factors/textures ignored | PBR approximation may be reported | Slot labels only; VibeCraft material descriptors own appearance |
| CUBICSPLINE/morph | Included in supported v1 profile | Capability-gated | Gate both initially; greenlight separately after fixtures |
| Missing clips | Missing references generally fail compile | Explicit fallback chain, but no schema for it exists | Fail required clips in v1; add fallback only with a concrete schema |
| Markers | Stored in animation graph | Also appear in compiled `Clip` | Author markers in graph only; compile into clip-time tables |

`ASSET-03` should become normative. `ASSET-04` should be superseded as a model/clip decision; salvage only texture-animation semantics into a smaller descriptor decision.

The normative GLB path should say:

- one JSON chunk and exactly one BIN chunk for accepted model assets;
- exactly one buffer with no URI; no images, textures, cameras, lights, audio, scripts, or broad Godot resources;
- explicit accept/reject rules for sparse accessors, `extras`, unknown optional extensions, unknown GLB chunks, primitive modes, missing tangents/normals, matrices, and morphs;
- unique names only where a VibeCraft descriptor references them;
- no original GLB is passed to Godot's scene generator. The validated importer emits plain vertex/index/skin/clip tables, and a narrow backend builds VibeCraft-owned resources;
- no presentation marker can invoke a generic mod event. V1 markers select a fixed host-owned cosmetic operation with per-instance/global rate limits.

Rejecting otherwise valid optional GLB chunks/extensions is acceptable as a VibeCraft application profile, but say so explicitly rather than claiming unrestricted glTF conformance.

### P0-4: Optional capability linking is not implementable as written

`MOD-02` says an unknown optional capability “remains unlinked and is reported to the module through startup metadata.” A component importing that interface cannot instantiate unless something fulfills the import. WIT package versions are exact full SemVer values; `vibecraft:client-mod@0.1.x` also confuses a package ID with a world and uses a range where WIT syntax expects a version.

Use this v1 shape:

- WIT package: exact `package vibecraft:client-host@0.1.0;`.
- Worlds inside it: `world client-mod` and a separate `world server-plugin`, or separate packages if the toolchain requires it.
- Manifest field: `mod_abi` is a resolver range over host package versions; the lock records one selected exact version.
- The component imports one small mandatory `capability-broker`/lifecycle interface. The host always fulfills it.
- After policy resolution, `open-capability(capability-id, requested-scope)` returns either a typed host resource/grant or a stable denial. Every operation still checks the resource owner, generation, scope, and quotas.
- Alternatively, publish a small finite set of complete WIT worlds. Do not generate arbitrary world variants per permission combination and do not rely on unresolved optional imports.
- Capability IDs omit embedded versions (`core.log`, not `core.log@1`); `selected_version` is a separate field. This removes the current double-version representation in `CapabilityRequest`.
- Audit the component's **complete transitive import set after toolchain adaptation**. A C# guest that silently imports WASI clocks, random, filesystem, or CLI interfaces fails the v1 profile unless each import is intentionally virtualized and reviewed.

This broker is not ambient authority if the only resources it returns are the immutable grants already approved by local/operator policy. Merely knowing a capability's name grants nothing.

### P0-5: Package ID is not a safe storage principal

`MOD-01` scopes client storage by package ID and server identity; the other mod briefs likewise use package namespaces. Hashes do not establish publisher identity, and package IDs are self-asserted. If package `example.map` is removed and a different artifact later claims that ID, automatic inheritance exposes the first package's stored data. The same problem occurs when an unsigned “update” replaces an installed package.

Define two different identities:

- `PackageId`: public dependency/content name.
- `PrincipalId`: host-created storage/authority lineage, distinct for each artifact component and side.

For signed packages, an operator/user-approved publisher key plus package ID can anchor update lineage. Without signatures, installation creates a random local principal, and a changed digest/version inherits it only through an explicit upgrade approval. A server-required package cannot claim an existing local principal merely by naming the same ID.

Client storage scope becomes `(local profile, authenticated server identity or local-world identity, PrincipalId, component_id)`. “Server identity” must mean a pinned/authenticated key or explicit connection-profile identity, not just a display hostname supplied by the peer. Server plugin storage similarly requires an operator-approved package transition recorded in the world manifest before new code inherits old records.

Do not place user credentials or general secrets in mod storage. Capability request `reason`, package titles, URLs, logs, and metadata are untrusted display strings and must be sanitized; host-written capability explanations remain authoritative.

### P0-6: Wasm runtime limits omit hostile compilation and unsafe native caches

Fuel and epoch deadlines start after code has been compiled and entered. They do not bound component validation, adapter generation, Cranelift compilation, debug/custom-section processing, or cache deserialization. A 16 MiB cap is useful but not a proof of bounded compile work.

Required v1 controls:

- Accept only standard core Wasm/component bytes in `.vcmod`; reject WAT and Wasmtime serialized native artifacts.
- Compile in a killable helper process with OS memory/CPU/deadline limits, or demonstrate equivalent containment. Cache only the helper's output in a private engine-owned location.
- Never call `Module.Deserialize`/component deserialization on package bytes. A compiled cache entry is accepted only when it was produced by the exact runtime/configuration, is tied to the source digest, and is stored under a trust model that prevents package-controlled replacement; otherwise recompile standard Wasm.
- Cap type/function/table/memory/import/export/custom-section counts independently of file bytes. Disable threads, shared memory, memory64, relaxed SIMD, unsupported proposals, and multiple memories unless separately greenlit.
- Count host-side binding allocations, component string transcoding, resource tables, and adapter instances; linear-memory limits do not cover them.
- Treat compiler/runtime upgrade as a security release. Re-run the full hostile corpus and invalidate private compiled caches.

For authoritative server plugins, avoid floating-point values in durable commands where practical. Use integers/fixed-point or canonicalize at the host boundary, reject NaN/infinity, disable relaxed SIMD, and define replay compatibility as requiring the same ABI/runtime policy. Fuel exhaustion may stop a required plugin, but must never become an ordinary branch that silently changes gameplay.

### P0-7: “All-or-nothing callback” is not supplied by command staging alone

`ARCH-05` and `MOD-02` promise that invalid output or a trap discards the full callback's commands and storage. That is true before publication. They also imply that a batch of accepted world commands plus plugin storage can commit atomically. `WORLD-04` database transactions do not automatically roll back already-mutated in-memory world/entity/inventory state.

The server prototype must implement one of these explicit semantics:

1. validate the entire command batch against one immutable revision, reserve all resources, and apply through non-failing commit primitives; or
2. build a reversible in-memory transaction/journal and roll back every mutation if any command fails.

For v1, prefer the first. One callback produces one batch; any stale precondition/conflict rejects the whole batch before mutation. No external I/O, nested plugin callback, packet send, or irreversible side effect occurs during apply. `After*` events and presentation messages are emitted only after commit. Durable database persistence may lag the in-memory tick under `WORLD-04`, but the save snapshot must include world and plugin records from the same committed revision.

Synchronous `Before*` policy hooks should return only small typed policy decisions, not world commands. Keep their catalog tiny because required policy code sits on the authoritative deadline.

### P0-8: The asset parser boundary is narrower than Godot, but not yet narrow enough

The custom VFS prevents path lookup and `res://` replacement, which is good. “Non-executable resource pack” must not be read as “safe”: ZIP, JSON, PNG, Ogg, WOFF2, and GLB decoders are code processing attacker bytes.

Add these normative controls to `ASSET-01`:

- Production and multiplayer use packaged artifacts only. Unpacked developer mode is explicitly trusted-local, or it must reject symlinks/reparse points and snapshot by opened file identity to prevent check/use replacement races.
- Define an accepted ZIP profile: EOCD/central-directory bounds, local/central header consistency, non-overlapping entry data, exact methods/flags, CRC verification, exact decompressed length, ZIP64/data-descriptor policy, trailing/prepended data policy, raw filename decoding, NUL/control rejection, and bounded extra fields/comments.
- Continue rejecting extraction. Bound decompressed bytes **and work/time**, because a 2 GiB allowed output can still stall a client even when memory stays bounded.
- Validate file magic and inner dimensions/counts before allocation. `pack.png` preview, license text, author strings, and URLs receive the same untrusted-input treatment and are not rendered as active markup.
- Decode/compile on bounded workers with global concurrency and cancellation. Publish only plain validated artifacts. No media decoder gets filesystem, resolver enumeration, scene tree, network, or world handles.
- Never invoke Godot editor import scripts, mount a PCK, deserialize `.tres/.res`, or call broad glTF scene generation on original package bytes.
- Treat custom WOFF2 fonts as deferred until the font/shaping parser corpus and UI spoof policy exist. The minimum third-party v1 media profile can be PNG, Ogg, and strict JSON, with GLB behind its own experiment.

The logical digest is meaningful only if the packager, server tooling, and runtime agree on the same accepted logical map. Differential fixtures must include malformed containers on which common ZIP readers disagree.

## P1 findings

### P1-1: Version axes are over-coupled

`ASSET-02` currently has package SemVer, `manifest_schema`, exact engine `asset_api` in the lock, individual descriptor schema majors, named capabilities, the game build, and a lock schema. Most are individually defensible, but their responsibilities overlap.

The exact engine asset API must not be part of compatibility identity. A dedicated server may not even include a renderer, and two client engine builds can implement the same selected contract. The lock should record:

- `lock_schema`;
- package ID/version/kind/logical digest and resolved order;
- selected **asset contract major/minor** required by the pack set;
- exact selected optional capability versions;
- selected sandbox ABI for executable artifacts.

Compiler version, Godot version, GPU profile, and platform stay in private cache keys. The engine separately advertises which contract/capability ranges it implements. Marketing/game build is diagnostic, not content identity.

Descriptor schema IDs still need their own major because a descriptor must be parseable in isolation, but avoid making every additive field a new independent negotiation dimension. Unknown required semantics reject; optional visual behavior needs a typed fallback.

### P1-2: The resolver is deterministic in intent, not yet in algorithm

“Choose the highest stable version,” optional installed dependencies, a dependency DAG, root user order, and lexical diagnostic tie-breaks do not fully specify a solver or total precedence order. Different backtracking choices can select different valid solutions. Optional dependencies also make output depend on the local install set, although the resulting lock records that choice.

Required changes:

- Define a complete deterministic solver, including candidate ordering, backtracking, prerelease semantics, conflict explanation, and maximum graph/search budgets.
- Resolve only while creating/updating a profile. Multiplayer runtime consumes an exact lock and does not re-solve ranges.
- Give every selected package one total `load_index` in the lock. A stable Kahn topological sort may use explicit root profile order and then package ID as the mechanical tie-break; tie-break position alone must not authorize an override.
- Continue requiring direct dependency plus explicit target/rule for an override. If two transitive overlays target the same key and no explicit top-level ordering relates them, fail.
- Remove self-declared handler “manifest priority.” Extension handler order should be `(phase, resolved package load_index, handler_id)`. Animation-transition priority is unrelated and may remain local to an animation graph.
- Pin a single SemVer parser and publish boundary fixtures. Do not let NuGet, npm, Cargo, or a loose home-grown interpretation silently define package semantics.

The current whole-asset replacement rule is a good v1 security choice. Do not add generic merge, deletion, or inheritance until a type-specific use case has conflict semantics and provenance reporting.

### P1-3: Asset-level deterministic choices need pinned byte semantics

`ASSET-03` seeds weighted variants from world seed, position, state, and asset revision, but does not choose a hash/PRNG, byte order, coordinate encoding, weight sum limits, or modulo-bias rule. Pin those details before cross-platform golden tests. A cryptographic hash over a domain-separated, length-framed tuple followed by rejection sampling is simple and stable.

Do not include dictionary order, process hash codes, floating-point transforms, archive path casing, or Godot resource IDs. Visual randomness may change when an overriding asset revision changes; that is acceptable and should be documented.

Animation graphs also need positive maximum clip duration, playback speed, marker count per update, transition chain, and event-output rate. A large presentation delta after a stall must not replay thousands of cosmetic markers.

### P1-4: Generic cosmetic events and mod-owned UI are covert authority surfaces

An animation marker described as a namespaced “local visual hook” can become an unreviewed event bridge into executable mods. A resource pack could then invoke expensive or privileged code simply by choosing an event ID. V1 markers should select fixed host operations or events declared by the owning sandbox component and explicitly bound in its manifest. Charge each emitted effect to both the asset instance and receiving module.

`client.ui.owned` can still phish or obscure trusted UI. The host must reserve permission prompts, server identity, native-code warnings, disconnect/error screens, and credential fields. Module UI should show provenance, cannot draw over trusted chrome, cannot synthesize trusted clicks, cannot inspect other UI, and receives text/keyboard focus only through an explicit host interaction. Clipboard, arbitrary URL opening, rich-text links, and password widgets remain absent.

`client.visible-world.read` plus messaging/storage/input can compose into automation or exfiltration. This is not a sandbox escape, but the permission UI and server fairness model must state it. Capability-combination tests matter more than checking each capability alone.

### P1-5: Native client policies in `NET-09` are labels, not controls

`forbid`, `ignore`, and `allowlist` sound enforceable even though the document later admits a cooperating client can lie. Rename them:

- `native_reporting: none` — recommended public-server behavior;
- `native_self_report: requested` — cooperative/private diagnostics only;
- `native_self_report_allowlist` — cooperative launch-profile check only.

No server rejection message should claim “native mods absent.” A modified client can omit them, inject code without a plugin manifest, or modify the base executable. Native plugins are never auto-downloaded, never required as a safe join dependency, and never inherit sandbox capability language.

The same caveat applies to “forbidden extra sandbox mods.” An honest launcher can report modules participating in the current VibeCraft session, but a hostile client can hide one. Requiring a full installed inventory conflicts with the privacy goal and still does not prove execution state. Lock required modules and maintain server authority; do not build v1 policy around absence claims.

### P1-6: Acquisition URLs and package metadata are hostile UI content

`NET-09` permits human-facing acquisition URLs. A malicious or impersonated server can use them for phishing. V1 should transmit package IDs, expected digests, and optional plain-text source hints. If URLs are supported, show the authenticated server identity and destination host, require an explicit click/confirmation, allow only a reviewed scheme such as HTTPS, never fetch/open automatically, and never render package-supplied markup in a trusted dialog.

Package author, title, reason-for-permission, license, log, conversion warning, and path text need length limits, control-character filtering, bidi/isolate-safe rendering, and clear untrusted provenance.

### P1-7: Procedural assets should produce package bytes, not merely cache bytes

`ASSET-05` says required clients agree on source graph bytes/capability but locally generate canonical output. If two compiler implementations or CPUs disagree, the clients have the same lock yet different visible geometry/materials. That can matter for fairness and diagnostics even when it cannot mutate server state.

For v1, the packager runs the bounded graph and puts canonical PNG/material outputs in `.vcpak`; the graph may be retained as non-runtime provenance/source outside the distributed pack. If client-side build/import is later greenlit, the descriptor must include expected canonical output digests and the engine must verify them before publication. Platform-specific GPU compression remains private cache data.

Built-in runtime shader templates are a separate renderer feature, not arbitrary procedural generation. Each template needs bounded parameters, quality fallbacks, batching classification, and GPU budget tests from `RENDER-06`.

### P1-8: Headless content agreement should not parse every media codec

`ASSET-03` implies headless/server validation of IDs and state coverage, which is useful. A dedicated server should not need to decode PNG, Ogg, WOFF2, or GLB merely to compare a lock. Split validation:

- packager/client: full archive, descriptor, reference, media, budget, and renderer-profile validation;
- server profile tool: archive/manifest/digest/resolution validation plus cheap descriptor metadata needed for policy;
- join path: compare pre-resolved lock digests and negotiated capability versions; do not repeatedly parse package media per connection.

If a public server requires exact visual fairness, the operator validates the artifact once while creating the lock and then pins its digest.

## Minimum coherent v1 asset/mod contract

This is the smallest contract that preserves the desired architecture without pretending unresolved mechanisms are finished.

### 1. Resource packs

- Only `.vcpak` with resource-only `pack.json` is shipping scope.
- Allowed initial third-party payload: strict JSON descriptors, PNG textures, Ogg audio, and license/localization text. WOFF2 and GLB remain feature-gated; the first-party pack may exercise them only through the same validator once their corpus passes.
- IDs are lowercase ASCII typed names. Package IDs, namespaces, and `AssetKind` are separate.
- The resolver supports required dependencies and declared whole-asset overlays. Optional dependencies may be omitted from the first server-required profile implementation.
- Profiles resolve to an immutable RFC-8785 lock with one total package order and `logical_content_sha256` for every entry.
- Source packages never contain Godot resources, shaders, code, nested packages, build scripts, or compiled platform artifacts.
- Runtime uses a read-only VFS; validation/compilation yields owned CPU artifacts; main-thread publication alone touches Godot resources.
- Minecraft compatibility is offline conversion into this exact format with a versioned report.

### 2. Models and animation

- Cuboid JSON plus block-state templates are the baseline chunk model path.
- GLB entity/block-entity geometry is experiment-gated and uses the single strict profile from P0-3.
- VibeCraft material descriptors, not GLB material semantics, own rendering.
- Presentation graphs cannot mutate gameplay. The server owns action timing; visual root motion and markers carry no authority.
- Texture animation is a bounded descriptor compiled into material tables; it does not remesh chunks.
- Arbitrary procedural generation, shader source, IK, retargeting, and runtime model scripts are deferred.

### 3. Content agreement

- The server publishes one exact required client-content lock after address validation and inside the authenticated handshake transcript.
- An honest client reports its locally resolved lock digest and selected supported ABI/capabilities.
- Mismatch errors are actionable but do not disclose unrelated local packages.
- This is cooperative configuration agreement, never anti-cheat or remote attestation.
- Native plugin presence/absence is not an enforceable lock property.

### 4. Sandboxed mods

- `.vcmod` is an experimental artifact class until the runtime gate passes.
- It contains standard Wasm component bytes, one manifest, and no native/precompiled code or embedded resource tree.
- One component, one side, one Store/instance/principal is the first prototype. Multi-side bundles and shared package storage come later.
- No general WASI filesystem, network, HTTP, process, environment, wall clock, terminal, or entropy interfaces. Audit all transitive imports.
- The host supplies one fixed lifecycle/capability-broker contract and returns scoped resources after policy approval.
- Callbacks receive immutable bounded values and return staged commands. No Godot/CLR/world object crosses the ABI.
- Compile in a killable bounded helper. Runtime execution receives fuel, epoch deadline, host-work charges, memory/table/handle/queue/storage budgets, and transactional output discard.
- Storage follows approved `PrincipalId` lineage, not package ID alone.
- No public stable ABI until two first-party features survive an internal storage/scheduler refactor.

### 5. Native plugins

- Native .NET is installed through a separate trusted workflow and directory.
- UI says “full access to this process and your account,” not “requested permissions.”
- `AssemblyLoadContext` is dependency separation and best-effort unload only.
- Native plugins are never downloaded or required by a game server as a safe join step.
- A server using native plugins reports that fact diagnostically; it does not claim containment or safe disable.

### 6. Explicitly deferred

- arbitrary HTTP/filesystem capabilities;
- client-side required procedural compilation;
- executable hot reload;
- multi-component client/server/data bundles;
- generic package signing/repository/TUF workflow;
- remote attestation;
- out-of-process native plugin API;
- public ABI 1.0 and plugin marketplace.

## Exact correction matrix

The original decision files were not edited. These are the exact sections that need follow-up.

| Priority | File and section | Required correction |
| --- | --- | --- |
| P0 | [`ASSET-01` — Decision; Container and authoring form](../decisions/ASSET-01-packaging-and-namespaces.md#container-and-authoring-form) | State that `.vcpak` and `pack.json` are resource-only. Point executable/data artifacts to separate future/class-specific contracts. |
| P0 | [`ASSET-01` — Archive and parser security policy](../decisions/ASSET-01-packaging-and-namespaces.md#archive-and-parser-security-policy) | Add the normative ZIP structural profile, work deadlines, decoder-global budgets, raw-name handling, CRC/exact-length checks, and parser-differential policy. |
| P0 | [`ASSET-01` — Container and authoring form](../decisions/ASSET-01-packaging-and-namespaces.md#container-and-authoring-form) | Define symlink/reparse/hardlink and check/use behavior for unpacked directory mode; otherwise label it trusted-local only. |
| P0 | [`ASSET-01` — Read-only resolver and snapshots](../decisions/ASSET-01-packaging-and-namespaces.md#read-only-resolver-and-snapshots) | Forbid passing original untrusted GLB/media to broad Godot scene import; publish from owned compiled tables only. |
| P0 | [`ASSET-01` — Reproducible content digest](../decisions/ASSET-01-packaging-and-namespaces.md#reproducible-content-digest) | Rename the public field `logical_content_sha256`; reserve `artifact_sha256` for literal ZIP bytes. |
| P0 | [`ASSET-02` — `pack.json` schema shape](../decisions/ASSET-02-manifest-and-overrides.md#packjson-schema-shape) | Remove executable/native `components` from resource schema v1. Define shared resolver records separately from artifact manifests. |
| P1 | [`ASSET-02` — Machine identifiers and versions](../decisions/ASSET-02-manifest-and-overrides.md#machine-identifiers-and-versions) | Pin one SemVer implementation and fixtures; normalize singular asset-kind vocabulary across manifests/descriptors. |
| P0 | [`ASSET-02` — Package selection and dependency resolution](../decisions/ASSET-02-manifest-and-overrides.md#package-selection-and-dependency-resolution) | Specify deterministic solving/backtracking limits and one total `load_index`; runtime consumes exact locks rather than re-solving. |
| P1 | [`ASSET-02` — Capabilities and API evolution](../decisions/ASSET-02-manifest-and-overrides.md#capabilities-and-api-evolution) | Collapse overlapping asset API/version responsibilities and distinguish required semantics from local cache/compiler versions. |
| P0 | [`ASSET-02` — Exact content lock and atomic activation](../decisions/ASSET-02-manifest-and-overrides.md#exact-content-lock-and-atomic-activation) | Remove exact engine API from lock, choose RFC 8785 now, and use the digest vocabulary from P0-2. |
| P0 | [`ASSET-03` — GLB-backed model](../decisions/ASSET-03-model-and-animation-contract.md#glb-backed-model) | Become sole normative GLB profile; specify buffers/chunks/sparse accessors/extras/optional extensions and prohibit broad Godot reparse. |
| P1 | [`ASSET-03` — Block visual selection](../decisions/ASSET-03-model-and-animation-contract.md#block-visual-selection) | Pin hash framing, integer encoding, PRNG/rejection sampling, weight limits, and revision semantics for weighted variants. |
| P1 | [`ASSET-03` — Animation graph](../decisions/ASSET-03-model-and-animation-contract.md#animation-graph) | Make markers fixed host-owned cosmetic operations with output caps; specify large-delta/seek deduplication and no generic privileged event bridge. |
| P0 | [`ASSET-04` — Import pipeline; Presentation state graph](../decisions/ASSET-04-animation-runtime.md#import-pipeline) | Supersede these sections with `ASSET-03`; remove `.gltf`, URI, PBR approximation, fallback, morph, and CUBICSPLINE contradictions. |
| P1 | [`ASSET-04` — Texture animation](../decisions/ASSET-04-animation-runtime.md#texture-animation) | Retain as a separate bounded texture-animation descriptor, adding positive duration/frame/output limits. |
| P1 | [`ASSET-05` — Build and runtime flow; Gameplay and networking](../decisions/ASSET-05-procedural-assets.md#build-and-runtime-flow) | For v1, package generated canonical outputs; do not rely on each required client producing the same cache bytes from source alone. |
| P0 | [`MOD-01` — Runtime and artifact classes](../decisions/MOD-01-client-mod-runtime.md#runtime-and-artifact-classes) | Define `.vcmod` separately from `.vcpak`; reject Wasmtime serialized artifacts and native binaries; use class-specific manifests. |
| P0 | [`MOD-01` — ABI and imports](../decisions/MOD-01-client-mod-runtime.md#abi-and-imports) | Correct WIT package/world version syntax; replace unresolved optional imports with a fixed broker or finite world profiles; audit transitive imports. |
| P0 | [`MOD-01` — Provisional runtime limits](../decisions/MOD-01-client-mod-runtime.md#provisional-runtime-limits) | Add compile-time validation/JIT/adapter limits and a killable compilation boundary; mark all numeric budgets as provisional policy. |
| P0 | [`MOD-01` — Persistence](../decisions/MOD-01-client-mod-runtime.md#persistence) | Scope storage by approved principal/update lineage and authenticated server identity, not package ID/hostname alone. |
| P0 | [`MOD-02` — Principal, request, grant, and use](../decisions/MOD-02-capability-security.md#principal-request-grant-and-use) | Make principal identity `(artifact/component/side/instance generation)` and fix optional capability instantiation semantics. |
| P1 | [`MOD-02` — Capability vocabulary](../decisions/MOD-02-capability-security.md#capability-vocabulary) | Separate capability ID from version; add trusted-UI restrictions and review capability combinations such as world-read + message/storage/input. |
| P1 | [`MOD-02` — Persistence security](../decisions/MOD-02-capability-security.md#persistence-security) | Add principal lineage, package takeover tests, server-key scoping, and explicit inheritance approval. |
| P1 | [`MOD-03` — Events](../decisions/MOD-03-extension-api-stability.md#events) | Replace manifest priority with resolved package order and phase; specify multi-policy conflict/composition semantics. |
| P1 | [`MOD-03` — Scheduling and ownership](../decisions/MOD-03-extension-api-stability.md#scheduling-and-ownership) | Mark wall-clock/I/O tasks as future brokered capabilities, not a v1 sandbox surface; trusted plugins are measured, not fuel-contained. |
| P0 | [`NET-09` — Decision](../decisions/NET-09-client-content-agreement.md#decision) | Replace “prove possession/exact bytes” with cooperative resolved-lock reporting and comparison. |
| P0 | [`NET-09` — Canonical lock manifest](../decisions/NET-09-client-content-agreement.md#canonical-lock-manifest) | Reuse `ASSET-02`'s one canonical lock schema and P0-2 digest names; do not duplicate a divergent structure. |
| P1 | [`NET-09` — Negotiation](../decisions/NET-09-client-content-agreement.md#negotiation) | State what honest extras are reported, preserve privacy, bind lock to authenticated transcript, and avoid any absence/attestation claim. |
| P1 | [`NET-09` — Native plugin policy; Distribution and signing](../decisions/NET-09-client-content-agreement.md#native-plugin-policy) | Rename native policies as cooperative self-reporting; harden acquisition URL UI and exclude native code from required safe content. |
| P0 | [`ARCH-05` — Extension tiers and trust](../decisions/ARCH-05-server-plugin-boundary.md#extension-tiers-and-trust) | Remove the v1 assertion that one package contains data plus side-specific Wasm; align with class-specific artifacts until a bundle schema is reviewed. |
| P1 | [`ARCH-05` — Lifecycle and registration](../decisions/ARCH-05-server-plugin-boundary.md#lifecycle-and-registration) | Align handler order with the resolved package `load_index`; remove conflicting manifest-priority language from `MOD-03`. |
| P0 | [`ARCH-05` — Callback, event, and command contract; Persistence and migrations](../decisions/ARCH-05-server-plugin-boundary.md#callback-event-and-command-contract) | Specify prevalidated non-failing command-batch commit or an actual rollback journal; database atomicity alone is insufficient. |
| P0 | [`ARCH-05` — Provisional server budgets](../decisions/ARCH-05-server-plugin-boundary.md#provisional-server-budgets) | Add compile-time containment and mark time-equivalent fuel figures as calibration hypotheses, not guarantees. |

## Prioritized hostile corpus

Every corpus item needs a stable expected classification, maximum CPU/memory/output, and “no partial activation/commit” oracle. Run release builds with sanitizers where available; retain reduced crashing inputs permanently.

### P0-A: Container and path differential corpus

- duplicate central-directory names; case-only and Unicode lookalikes; invalid UTF-8; CP437/UTF-8 flag disagreements; NUL/control bytes; backslashes; absolute, drive, UNC, device, ADS, `.`/`..`, trailing-dot/space names;
- local-header versus central-directory name/method/flags/size mismatches; overlapping entry ranges; entries pointing into the central directory; duplicate or forged EOCD; prepended SFX/polyglot bytes; trailing payloads; truncated descriptors;
- ZIP64 boundary fields, data descriptors, encrypted flags, unsupported methods, unknown/bloated extra fields/comments, symlink/reparse attributes, false CRC/length, zero-length oddities;
- flat deflate bombs, extreme ratios, many tiny entries, slow streams, cancellation at every byte boundary, aggregate checked-overflow cases, and nested archives treated only as rejected/opaque data;
- developer-directory symlink/junction/hardlink replacement and rename races between scan, hash, decode, and cache copy;
- differential execution through the packager, server profile tool, Windows client, and Linux client: all must accept the same logical map or reject with the same class.

### P0-B: Manifest, lock, resolver, and override corpus

- duplicate JSON keys, unknown fields, invalid/lone-surrogate Unicode, over-depth objects, huge strings/arrays, number exponent/rounding edges, negative zero, integers beyond the chosen safe range, NaN/infinity attempted through nonstandard parsers;
- every SemVer comparator/prerelease/leading-zero boundary; duplicate `(kind,id,version)` with same/different digest; package ID confusables and overlengths;
- required/optional cycles, exponential/conflicting constraint graphs, absent/present optional dependencies, graph/search budget exhaustion, shuffled discovery and hash-map order;
- namespace collisions, singular/plural kind mismatches, unauthorized foreign writes, overbroad/empty prefixes, direct versus transitive override targets, two independent winners, and profile order changes;
- RFC-8785 golden locks across C#/independent implementation, one-byte/path/order changes, stale/replayed locks, unknown lock schema, lock-self-field exclusion, and literal-artifact versus logical-content hash confusion.

### P0-C: Media and descriptor parser corpus

- PNG dimensions/chunk lengths/CRC/interlace/palette/decompression/ICC/APNG edge cases and decoded-byte overflow before allocation;
- Ogg page/lacing/packet/duration/channel/sample-rate/granule corruption, decoder stalls, and malformed comment metadata;
- WOFF2 malformed tables, offsets, glyph/composite recursion, shaping blowups, and UI spoof fixtures before custom fonts are enabled;
- JSON descriptor cycles, fan-out bombs, missing/wrong-kind references, duplicate cases, state-space explosion, float NaN/infinity, and extreme animation durations/speeds/markers;
- every accepted asset must be decoded under cancellation and aggregate worker/GPU-publication budgets; a failed snapshot leaves the old snapshot untouched.

### P0-D: GLB profile corpus

- bad header length/version/alignment/chunk order; zero/multiple BIN chunks; unknown chunks; external/data/file/http URIs; embedded images; multiple buffers; sparse accessors; deeply nested `extras`; unknown optional and required extensions;
- accessor offset/stride/count/product overflow, out-of-range indices, non-triangle modes, missing/NaN normals/tangents, non-normalized weights/quaternions, invalid inverse-bind matrices, node/skin cycles, duplicate referenced names;
- animation times unordered/duplicate/huge, CUBICSPLINE output cardinality, morph-target count/size, marker seek/large-delta storms, root motion, and missing clip/slot/socket bindings;
- Khronos validator corpus plus VibeCraft-specific profile failures;
- an assertion that no original hostile GLB reaches `GLTFDocument.GenerateScene` or another broad scene loader.

### P0-E: Wasm validation, compilation, and cache corpus

- malformed core/component binaries, huge type/function/import/export/custom-section graphs under the byte cap, deeply nested components/adapters, compile-time CPU and memory bombs, cancellation, helper crash/hang, and concurrent compile storms;
- threads/shared memory, memory64, multi-memory, relaxed SIMD, unsupported proposals, WAT, serialized Wasmtime module/component artifacts, native libraries, and disguised DLL/ELF/Mach-O payloads;
- transitive imports introduced by Rust/C# toolchains, every WASI filesystem/socket/HTTP/clock/random/CLI/terminal/process interface, version aliases, import name confusables, and unknown VibeCraft interfaces;
- private cache tamper, wrong runtime/config/platform/source digest, stale cache after security update, partial cache write, and package attempt to supply a cache blob;
- run applicable Wasmtime advisory regression fixtures against every runtime update before release.

### P0-F: Canonical ABI and capability corpus

- invalid UTF-8, enum/variant tags, huge/negative/truncated lengths, list multiplication overflow, NaN/infinity, malformed resources, destructor/drop storms, reentrant host calls, callback trap during lowering/lifting, and component string-transcoding edge cases;
- guessed/cross-module/cross-side/cross-server handles, stale generation, use after callback/disable/reinstantiation, borrowed-resource retention, forged package/component IDs, and wrong-owner revision;
- optional capability denied at startup, grant changes requiring reinstantiation, quota-class downgrade/upgrade, capability ID/version mismatch, and toolchain imports not represented in the manifest;
- capability combinations: visible-world + message/storage/input, UI + input focus, world-read + network-like message, registry + override, and admin role + plugin grant;
- every host call traced to principal, grant, scope, owner, quota reservation, validator, typed error, and audit record.

### P0-G: Storage and update-lineage corpus

- uninstall/reinstall same package ID with a different digest; unsigned update; signed key change; package rename; split/merged components; client/server side collision; profile/server identity collision;
- malicious server copying another server's display hostname; authenticated server key rotation; local-to-server scope confusion; package attempting another component's storage;
- migration trap/fuel/deadline/output overflow, corrupt/future schema, rollback after staged write, missing package preservation, explicit cleanup, and interrupted principal transfer;
- expected result: no prior namespace is inherited without an explicit, audited user/operator transition.

### P0-H: Callback transaction and scheduler corpus

- trap or quota failure after staging each command/storage/message position; stale second command in an otherwise valid batch; conflict between two plugin batches; failure during non-failing apply assertion;
- recursive `Before`/`After` events, handler-order shuffle, worker-completion shuffle, disable/reinstantiate during dispatch, owner unload/migration, scheduled payload after package update, and command result after disconnect;
- kill before/after in-memory commit, snapshot capture, database commit, and save acknowledgement; reopen at the last coherent world/plugin revision;
- deterministic replay under dictionary/discovery/worker randomization, with wall-clock watchdog timing allowed to change only availability, never a committed state hash.

### P1-I: UI, metadata, privacy, and handshake corpus

- bidi controls, ANSI/control sequences, giant author/reason/license/log strings, malicious rich text, misleading homoglyph package IDs, URL schemes/redirects/private addresses, and attempts to cover trusted UI;
- server requires a module with sensitive UI/read/input grants; user denies one grant; join fails without partial module activation or hidden fallback;
- lock mismatch, stale transcript, downgrade of ABI/capability, duplicated IDs, abbreviated-hash ambiguity, hidden/extra honest mods, and full-inventory privacy checks;
- modified-client fixture that simply echoes expected hashes, proving tests and UI never claim attestation.

### P2-J: Converter and procedural corpus

- Minecraft Java/Bedrock version mismatch, unsupported extensions, traversal/bombs, missing vanilla dependencies, provenance/report determinism, and no source-edition branch in runtime output;
- generator cycles, DAG fan-out, duplicated references, fixed/integer noise vectors, floating-point divergence, output digest mismatch, cache poisoning, shader-template parameter extremes, and GPU upload storms;
- keep these out of the first playable slice until P0/P1 paths are stable.

## Greenlight sequence

1. **Contract vocabulary gate:** approve artifact classes, canonical field names, digest meanings, asset-kind enum, version responsibilities, principal identity, and handler order. No code format should predate this.
2. **Resource-pack gate:** implement only path/ZIP parsing, logical digest, resource manifest, deterministic resolver/lock, PNG + JSON, and hostile corpus P0-A/B. Greenlight `ASSET-01` only after cross-platform agreement.
3. **Asset runtime gate:** add cuboid/material compilation and narrow Godot publication. Run P0-C. Add GLB only after P0-D and measured entity/chunk fixtures pass; then greenlight the relevant subset of `ASSET-03`.
4. **Content-agreement gate:** implement the cooperative lock handshake, transcript binding, privacy behavior, and mismatch UX. Explicitly demonstrate that a lying client is not detected; that is a correct non-goal, not a failed test.
5. **Sandbox host gate:** compare direct .NET hosting, C-API bridge, core-Wasm fallback, and helper-process options. Require P0-E/F/G across every supported RID before shipping a “safe mod” label.
6. **Server-plugin gate:** implement the prevalidated batch transaction and owner scheduler in an engine-neutral harness. Require P0-H, crash recovery, and two first-party features before public ABI stability.
7. **Deferred-feature gate:** only then revisit custom fonts, procedural assets, multi-component bundles, brokered I/O, signing/repository distribution, and public mod marketplace.

## Final disposition

The research packet has a viable security spine, especially its refusal to call native .NET sandboxed and its server-authority boundary. The largest risk now is not choosing the wrong library; it is letting similar words conceal different contracts: package versus artifact, logical content versus literal bytes, agreement versus attestation, namespace versus principal, import versus granted capability, and staged output versus atomic world mutation.

Resolve those distinctions first. The resulting v1 can be pleasantly small: inert resource packs, deterministic exact locks, cuboid-first assets, cooperative content agreement, and trusted native plugins clearly separated. Sandboxed Wasm remains an excellent target, but only the hostile C#/Wasmtime prototype earns the right to turn “scoped permissions” from a design aspiration into a product claim.
