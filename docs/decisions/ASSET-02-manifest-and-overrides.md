# ASSET-02 Pack manifest, ordered overlays, and conversion

Status: Proposed

Owner: Asset-contract research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Use a strict, versioned, **resource-only** `pack.json` and an
explicit low-to-high profile stack of exact package artifacts. Every asset key resolves
to the last selected pack that defines it; replacement is whole-asset only. Resource
packs have no dependency DAG. Minecraft compatibility is best-effort edition/version-
specific offline conversion into an ordinary `.vcpak`.

One-sentence rationale: Explicit user stack order gives the familiar texture-pack
behavior deterministically, while exact locks prevent filesystem discovery, Godot, or
Minecraft semantics from choosing a winner.

### Owner decision — 2026-08-10

Resource packs are a simple ordered overlay stack: `vibecraft.base`, then each selected
pack from low to high priority. The last pack defining an asset wins. There is no
resource-pack dependency solver, required/optional edge, authorization rule, or
cross-pack deep merge. The UI and multiplayer lock expose the complete stack and the
winning origin for every asset.

The first-party `vibecraft.base` art pack is the required index-zero entry of every
profile. It is not special after that: any later selected pack may replace any of its
resource assets through the same whole-asset rule.

## Context and constraints

- The client must know what a pack is, its exact identity, where it appears in the
  selected stack, and which engine capabilities its descriptors require before
  decoding expensive media.
- Resource packs, declarative data, and sandboxed modules have different trust, side,
  parsers, and failure policies. They may later share identity vocabulary, but a
  resource artifact can never become executable by adding a manifest field.
- A version string is human/publisher intent; it does not prove byte identity. `ASSET-01` provides `LogicalContentDigest`, and `NET-09` binds the resolved lock declaration to an authenticated session.
- Unordered directory discovery and “last loaded wins” are not acceptable semantics. The same profile must resolve identically on every platform.
- Cosmetic replacement is useful. Its scope is visible and deterministic because the
  selected profile, rather than directory discovery or hidden dependency order, is the
  complete precedence rule.
- V1 needs whole-asset replacement. Generic JSON merge, inheritance across package boundaries, and deletion/tombstone semantics would each become a second schema and are not justified yet.
- Minecraft Java and Bedrock have different pack contracts. Conversion must name edition and source version and produce a report rather than silently dropping unsupported features.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| Filesystem order plus last-file-wins | Minimal loader code; familiar texture-pack behavior | Platform/discovery nondeterminism and invisible winners | Rejected |
| **Explicit low-to-high user stack + strict manifests + exact lock** | Familiar overlays, deterministic winners, simple UI, reproducible multiplayer agreement | No shared pack libraries or automatic composition | **Accepted** |
| Dependency DAG + declared override authorization | Supports modular package libraries | Resolver/tooling complexity exceeds the desired resource-pack model | Rejected for resource packs |
| Generic deltas/JSON merge patches | Small patches and composability | Type-specific merge semantics, ordering conflicts, schema/version debt, difficult provenance | Defer until a concrete high-value use case exists |
| Embed/import Minecraft metadata at runtime | Quick apparent compatibility | Edition/version ambiguity; foreign semantics become permanent runtime API; unsupported extensions fail unpredictably | Rejected; offline converter only |
| UUID-only package identity | Easy generation and rename tolerance | Poor diagnostics/discovery and author UX; UUID/version still does not prove bytes | Rejected as primary identity; signatures may use separate key IDs later |

## Evidence

### Minecraft

**Java Edition official release notes.** Java's resource-pack format is explicitly versioned and keeps evolving. Java 1.20.2 added compatibility ranges and ordered in-pack overlays; Java 1.21.9 changed pack versions to major/minor semantics and revised `pack.mcmeta`; Java 1.20.3 gave downloaded server packs UUIDs and hashes and allowed multiple server packs ([Java 1.20.2](https://feedback.minecraft.net/hc/en-us/articles/19703470383757-Minecraft-Java-Edition-1-20-2), [Java 1.21.9](https://www.minecraft.net/en-us/article/minecraft-java-edition-1-21-9), [Java 1.20.3](https://www.minecraft.net/en-us/article/minecraft-java-edition-1-20-3)). This is direct evidence that version, pack identity, stack order, and hash are distinct pieces of metadata.

