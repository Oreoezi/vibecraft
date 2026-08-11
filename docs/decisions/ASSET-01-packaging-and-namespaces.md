# ASSET-01 Resource-pack packaging and namespaces

Status: Proposed

Owner: Asset-contract research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Distribute a VibeCraft resource pack as a standard ZIP container with the `.vcpak` extension and the same strict logical tree in an unpacked developer directory; read it through a VibeCraft-owned, read-only virtual filesystem, identify every asset by a typed `namespace:path` key, and compile validated source assets into a disposable content-addressed cache.

One-sentence rationale: ZIP and familiar source formats keep packs authorable and inspectable, while a native resolver, strict canonical paths, bounded streaming validation, and a separate compiled cache avoid inheriting Godot or Minecraft path semantics as VibeCraft's permanent API.

### Owner decision — 2026-08-10

ZIP-based `.vcpak` plus unpacked development directories is accepted. The pack contract
must stay engine-agnostic: Godot imports/caches are private implementation products,
not an asset requirement. Minecraft import is best-effort offline tooling supplied
without any promise of fidelity or even a usable result for a given source pack.

This decision makes three boundaries explicit:

- `.vcpak` is a distribution container, not an executable module and not a Godot project overlay.
- Minecraft packs are input to an offline converter; `pack.mcmeta`, Minecraft directory names, block-model inheritance, and edition-specific behavior are never accepted as native runtime semantics.
- The source pack remains portable. Platform/GPU-specific textures, meshes, atlases, and Godot resources belong in a regenerable cache and are never the authoritative package.

## Context and constraints

- The shipped first-party art must use the same pack contract as third-party art.
- Packs need textures, sounds, localization, static block models, animated entity/block-entity models, and future material/procedural descriptors.
- The Godot/C# client must load untrusted data without letting a pack replace arbitrary `res://` files, load assemblies, escape an archive path, or force unbounded allocation/decompression.
- Pack identity must be stable across Windows and Linux despite filesystem case and Unicode behavior.
- Ordered overlays, compatibility, and lock manifests are defined by `ASSET-02`; this document defines what can be mounted and addressed.
- Visual formats must not become authoritative gameplay formats. Collision, light emission used by gameplay, registry state, and server behavior are outside a resource pack.
- V1 should remain debuggable with normal ZIP/JSON/PNG tools and should not require
  the Godot editor to author a pack. The native model/rig source format is a separate
  open format spike, not a Godot import format.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| Loose directories only | Excellent authoring and hot reload; trivial inspection | Poor distribution integrity; filesystem case/symlink behavior leaks into identity; many-file install overhead | Developer mode only |
| **Standard ZIP plus directory-equivalent layout** | Familiar tools, compression, one distributable file, streamable entries, same authoring tree | Requires strict path/size validation and a custom resolver; ZIP metadata itself is not reproducible | **Recommended** |
| Godot PCK/ZIP mounted into `res://` | Uses Godot's virtual filesystem and imported resources | Later packs can replace same-path project resources; ties pack creation to Godot imports and engine internals; weak package isolation | Rejected for untrusted/native packs; acceptable only for first-party DLC outside this contract |
| glTF/GLB as the entire pack | Good standardized 3D delivery, single-file geometry | Does not model sounds, localization, stack policy, block-state selection, override policy, or all voxel materials | Reject as the native pack/model contract; may remain offline authoring input only if a later tool needs it |
| Custom monolithic binary bundle | Fast indexed reads and complete layout control | New tooling and migration burden; opaque to creators; platform formats easily leak into the contract | Defer; the cache can use private binary formats |
| Content-addressed object store as distribution | Deduplication and patching | Complex publication, garbage collection, signatures, and author UX before a repository exists | Possible future distribution layer, not the pack format |

## Evidence

### Minecraft

