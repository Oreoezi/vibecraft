# NET-07 Protocol versioning and capability negotiation

Status: Proposed

## Decision

Recommended choice: Version the wire protocol with a small incompatible `protocol_major`, negotiate named capabilities during a typed handshake, and evolve Protobuf schemas additively within a supported major.

One-sentence rationale: This gives VibeCraft deliberate compatibility without committing to permanent translation for every historical client or conflating the network protocol with saves, packs, mods, and gameplay versions.

### Owner direction — 2026-08-13

Post-v1 networking should support native proxies and authenticated server transfer: a
player may connect to one public address such as `vibepixel.net` and be routed to a
regional backend. V1 does not need the service, but protocol/session boundaries must
not assume one transport connection or backend process lasts for the entire login.
Never trust client-supplied forwarding headers or unauthenticated redirect endpoints.

## Context and constraints

- Client and dedicated server may update independently.
- A server must reject incompatible clients before allocating world/player state.
- Mods and future gameplay extensions need negotiation without owning core field numbers.
- UDP/QUIC packet framing, Protobuf schema evolution, and gameplay semantics can each break compatibility differently.
- Singleplayer should exercise the same handshake even if an in-process transport is used.

## Options considered

| Option | Strengths | Costs/risks | Fit |
| --- | --- | --- | --- |
| Exact build match | Trivial, reproducible | Poor independent updates; needless lockstep | Acceptable for prototypes only |
| One integer protocol version with hand-written compatibility | Familiar | Quickly becomes branching/version debt | Better than build matching but underspecified |
| Major protocol plus negotiated capabilities | Additive evolution, explicit semantics, bounded support | Requires disciplined feature registry and tests | Recommended |
| Per-message semantic versions | Fine-grained | Complex combinations and weak system-level guarantees | Reject for core protocol |

## Evidence

