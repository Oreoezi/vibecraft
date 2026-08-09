# NET-09 Client content and mod agreement

Status: Proposed

## Decision

Recommended choice: Servers publish a canonical required-content lock manifest; cooperating clients locally verify and declare the selected package digests and sandbox ABI over an authenticated session, while native client plugins remain explicitly trusted/local and outside any safety guarantee.

One-sentence rationale: Content hashes give deterministic compatibility and local integrity for cooperating clients, but a hostile client can lie about possession or execution and remains subject to server authority.

## Context and constraints

- The draft requires the server to assert that client-side mods are synchronized but not distribute them.
- Resource packs, data/content packs, sandboxed logic, and native plugins have different trust and compatibility properties.
- Users should see actionable mismatch errors before entering the world.
- A client should not have to reveal every unrelated locally installed mod.

## Options considered

| Option | Strengths | Costs/risks | Fit |
| --- | --- | --- | --- |
| Compare package names/versions | Human-readable | Versions can lie; bytes may differ | Reject as proof |
| Compare hashes of required packages | Simple, deterministic | Requires one canonical logical-map digest; no provenance | Recommended baseline |
| Require publisher signatures | Adds provenance/revocation possibilities | Key management; signature does not imply safety | Optional policy layer |
| Remote attestation/anti-tamper | Stronger execution claims in narrow environments | Platform lock-in, bypasses, privacy, major scope | Reject for v1 |

## Evidence