**Java Edition official release documentation.** Java resource packs use a namespaced `assets/<namespace>/...` tree, and Mojang has repeatedly versioned and changed the meaning/layout of files within it. Java 1.20.2 introduced version-selected pack overlays, a specific overlay stacking order, and symbolic-link validation; Java 1.21.9 changed pack-version metadata to major/minor compatibility; Java 1.19.3 changed texture-atlas configuration and demonstrated namespaced resource lookup from paths ([Java 1.20.2 notes](https://feedback.minecraft.net/hc/en-us/articles/19703470383757-Minecraft-Java-Edition-1-20-2), [Java 1.21.9 notes](https://www.minecraft.net/en-us/article/minecraft-java-edition-1-21-9), [Java 1.19.3 notes](https://www.minecraft.net/sv-se/article/minecraft-java-edition-1-19-3)).

The durable idea is a namespaced logical tree. The caution is that another game's pack-version integer and file meanings are a moving compatibility surface. VibeCraft should convert a declared Minecraft edition/version into its own contract rather than guess at runtime.

**Bedrock Edition official samples.** Mojang publishes a separate resource-pack tree, manifest, geometry, animation, and controller system in `Mojang/bedrock-samples` ([official sample repository](https://github.com/Mojang/bedrock-samples)). Java and Bedrock resource packs are not one format; “Minecraft compatible” must always identify edition and source version.

### Luanti

Luanti's project documentation accepts media from mod folders, constrains media filename characters, recommends binary GLB over textual glTF, and uses `modname:<name>` registration identifiers to prevent map-corrupting name collisions. It also permits a dependent mod to replace equal-named media, while documenting a number of unsupported glTF features and warning creators not to rely on ignored features remaining ignored ([Luanti mod/media API](https://api.luanti.org/mods/)).

Luanti texture packs are ordinary directories or ZIP archives that replace same-named texture files ([texture-pack documentation](https://docs.luanti.org/for-players/texture-packs/)). This is accessible, but filename-global replacement and implementation-dependent model subsets are exactly why VibeCraft should make asset kind, namespace, supported feature set, and override authority explicit.

### Terasology

Terasology modules place resources under an `assets` directory, attach assets to modules, describe dependencies and versions in `module.txt`, and have separate `deltas` and `overrides` locations ([Terasology module documentation](https://metaterasology.github.io/docs/concepts/modules.html)). Its block assets are split into definitions, tiles, shapes, and prefabs rather than treating one imported scene as the whole content contract ([block-definition documentation](https://metaterasology.github.io/docs/developing/blocks/blockDefinition.html)).

This supports package-owned namespaces and typed assets. It also shows that generic merge/override features quickly become an additional format of their own; VibeCraft v1 should use whole-asset replacement only, as specified by `ASSET-02`.

### Godot, glTF, and archive handling

Godot documents runtime loading of user PNG/audio/3D files and ZIP archives, but its PCK/ZIP resource-pack loader merges content into `res://`; same-path resources can replace previously loaded resources, and C# code packs require separately loading an assembly ([runtime file loading](https://docs.godotengine.org/en/stable/tutorials/io/runtime_file_loading_and_saving.html), [PCK/ZIP loading and replacement](https://docs.godotengine.org/en/stable/tutorials/export/exporting_pcks.html)). That behavior is useful for trusted patches, not an isolation boundary for untrusted art.

Khronos defines glTF as a runtime 3D asset delivery format, including geometry, skins, morphs, materials, and transform keyframes. It does not define package dependencies, playback state machines, or arbitrary-property animation, and names are not guaranteed unique ([glTF 2.0 specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html)). GLB therefore fits as bounded geometry/clip input behind a VibeCraft descriptor, not as the package root.

Microsoft's .NET archive guidance explicitly requires applications processing untrusted archives to validate names and paths and bound per-entry size, total expanded size, and entry count; traversal-safe extraction alone does not prevent decompression bombs ([.NET ZIP/TAR security guidance](https://learn.microsoft.com/en-us/dotnet/standard/io/zip-tar-best-practices)). VibeCraft can reduce the attack surface further by never extracting a pack into a shared filesystem tree.

### Sourced conclusions versus inference

Directly sourced:

- Minecraft, Luanti, Terasology, and Godot all demonstrate directory/ZIP-style resource packaging and path-based replacement.
- Godot's resource-pack mount can replace existing same-path resources.
- ZIP consumers must supply resource limits and path policy.
- glTF does not define application playback behavior and permits references/features beyond VibeCraft's needs.

VibeCraft engineering inference:

- A custom read-only resolver is a smaller security and compatibility surface than mounting untrusted content into Godot's project namespace.
- ASCII lowercase canonical names are worth the aesthetic restriction because the same bytes then identify the same asset on every supported filesystem.
- Source assets and runtime-optimized assets should have separate lifecycles; otherwise renderer/platform decisions become public pack compatibility promises.

## Proposed design

### Container and authoring form

The release artifact is a normal, non-encrypted ZIP file named `*.vcpak`. It is always a `resource_pack`, never a mod, data, or native-code container. V1 accepts the ZIP methods **stored** and **deflate** only. It rejects split/multi-disk archives, encrypted entries, non-regular entries, nested package mounting, and unsupported compression methods.

The accepted ZIP profile must also validate EOCD/central-directory bounds, local-versus-central header consistency, non-overlapping entry ranges, CRC and exact decompressed length, flags/methods, filename decoding, NUL/control bytes, comments/extra-field caps, and an explicit policy for ZIP64, data descriptors, and prepended/trailing bytes.

An unpacked directory with the identical logical tree is accepted only when trusted local developer mode is enabled. Directory mode rejects symlinks/reparse points or snapshots by opened file identity to prevent check/use replacement races, then runs the same path/index/digest pipeline. It is not a second asset contract. Production profiles and multiplayer content locks use packaged `.vcpak` artifacts.

The engine reads entries in place. It does not call `ExtractToDirectory`, add the archive to Godot's `res://` namespace, or search a process working directory. A file needed by a decoder that cannot consume a stream is copied to a private content-addressed cache file after validation, never to an author-controlled path.

### Logical tree

V1 permits this root shape:

```text
pack.json                         required; ASSET-02 manifest
pack.png                          optional preview, PNG
licenses/<path>.txt               optional human-readable license files
assets/<namespace>/
  textures/<path>.png
  sounds/<path>.ogg
  fonts/<path>.woff2               first-party only until parser/UI-spoof tests pass
  locale/<locale>.json
  models/<path>.model.json
  materials/<path>.material.json
  block_visuals/<path>.block-visual.json
  entity_visuals/<path>.entity-visual.json
```

New top-level directories or executable/library formats require a future package-schema revision. Source files such as `.blend`, `.psd`, build scripts, DLLs, nested ZIPs, Godot `.tscn/.tres/.res`, and arbitrary shaders are not loadable pack entries. Authors keep those beside the pack source project, not in the distributed package. Procedural-asset declarations, if greenlit by `ASSET-05`, receive an explicit non-executable asset kind rather than hiding code in this tree.

`pack.json`, `pack.png`, every license, and every asset entry participate in the content digest. Directory entries themselves do not.

### Asset identity

Runtime identity is a typed key:

```csharp
public readonly record struct AssetKey(AssetKind Kind, AssetName Name);
public readonly record struct AssetName(string Namespace, string Path);
```

The text form of `AssetName` is `namespace:path/to/name`. The typed field containing a reference supplies `AssetKind`, so `acme:block/stone` can independently name a texture and a material without collision. Diagnostics print both, for example `texture acme:block/stone`.

Physical mapping is fixed for selected asset kinds. The public model/rig source
format is intentionally not fixed until the `ASSET-03` format spike; it must not be
implicitly inferred as GLB from a path. For example:

```text
texture acme:block/stone  -> assets/acme/textures/block/stone.png
model   acme:block/stone  -> assets/acme/models/block/stone.model.json
```

Package IDs and asset namespaces are separate. A later profile entry may define an
asset in any namespace; `ASSET-02` makes explicit stack position, not hidden namespace
ownership or a dependency declaration, the whole-asset precedence rule.

Canonical names obey all of these rules:

- namespace: 1–64 ASCII characters matching `[a-z0-9](?:[a-z0-9._-]*[a-z0-9])?`;
- asset path: slash-separated components, each 1–64 ASCII characters using the same lowercase character set;
- complete archive path: at most 240 ASCII bytes;
- `/` is the only separator; backslash, colon inside a path, drive/root prefixes, empty components, `.`/`..`, leading/trailing dot or space, control characters, percent-encoded aliases, and Unicode are rejected;
- extension and kind must match the table exactly; paths are never guessed by trying alternative extensions;
- two ZIP entries that produce the same canonical path are a fatal duplicate, even if a ZIP library would return one of them first.

Display names, descriptions, author names, and localization values remain unrestricted UTF-8 strings within their own bounded JSON fields. The restriction applies to machine identity, not user-facing language.

### Read-only resolver and snapshots

```csharp
public interface IPackImage : IDisposable
{
    PackDescriptor Descriptor { get; }
    LogicalContentDigest Digest { get; }
    IReadOnlyCollection<CanonicalPackPath> Paths { get; }
    Stream OpenBounded(CanonicalPackPath path);
}

public interface IAssetSnapshot
{
    AssetRevision Revision { get; }
    bool TryOpen(AssetKey key, out BoundedAssetStream stream);
    AssetOrigin GetOrigin(AssetKey key); // package, source path, override chain, digest
}
```

`IPackImage` owns archive streams and an immutable central-directory index. `IAssetSnapshot` is the fully resolved stack from `ASSET-02`; callers never enumerate the host filesystem or choose override order. Decoders receive bounded streams plus the declared asset kind. They cannot open arbitrary sibling paths.

Resource loading has two phases:

1. **Headless validation/compilation:** parse manifests/descriptors, resolve the ordered
   profile stack, validate the enabled media subset, and produce plain CPU-side
   compiled artifacts on bounded cancelable workers. The native model/rig format is
   experiment-gated; custom WOFF2 remains first-party-only until its parser and
   UI-spoof corpus pass.
2. **Godot publication:** on the permitted client thread, create textures, meshes, materials, audio streams, and animation resources from those artifacts.

No asset parser receives filesystem authority, resolver enumeration, a Godot scene
tree, network access, or an authoritative world handle. Original untrusted model bytes
are never passed to broad Godot scene generation; the validator/compiler emits owned
plain arrays/tables for a narrow publication adapter. A failed new snapshot leaves the
previous snapshot active.

### Archive and parser security policy

V1 loader defaults are intentionally finite:

| Limit | V1 value | Failure |
| --- | ---: | --- |
| Compressed `.vcpak` file | 1 GiB | Reject before opening entries |
| Regular file entries | 20,000 | Reject after central-directory scan |
| One uncompressed entry | 256 MiB | Reject before decode |
| Total declared uncompressed bytes | 2 GiB | Reject with checked arithmetic |
| One JSON descriptor | 8 MiB, depth 64 | Reject before/schema parse |
| One path | 240 bytes; 64 bytes/component | Reject during index build |
| One model/rig payload | 128 MiB within the entry cap | Reject before model parse |
| One decoded texture | 8192×8192 and 256 MiB | Reject before GPU publication |

These are client policy defaults, not promises that every valid-looking pack will fit every GPU. A future engine release may raise hard limits; a pack cannot ask the loader to weaken them. Server-required packs must fit the public default profile unless protocol negotiation explicitly selects another profile.

Validation order prevents expensive work on already-invalid input:

1. verify outer file size, ZIP structure, compression method, entry count, and checked aggregate lengths;
2. canonicalize every name and reject duplicates/non-regular entries;
3. stream and hash every entry with cancellation and exact output bounds;
4. parse the manifest with duplicate JSON-property rejection and a fixed maximum depth;
5. resolve the explicit ordered profile stack and whole-asset winners;
6. parse type-specific descriptors and references;
7. decode/compile heavy media under per-asset and aggregate work budgets;
8. publish an immutable snapshot only after every required asset succeeds.

ZIP-reported sizes are preflight data, not the only guard. Every decompression stream is wrapped in a counting stream that aborts if output exceeds its declared length, per-entry cap, aggregate cap, or cancellation/deadline budget. Integer sums use checked 64-bit arithmetic. Packs are never partially mounted.

### Reproducible content digest

ZIP bytes are not canonical: timestamps, entry order, compression level, and extra fields can differ for the same files. Define `LogicalContentDigest` / `logical_content_sha256` over the validated logical file map:

```text
SHA-256(
  ASCII("VCPACK-CONTENT-1\0") ||
  for each regular entry sorted by canonical path (ordinal):
    u32be(path_utf8_length) || path_utf8 ||
    u64be(uncompressed_length) || SHA-256(uncompressed_bytes)
)
```

All permitted path bytes are ASCII, but they are encoded as UTF-8 in the digest framing. Directory entries, ZIP comments, timestamps, permissions, compression method/level, and central-directory ordering are excluded. Duplicate paths make the package invalid rather than entering the digest twice.

The packager may also publish `artifact_sha256` and `artifact_length` for the literal `.vcpak` file for download corruption checks. Multiplayer/content compatibility uses `logical_content_sha256`, package ID/version, and the resolved lock from `ASSET-02`; it does not confuse container-byte equality with logical-content equality. A one-byte change to any allowed logical file changes the logical digest.

### Compiled cache

The first successful load may compile assets to an implementation-private cache keyed by:

```text
(LogicalContentDigest, asset compiler version, asset API major,
 target platform, renderer capability profile, source AssetKey)
```

The cache may contain atlases, mip chains, compressed GPU texture formats, flattened block render templates, Godot/native mesh buffers, decoded audio metadata, and animation tables. It is safe to delete and must be rebuilt after a compiler or capability change. Cache files never become package identity, are never written back into `.vcpak`, and are never trusted without matching key/checksum metadata.

The first-party base pack goes through the same validator/compiler. It may be precompiled and shipped with a warm cache for startup time, but the portable `.vcpak` remains the source-of-truth artifact and receives an ordinary package/content digest.

### Failure and diagnostics

Every rejection reports:

- stable machine code (`PACK_PATH_TRAVERSAL`, `PACK_DUPLICATE_PATH`, `PACK_SIZE_LIMIT`, `ASSET_FORMAT_UNSUPPORTED`, and so on);
- package ID/version when the manifest was readable;
- canonical source path or redacted raw path;
- limit/expected/actual values where safe;
- override origin and profile-stack position when relevant.

Diagnostics never log archive entry contents or secrets. A failed user pack is disabled and the previous valid profile remains active. A missing/invalid required base or server-locked pack prevents world entry with an actionable content-repair screen; it does not silently substitute gameplay-significant geometry.

## Greenlight criteria

- Two directories with identical allowed file bytes produce the same `LogicalContentDigest` when packed with different ZIP order, timestamp, permissions, compression level, and stored/deflate choices; changing one file byte or canonical path changes it.
- Linux and Windows implementations accept/reject the same path corpus, including case variants, Unicode lookalikes, `..`, absolute/drive paths, backslashes, trailing dots/spaces, duplicate entries, and ZIP symlink metadata.
- Malformed/truncated archives, unsupported/encrypted methods, false lengths, 20,001 entries, oversize entries, aggregate overflow, and decompression bombs fail without extraction, unbounded allocation, or partial mount.
- No `.vcpak` can resolve or replace a Godot `res://` path, load `.NET` code, invoke a Godot scene/script resource, or open a host path through a media URI.
- A representative 10,000-entry pack indexes, validates cached per-entry hashes, and resolves its manifest in under 1 second on the eventual minimum-spec SSD machine with less than 128 MiB transient managed memory; first-load media compilation is measured separately by type.
- The same source pack can be loaded from directory mode and `.vcpak` mode with an identical asset-key inventory, origin report, and compiled logical output.

## Prototype or benchmark

Required: yes.

Smallest useful experiment: Implement only `CanonicalPackPath`, the bounded ZIP/directory `IPackImage`, logical digesting, and a resolver for PNG plus JSON. Create a corpus containing legitimate first-party-style paths and adversarial archives generated with duplicate names, mixed separators, Unicode/case aliases, symlink attributes, malformed central directories, huge declared sizes, excessive entries, and highly compressible payloads.

Success metrics:

- 100 repeated repacks of one logical tree yield one content digest; each one-byte/path mutation yields a different digest.
- The corpus result and machine error codes are identical on Windows and Linux.
- No test writes outside the private cache root; normal validation writes nothing.
- Peak memory remains below 128 MiB while rejecting/streaming the 2 GiB-limit synthetic cases.
- Fuzzing the path/index/manifest boundary for at least 10 million generated inputs produces no crash, hang, partial snapshot, or path escape.

## Risks and open questions

- Minimum supported hardware is still unspecified; the concrete 1-second startup target must be rerun on that machine before greenlight.
- ZIP central directories favor random lookup but not patch-efficient distribution. A repository/CDN delta format can be layered above `.vcpak` without changing asset identity.
- WOFF2, Ogg codec profiles, texture color-space rules, and advanced material texture sets need type-specific decisions and fixtures.
- A future signed repository must sign literal `artifact_sha256`/length plus `logical_content_sha256`; signing is provenance, not a claim that visual content is harmless or fair.
- Cosmetic geometry can still expose hidden information or change target visibility. `NET-09` must let servers lock or constrain resource packs where fairness requires it.

## Dependencies

- Requires: `FOUNDATION-00`, `ARCH-03`.
- Blocks: `ASSET-02`, `ASSET-03`, `ASSET-04`, `ASSET-05`, resource-pack tooling, `NET-09` canonical content agreement.

## Rejected or deferred alternatives

- Native loading of Minecraft Java/Bedrock pack trees: rejected; use an explicit offline converter profile.
- Untrusted Godot PCK/ZIP mounting and `.tscn/.tres` resources: rejected.
- Runtime lookup by unqualified filename or host-relative path: rejected.
- ZIP extraction into the install/project directory: rejected.
- Case-insensitive or Unicode-normalized machine identifiers: rejected in favor of canonical lowercase ASCII.
- Platform/GPU-ready binary files as the portable pack contract: rejected; keep them in the cache.
- A custom encrypted/proprietary bundle: rejected for v1; it does not provide publisher trust and harms tooling.