Protobuf permits adding fields and enum values under defined rules, preserves unknown fields in the binary representation, and warns that existing field numbers must not change or be reused; removed numbers should be reserved ([official Proto3 language guide](https://protobuf.dev/programming-guides/proto3/)). These rules provide wire compatibility, not semantic compatibility.

Veloren separates its I/O-free network protocol from TCP, MPSC, and QUIC backends and exposes one network version plus prioritized streams ([Veloren network protocol API](https://veloren.gitlab.io/veloren/veloren_network_protocol/index.html)). PrismarineJS maintains a large version-indexed protocol surface across many Minecraft releases and relies on broad tests ([node-minecraft-protocol](https://github.com/PrismarineJS/node-minecraft-protocol)). Together they show the benefit of transport separation and the maintenance cost of promising many historical variants.

## Proposed design

### Independent version domains

Never use one “game version” integer for everything:

- `protocol_major`: incompatible connection/message semantics.
- `gameplay_ruleset`: server-authoritative content/behavior contract.
- `world_format`: persisted container/record compatibility.
- `pack_format`: resource-pack manifest and asset contract.
- `mod_abi`: sandbox host API contract.

A change may advance one domain without advancing the others.

### Handshake

Before player or world allocation:

```text
ClientHello {
  protocol_major
  client_build
  supported_compression[]
  capabilities[] { id, min_version, max_version }
  client_nonce
}

ServerHello {
  protocol_major
  server_build
  selected_compression
  selected_capabilities[] { id, version }
  session_id
  server_nonce
  mod_policy_digest
}
```

The transport may add address validation and cryptographic handshake fields; those are not Protobuf application fields unless the chosen transport requires them to be.

Rules:

1. No overlapping `protocol_major`: reject with a small bounded reason containing the server's supported major(s).
2. Missing required capability: reject and identify the missing namespaced capability.
3. Unknown optional capability: ignore.
4. Unknown critical message/type: terminate the connection rather than guess.
5. Capability selection is transcript-bound to an authenticated peer/channel before claiming downgrade resistance. Until `NET-03` proves such a binding, the prototype can test negotiation semantics but not a secure public handshake.

Core capability IDs use names such as `vibecraft:chunk_palette/1`; mods use their own namespace. The server selects exactly one version for each enabled capability. Do not use a single unbounded bitset as the durable registry; numeric IDs may be assigned per session after negotiation.

### Schema rules

- Put each incompatible generation in a package such as `vibecraft.protocol.v1`.
- Never change or reuse an emitted field number.
- Reserve removed field numbers and names.
- Prefer new optional fields/messages over changing meaning in place.
- Never route binary protocol data through ProtoJSON because unknown fields can be lost.
- Limit lengths/counts before allocation; generated Protobuf parsing is not a substitute for semantic validation.
- Permit packed custom payloads for hot snapshots/chunks behind a versioned message and capability.

### Support policy

During early development, support one protocol major at a time. After public releases, servers support the current major and optionally one previous major only if a maintained adapter and conformance suite exist. Compatibility is a release decision, not an automatic requirement.

### Future proxy and server-transfer seam

Reserve the state-machine/capability seam, not field numbers or an implementation, for
`vibecraft:server_transfer/1`:

```text
ServerTransferOffer {
  transfer_id
  endpoint
  expected_server_identity
  single_use_handoff_token
  expires_at
  reason
  required_content_lock_digest?
}
```

- The offer arrives over the authenticated current session. The client displays or
  follows only policy-allowed endpoints and verifies the destination identity before
  presenting the handoff token.
- The token is short-lived, single-use, audience-bound to the destination, and does
  not expose the player's reusable credential. Failure returns to a safe menu or the
  original service when explicitly supported; it never loops redirects indefinitely.
- Destination admission reruns protocol/capability/content agreement. A transfer is a
  new authority/session epoch, not an in-place mutation of connection identity.
- A trusted edge proxy may assert original connection/account metadata only through a
  cryptographically authenticated proxy-to-backend channel and explicit trust list.
  Client packets that imitate proxy metadata are rejected.
- Transparent packet forwarding, regional selection, account handoff, and fleet
  control remain post-v1 services. The gameplay protocol merely preserves clean
  reconnect/epoch semantics and does not bake a specific proxy vendor into messages.

## Greenlight criteria

- A written change matrix classifies additive, capability-gated, and major-breaking changes.
- Old-client/new-server and new-client/old-server fixtures cover every supported combination.
- Downgrade, unknown-critical-message, and malformed-length tests fail closed.
- Network, world, pack, and mod versions are represented separately in interfaces.
- Disconnect/reconnect/session epoch APIs can represent a future authenticated
  transfer without preserving stale action IDs, interest epochs, or content maps.

## Prototype or benchmark

Required: yes.

Build two toy protocol revisions. Demonstrate additive fields, an optional capability,
a required capability, a removed/reserved field, and a major mismatch. Keep captured
binary fixtures in tests and verify parsing/round trips across both supported
implementations. Add a state-machine-only transfer fixture using fake authenticated
endpoints/tokens to prove old action/interest/content epochs cannot leak across the
new connection; this does not implement a production proxy.

## Risks and open questions

- The authentication provider and cryptographic transcript binding depend on `NET-03`.
- Capability count, ID length, version count, and total hello bytes need hard pre-allocation limits. Mod-defined capabilities are namespaced entries within those same quotas.
- Supporting even one previous major doubles some integration testing; do not promise it before release policy exists.

## Dependencies

- Requires: `NET-03` authenticated-channel primitive and bounded transport envelope.
- Coordinates with: `MOD-01` and `ASSET-02`, which later register bounded namespaced capabilities without blocking the core handshake.
- Blocks: stable client/server protocol, mod synchronization, release compatibility policy.

## Rejected or deferred alternatives

- Permanent support for all old clients: deferred unless demand justifies a gateway/adapter project.
- Build-number equality as the public contract: rejected because it prevents safe additive evolution.