The Update Framework treats distributed artifacts as opaque targets identified by signed metadata containing lengths and hashes, and separately addresses rollback, freeze, mix-and-match, and key-delegation problems ([TUF specification](https://theupdateframework.github.io/specification/latest/)). VibeCraft does not need to implement all of TUF merely to compare already-installed packages, but should borrow its separation of byte integrity, metadata version, publisher trust, and freshness.

WASI describes modules as starting without ambient authority and receiving only explicitly granted capabilities ([WASI introduction](https://wasi.dev/)); Wasmtime's security documentation treats executing untrusted WebAssembly safely as an explicit security goal ([Wasmtime security](https://docs.wasmtime.dev/security.html)). In contrast, Microsoft's `AssemblyLoadContext` documentation states that it provides no security features and loaded code has full process permissions ([AssemblyLoadContext](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext)). Therefore “same native C# mods” and “safe native C# mods” are unrelated claims.

Luanti exposes server restrictions for client-side mods but documents functional limits and a shared script environment ([Luanti client Lua API](https://github.com/luanti-org/luanti/blob/master/doc/client_lua_api.md)); its security advisories include sandbox escape and access-control issues ([Luanti security](https://github.com/luanti-org/luanti/security)). This supports treating the sandbox as a maintained attack surface.

## Proposed design

### Package classes

- `resource_pack`: `.vcpak`/`pack.json`; strict inert resource tree, though media parsers remain attack surface.
- `data_pack`: future separate authoritative declarative-content contract owned with `GAME-01`.
- `sandbox_component`: future `.vcmod`/`mod.json` standard Wasm component; no native/precompiled code.
- `native_plugin`: unrestricted local/operator code in a distinct trusted installation path; never a remotely enforceable requirement.

These classes may share resolver vocabulary but never a permissive artifact parser, extension, or trust prompt. V1 content agreement can ship resource-pack locking before executable/data classes exist.

### Canonical lock manifest

The server operator creates a resolved lock file from package manifests:

```text
ContentLock {
  lock_format
  gameplay_ruleset
  packages[] {
    id                 // namespaced stable ID
    version            // display/resolution version
    artifact_kind
    logical_content_sha256
    artifact_sha256?   // literal download bytes; distribution integrity only
    artifact_length?
    required_side      // client, server, both
    mod_abi            // future sandbox_component only
    required_capabilities[]
  }
  policy {
    extra_resource_packs
    extra_sandbox_components
    native_plugins
  }
  lock_sha256          // RFC 8785 canonical lock excluding this field
}
```

`logical_content_sha256` is `ASSET-01`'s domain-separated digest of the accepted logical map. `artifact_sha256` and `artifact_length` identify literal download bytes and are not compatibility identity. `lock_sha256` hashes the RFC 8785 canonical lock (excluding itself). Compiled cache keys are local implementation state and never package identity.

### Negotiation

1. Server sends lock digest, policy, and the subset/list of required package descriptors after address validation.
2. Client answers only for challenged required IDs plus policy-relevant extras; it does not upload its full mod inventory.
3. Server rejects missing hashes, wrong bytes, unsupported ABI/capabilities, disallowed extras, and package-ID collisions before world state allocation.
4. The rejection payload identifies expected/actual ID, version, and abbreviated hash, and distinguishes missing, wrong build, incompatible engine, and forbidden-extra cases.
5. On success, the cooperating client's selected `lock_sha256` declaration is transcript-bound to the authenticated session and recorded with the join event.

Hash comparison is not remote attestation. A modified client can lie unless the protocol or platform supplies stronger attestation, which is outside v1. Server authority and validation remain mandatory.

### Native plugin policy

Supported server policies:

- `forbid`: no native client plugins allowed for this connection according to the cooperating client;
- `ignore`: server makes no claim about native client plugins;
- `allowlist`: cooperating client reports only matching allowed IDs/hashes.

None is an anti-cheat guarantee. A public server must assume the client binary can be modified. A server should not require downloading arbitrary native client code as a normal join step.

### Distribution and signing

V1 verifies installed content and provides package IDs plus human-facing acquisition URLs; the game server does not transfer packages. Publisher signatures may later establish provenance, and a repository may adopt TUF-style signed metadata. Unsigned packages can still be byte-identical; signed malicious packages remain malicious.

## Greenlight criteria

- Logical-map hashing and RFC 8785 lock encoding produce identical digests across supported platforms; literal artifact hashes remain separately named.
- The lock resolver detects cycles, duplicate IDs, conflicting versions, and capability incompatibility.
- Join mismatches are actionable without leaking unrelated local mods.
- Documentation explicitly separates integrity, publisher trust, sandbox safety, and anti-cheat.
- Native .NET plugins are labeled unrestricted/trusted in every UI and manifest path.

## Prototype or benchmark

Required: yes.

Phase 1 builds a packager and fake handshake for one resource pack. Test reordered ZIP entries, timestamp changes, duplicate paths, path traversal, decompression limits, a one-byte logical change, literal-container-only changes, forbidden extras, and stale lock digest. The sandbox experiment later adds one `.vcmod`, ABI/grant mismatch, transitive-import audit, and hostile compilation cases; it does not broaden `.vcpak`.

Pass condition: canonical packages hash reproducibly; malformed archives are rejected before extraction; every mismatch has a stable machine code and useful user message; no full local mod inventory is disclosed.

## Risks and open questions

- A package repository/distribution service is a separate future security design.
- SHA-256 collision risk is not the practical concern; canonicalization ambiguity and parser differentials are.
- Cosmetic client-only resources may change geometry enough to affect fairness; server policy must decide whether they are free, allowlisted, or locked.
- A future client/server sandbox artifact needs an explicit side/component contract; it cannot embed resource-pack or native-plugin payloads.

## Dependencies

- Requires: `ASSET-02`, `MOD-01`, `MOD-02`, `NET-07`, and the authenticated session/channel binding selected by `NET-03`/`NET-08`.
- Blocks: join flow, public modded servers, content acquisition UX.

## Rejected or deferred alternatives

- Treating matching hashes as anti-cheat: rejected.
- Auto-downloading native code from arbitrary servers: rejected for v1.
- Full client mod inventory disclosure: rejected on privacy and necessity grounds.
- Remote attestation: deferred beyond the current product scope.