**Bedrock Edition official documentation.** Bedrock requires a manifest with unique IDs, pack/module versions, minimum engine version, module kinds, and dependencies; dependencies cause required packs/modules to load first ([manifest reference](https://learn.microsoft.com/en-us/minecraft/creator/reference/content/addonsreference/packmanifest), [dependency reference](https://learn.microsoft.com/en-us/minecraft/creator/reference/content/manifestreference/dependency)). Microsoft separately documents identifier-based replacement of models, animations, and controllers ([overwriting assets](https://learn.microsoft.com/en-us/minecraft/creator/documents/overwritingassets)). This validates explicit metadata and identity, while Bedrock's separate semantics reinforce conversion rather than a generic “Minecraft pack” mode.

Official cooperative-add-on guidance asks paired behavior/resource packs to depend on each other and recommends creator-specific prefixes because many contained identifiers are globally collision-prone ([cooperative add-on guidance](https://learn.microsoft.com/en-us/minecraft/creator/documents/practices/guidelinesforbuildingcooperativeaddons)). VibeCraft keeps resource packs simpler: asset identity remains namespaced, while explicit profile order—not dependencies—chooses a replacement winner.

### Luanti

Luanti `mod.conf` supports required and optional load-before dependencies. Its media rules discourage but permit a mod to overwrite equal-named media from a dependency; registered `modname:name` identifiers are enforced to avoid collisions, and overriding another registration requires a dependency plus explicit override syntax ([Luanti mod/media API](https://api.luanti.org/mods/), [client mod format](https://github.com/luanti-org/luanti/blob/master/doc/client_lua_api.md)). The dependency-plus-explicit-override pattern is useful. Global media filenames and absence of package version constraints in that path are cautions.

### Terasology

Terasology's `module.txt` records module ID, semantic-style version, and dependencies with inclusive minimum, exclusive maximum, optional, and side-related flags. Modules own `assets`, `deltas`, and `overrides` directories ([Terasology module documentation](https://metaterasology.github.io/docs/concepts/modules.html)). This demonstrates a mature package graph and explicit modification areas. It also shows the conceptual cost of deltas in addition to overrides; VibeCraft v1 keeps one replacement operation.

### Versioning and validation standards

Semantic Versioning 2.0.0 distinguishes incompatible API changes, backward-compatible additions, and fixes, and says released version contents must not be modified ([SemVer 2.0.0](https://semver.org/spec/v2.0.0.html)). VibeCraft uses that vocabulary for package releases and asset API compatibility, while still locking exact content digests because publisher version discipline is not machine proof.

JSON Schema Draft 2020-12 provides a standardized way to publish and test machine-readable manifest/descriptor constraints ([JSON Schema 2020-12](https://json-schema.org/draft/2020-12)). The shipping loader remains the authority and uses strict duplicate-property and unknown-property checks; a schema file is tooling, not a substitute for bounded parsing.

### Sourced conclusions versus inference

Directly sourced:

- Minecraft, Luanti, and Terasology all separate package metadata from asset files.
- Minecraft Java and Bedrock use materially different version and manifest systems.
- Luanti demonstrates that equal-named media replacement needs a deterministic policy.
- Package version and downloaded-content hash are represented separately in Minecraft's server-pack path.

VibeCraft engineering inference:

- An override should be legal only when the overriding package names a required target and a bounded asset prefix; otherwise a duplicate key should be an error.
- Whole-asset replacement is sufficient for initial texture/model packs and produces one clear origin for every resolved asset.
- Exact resolved locks belong to profiles/worlds/servers, while compatible version ranges belong to reusable package manifests.

## Proposed design

### Machine identifiers and versions

- `id`: stable package ID, 3–128 lowercase ASCII characters, dot-separated segments using `[a-z0-9][a-z0-9_-]*`; example `example.high_res`. It is never inferred from the filename or display title.
- `version`: a full SemVer 2.0.0 value. A published `(id, version)` must be immutable; two installed artifacts with the same ID/version and different content digests are a hard ambiguity, not an update.
- `manifest_schema`: positive integer. V1 is `1`. Unknown values are rejected.
- `asset_api`: a VibeCraft-owned SemVer comparator set describing pack-contract semantics, independent of the game's marketing/build version.
- `namespaces`: lowercase canonical namespaces owned by the package, following `ASSET-01`. Two selected packages cannot own the same namespace.
- user-facing title/description/authors: bounded Unicode metadata; never used as identity.

Version-range grammar in v1 is deliberately small: one or more whitespace-separated comparators from `=`, `>=`, `>`, `<=`, and `<`, each followed by a full SemVer value. Empty ranges, wildcards, caret, tilde, implicit versions, and boolean OR are rejected. Example: `>=1.2.0 <2.0.0`. Pre-release versions match only a comparator set that contains a pre-release comparator for the same major/minor/patch tuple or an exact pre-release selection.

### `pack.json` schema shape

Illustrative valid resource-pack manifest:

```json
{
  "manifest_schema": 1,
  "id": "example.high_res",
  "version": "1.4.2",
  "asset_api": ">=1.0.0 <2.0.0",
  "title": "Example High Resolution",
  "description": "64 px first-party-style material replacements",
  "authors": ["Example Studio"],
  "license": "CC-BY-4.0",
  "homepage": "https://example.invalid/high-res",
  "artifact_kind": "resource_pack",
  "namespaces": ["example"],
  "required_capabilities": [
    "material.voxel_pbr@1"
  ],
  "metadata": {
    "source_repository": "https://example.invalid/repository"
  }
}
```

Rules:

- JSON is UTF-8 without comments. Duplicate object properties, non-finite numbers, invalid Unicode, or unknown fields outside `metadata` are errors.
- `artifact_kind` is exactly `resource_pack` in `pack.json`. `.vcpak` permits only the `ASSET-01` resource tree. `data_pack` and the eventual sandbox component artifact are separate future contracts and cannot be embedded or selected through this parser. Private native forks are outside package discovery entirely.
- Cross-kind embedding is forbidden. A future sandbox component that needs art selects
  a separate `.vcpak`; it does not hide assets or executable code in the other artifact.
- `license` is SPDX-expression-shaped metadata where possible but is not interpreted as permission by the loader. Missing/unknown licensing is shown to users and repositories; it is not silently invented.
- `metadata` values are informational, bounded JSON and do not affect resolution. Keys beginning `x-` may be used there by tools. The resolver never executes URLs or fetches dependencies.
- `logical_content_sha256`, `artifact_sha256`, `artifact_length`, install path, stack
  position, signatures, and resolved profile contents are absent. They are properties
  of an artifact/profile/lock, not publisher-authored claims inside the artifact.

The project should publish the normative JSON Schema and golden valid/invalid fixtures beside the eventual loader. The C# parser must enforce the same constraints without first materializing an unbounded generic object graph.

### Package selection and ordered precedence

A content profile is an explicit, low-to-high list of exact installed artifacts. It
always begins with the shipped-compatible `vibecraft.base`; every later entry is a user
choice, not a dependency of another pack. The profile/lock stores `(id, version,
logical_content_sha256)` for each position, so selecting a different build or changing
the order is a different profile.

Resolution algorithm:

1. Discover only `ASSET-01`-valid package images and parse their bounded manifests.
2. Match each profile entry to exactly one installed artifact. A missing entry or an
   ambiguous duplicate `(id, version, digest)` fails with an actionable diagnostic.
3. Validate the manifest schema and required engine capabilities for every selected
   artifact before decoding its media. The resolver never downloads anything.
4. Scan selected packs strictly from low to high. Each valid complete logical asset
   replaces the previous definition of the same `AssetKey`; no file, JSON field, or
   array is merged.
5. Resolve references against the final winning map, compile a staging snapshot, and
   emit an exact ordered lock.

Filesystem enumeration, archive order, package filename, locale, and dictionary order
never choose a winner. A pack may define a key in its own or another namespace; the
profile order is intentionally the entire override authorization model. The UI reports
every contributing origin and the final winner.

### Whole-asset replacement policy

- Replacement is the complete logical asset. There is no generic JSON deep merge,
  array concatenation, deletion marker, tombstone, or cross-package inheritance.
- A later texture can replace a texture while an unchanged material/model keeps its
  reference; a later model can replace that model without restating unrelated assets.
- A malformed winning asset fails the staged profile rather than silently falling back
  to a lower asset. The previous active profile remains live.
- A pack cannot replace another package's manifest, alter stack order, change lock
  metadata, or turn itself into executable content.
- Duplicate definitions inside one pack remain invalid at the archive/path layer. The
  same key across different selected packs is normal and deterministic.

### Capabilities and API evolution

The engine advertises an `asset_api` version and namespaced capability versions such as:

```text
model.cuboid@1
model.voxel_rig@1             // format spike; not yet a shipped requirement
animation.rig_profile@1
material.voxel_pbr@1
material.transmission@1       // only if RENDER-06 later greenlights it
```

A pack lists every non-baseline capability needed to preserve intended meaning. Unknown/missing required capability rejects the package; the loader does not silently ignore a material/model feature. Optional visual enhancements require an asset-type-defined fallback in the descriptor, not a manifest flag that magically makes unknown fields safe.

Version responsibilities:

- `manifest_schema` changes when manifest syntax/meaning is incompatible.
- `asset_api` changes when cross-type resolver/descriptor contracts change.
- each descriptor has its own schema major (`vibecraft.model/1`, and so on).
- capability versions describe optional feature contracts.
- package `version` changes when that publisher releases new package contents.
- engine/game build version is diagnostic only unless a separate explicit compatibility field is proven necessary.

The loader supports migration/conversion tools between asset API majors; it does not mutate installed packages in place. A package claiming one released version with new bytes is rejected by repositories/locks as publisher error.

### Exact content lock and atomic activation

The resolver writes a canonical lock for profiles, worlds, and `NET-09`:

```text
ContentLockV1 {
  lock_schema: 1
  asset_api: exact engine asset API
  packages_low_to_high[] {
    stack_index
    id
    version
    artifact_kind              // resource_pack in this v1 contract
    logical_content_sha256     // ASSET-01 logical-map digest
    artifact_sha256?           // literal download artifact integrity only
    artifact_length?           // paired with artifact_sha256
    required_capabilities[]
  }
  effective_asset_map_digest
  lock_sha256
}
```

V1 lock encoding is RFC 8785 canonical JSON with no floats, duplicate keys, invalid Unicode, or integers outside the interoperable safe range; identity/version/digest values are strings. `effective_asset_map_digest` hashes sorted `(AssetKey, winning package LogicalContentDigest, winning canonical path)` records. `lock_sha256` covers the complete canonical lock except itself. A later encoding requires a new lock schema/domain.

Activation is transactional:

```text
discover -> resolve -> validate -> compile staging snapshot
         -> compare/record lock -> swap snapshot at frame boundary
         -> retire old snapshot after handles/jobs drain
```

Any failure before swap leaves the old profile active. Resource-only hot reload may use this flow in developer/local mode. It does not promise that data registries, executable mods, live worlds, or multiplayer content can be hot-swapped. A connected server lock remains fixed for that session unless a future protocol explicitly coordinates a content transition.

### Minecraft offline conversion boundary

No VibeCraft runtime loader recognizes `pack.mcmeta`, Bedrock `manifest.json`, Minecraft `format_version`, resource locations, model inheritance, Molang, OptiFine/CIT conventions, or Minecraft pack precedence. The conversion CLI owns those semantics:

```text
vibecraft-content import minecraft-java \
  --source <directory-or-zip> \
  --source-version 1.20.2 \
  --package-id user.imported_pack \
  --out user.imported_pack.vcpak

vibecraft-content import minecraft-bedrock \
  --source <directory-or-mcpack> \
  --source-version 1.21.80 \
  --package-id user.imported_pack \
  --out user.imported_pack.vcpak
```

Separate converter profiles are versioned and tested against exact edition/version fixtures. The source version is required; “auto” may offer a guess but cannot produce a release artifact until the user confirms it.

The converter:

1. opens the source through the same bounded, non-extracting archive policy;
2. parses only its declared Minecraft edition/version plus explicitly supported extension profiles;
3. maps known Minecraft asset IDs to VibeCraft native asset keys through a versioned mapping table;
4. emits native manifests/descriptors/media and explicit override rules against `vibecraft.base` where mappings exist;
5. copies/decodes only user-supplied assets the user is entitled to use; it never bundles missing vanilla Minecraft assets from the game or a network service;
6. validates the generated native pack through the normal VibeCraft packager;
7. emits a deterministic conversion report beside the output.

```text
ConversionReportV1 {
  tool_version
  source_edition
  source_version
  source_content_digest
  mapping_table_version
  output_package_id/version/content_digest
  converted[] { source_path, source_identifier?, output_asset_keys[] }
  warnings[]  { code, source_path, message, suggested_action? }
  errors[]    { code, source_path, message }
  unsupported[] { feature, count, source_paths[] }
}
```

Java cuboid models/blockstates, textures, sounds, language files, and simple animation metadata can map where native equivalents exist. Bedrock geometry/animations/controllers require their own mapping. Core shaders, arbitrary Molang expressions, edition-specific render controllers, custom third-party extensions, and unsupported blend/material behavior are reported, never silently approximated. The generated pack contains provenance metadata, but no `minecraft:` namespace receives special runtime meaning.

Conversion is best-effort tooling supplied as-is. Success means only “valid native
output with a reviewed report,” never visual, behavioral, or practical usability
parity. The tool should support incremental re-conversion from source; users should not
hand-edit generated output without either forking it or accepting that regeneration
replaces it.

## Greenlight criteria

- Given the same installed artifacts and selected profile, resolution produces byte-identical canonical locks and effective asset maps regardless of filesystem enumeration, archive order, locale, OS, or process hash randomization.
- The resolver reports actionable paths for missing stack entries, duplicate
  ID/version/digest artifacts, unsupported capabilities, malformed winning assets, and
  final winning origins.
- Every effective asset has exactly one winning origin plus a complete ordered override chain; no collision is settled by directory discovery or dictionary insertion order.
- Package `version`, asset API compatibility, content digest, literal download hash, and publisher trust are separately represented in code/UI/tests.
- Activation is all-or-nothing: corrupting any required descriptor leaves the previous snapshot and lock active.
- A Java and a Bedrock fixture with the same marketing content use separate converters, produce only native VibeCraft files, and report every dropped/approximated source construct.
- Joining with a `NET-09` lock detects a one-byte package change, changed stack order, changed effective winner, missing package, and incompatible capability before world admission.

## Prototype or benchmark

Required: yes.

Smallest useful experiment: Build a headless resolver/packager for `vibecraft.base`,
two independent texture/model overlays, and one malformed package for every failure
class. Add a fake Java 1.20.2 input converter that maps two textures, one cuboid model,
one blockstate, and one unsupported shader/extension into a native pack plus report.

Success metrics:

- Shuffle package discovery and dictionary insertion 10,000 times; obtain one lock/digest and one asset-origin report.
- Resolve 100 explicitly ordered packages and 100,000 asset keys in under 250 ms after
  manifests/indexes are cached on the eventual minimum-spec desktop, with bounded
  linear memory.
- Exhaustive fixtures cover duplicate artifact identity, missing stack entries,
  duplicate paths, replacement of each asset kind, malformed winning assets, and every
  stack-order permutation that changes a winner.
- A failed staged reload changes zero active asset handles and leaks no archive/cache handles after retirement.
- Re-running the converter with identical source/tool/mapping versions yields the same native content digest and conversion report; every unsupported construct appears with a stable code and source path.

## Risks and open questions

- A package repository, acquisition URLs, publisher signatures, revocation, and trust UI are separate distribution/security decisions. A signature proves provenance, not safety or quality.
- Resource packs deliberately cannot express reusable package dependencies. If an
  author needs a library-like relationship, publish a combined pack or make the
  required stack order explicit in installation instructions/profile tooling.
- Overlay packs can intentionally create visually misleading content. Public servers need `NET-09` policy for free, allowlisted, or exact cosmetic packs.
- One resolver implementation may later coordinate distinct artifact classes, but this decision greenlights only resource packs. It must not be generalized into an executable shared manifest without a separate security decision.
- Whether worlds store their historical resource lock for faithful screenshots/replays is a product decision; authoritative save compatibility must not depend on cosmetic resources.

## Dependencies

- Requires: `ASSET-01`, `MOD-03`, `NET-07` capability-version principles.
- Blocks: `ASSET-03`, `ASSET-04`, `ASSET-05`, pack/profile UI, resource hot reload, `NET-09`, converter implementation.

## Rejected or deferred alternatives

- Native runtime interpretation of any Minecraft edition: rejected.
- Inferring edition/version or silently accepting “close enough” Minecraft input: rejected for release conversion.
- Filesystem/discovery-order last-file-wins: rejected; explicit profile-order
  whole-asset replacement is accepted.
- Generic deep merge, JSON Patch, deletion/tombstones, and cross-package inheritance: deferred.
- Selecting the same package ID more than once in one resource profile: rejected for
  v1; install a combined/repackaged artifact when that is genuinely required.
- Resolver downloads or executes acquisition URLs: rejected; installation is a separate user-mediated workflow.
- Treating version equality, UUID equality, signatures, or hashes as interchangeable: rejected.
