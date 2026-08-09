# NET-03: Transport and reliability

Status: Proposed

Owner: Networking research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)  
Parent decisions: [`NET-01-network-simulation-model.md`](NET-01-network-simulation-model.md), [`NET-02-movement-prediction-reconciliation.md`](NET-02-movement-prediction-reconciliation.md)

Recommended choice: Greenlight a VibeCraft-owned message transport abstraction and three bounded semantic traffic classes, while treating Valve GameNetworkingSockets (GNS) as the first **prototype candidate**, not the selected production transport. Start with one 20 Hz input bundle and at most one coalescible snapshot per `WorldTick`. Public direct-IP use remains blocked until authenticated peer identity/channel binding, native ownership, admission cost, lane behavior, and packaging pass measurement. Do not fall back to an improvised UDP protocol.

## Decision

VibeCraft will expose a message-oriented transport abstraction that is independent of Godot RPCs and scene paths. The first spike wraps GNS's flat C API and evaluates its encryption, connection management, congestion behavior, fragmentation/reassembly, delivery modes, statistics, and network-condition simulation. Selection occurs only after the prototype gates; the architecture must survive rejecting GNS.

The game protocol, authorization, serialization, interest management, snapshot deltas, replay protection, and application-level transaction semantics remain VibeCraft responsibilities. GNS encryption does not establish that a player owns a VibeCraft account, make an arbitrary direct-IP server trustworthy, or stop volumetric denial-of-service traffic. Those claims require application authentication, server identity policy, pre-auth resource limits, and possibly upstream relay or hosting protection.

The rates in the current spec are research questions, not quality presets. `WORLD-08` owns one 20 Hz authoritative clock. `NET-06` may change packet batching/coalescing or test an exactly nested movement substep, but transport configuration cannot create a second gameplay timeline.

One-sentence rationale: Mature transport mechanisms are worth prototyping, but VibeCraft must prove GNS's C#/Godot packaging and public trust path while preserving application-level authority, ordering, backpressure, and a viable replacement seam.

## Context and constraints

- One protocol must support a Godot C# client and a headless, Godot-independent C# server.
- The public-Internet path needs authenticated encryption, congestion-aware sending, connection liveness, sequencing, and denial-of-service-aware admission. Application authentication remains separate.
- Realtime motion must not wait behind chunk data or other old reliable messages.
- Block, inventory, entity-lifecycle, and administrative transactions cannot be silently lost or applied twice.
- Chunk baselines and resource manifests may be large, but must not create unbounded memory queues or stall the world thread.
- IPv4 and IPv6, NAT behavior, path-MTU variation, packet loss, reordering, and duplication must be expected.
- V1 is a desktop dedicated-server game. Browser, console, peer-to-peer host migration, and globally relayed networking are not assumed.
- Protocol and payload versions must permit clients and servers to be deployed at different times.
- The spec's “safe from DDoS” goal is not achievable solely inside a game transport. Volumetric attacks require infrastructure outside the process.

## Options considered

### Option A — Valve GameNetworkingSockets behind a thin C# binding (first prototype candidate)

GNS provides a message-oriented connection API with reliable and unreliable messages, fragmentation/reassembly, acknowledgements and retransmission, encryption, connection statistics, loss/latency simulation, and connection lanes. It can be built without Steam. Its scope deliberately excludes game entity serialization and compression, which keeps those policies in VibeCraft. The project publishes a flat C ABI suitable for a small generated P/Invoke layer.

Advantages:

- A mature set of transport mechanisms that VibeCraft would otherwise have to design, secure, tune, fuzz, and operate.
- Multiple lanes allow sparse control traffic and bulk chunk traffic to avoid reliable head-of-line interference with one another.
- Built-in diagnostics and network-condition simulation improve reproducibility.
- BSD-3-Clause licensing and standalone operation avoid tying the dedicated server to Steam.

Costs and caveats:

- Native builds, ABI pinning, packaging, and lifetime management increase integration risk in a C# project.
- GNS direct-IP mode does not provide account authentication or volumetric DDoS protection.
- A third-party C# wrapper would add another compatibility dependency; VibeCraft should initially bind only the narrow flat API it uses.

### Option B — LiteNetLib in pure C#

LiteNetLib supports Godot and .NET Standard 2.1, IPv6, channels, fragmentation, MTU discovery, packet simulation, and several reliable, sequenced, and unreliable delivery modes. Its README claims a one-byte unreliable and four-byte reliable library header. This is the lowest-friction C# integration and is the primary fallback.

The project's documented feature list does not advertise a complete authenticated-encryption, peer-identity, or Internet congestion-control solution. Selecting it would therefore require explicit, independently reviewed designs for those layers rather than an assumption that “UDP plus reliability” is sufficient. That extra security and operations work is why it is not the default.

### Option C — Godot ENet or low-level ENet

Godot's ENet peer uses UDP, channels, configurable bandwidth limits, and unreliable, unreliable-ordered, and reliable transfer modes. ENet is small and established. Godot's high-level multiplayer API, however, is a poor protocol boundary for a separate non-Godot server because RPC configuration and scene conventions can leak into the wire contract. Low-level ENet avoids that coupling but still leaves encryption, identity, admission, and much operational telemetry to VibeCraft.

This remains suitable for a LAN prototype, not the current public-server requirement without additional reviewed layers.

### Option D — QUIC streams plus DATAGRAM

QUIC is a secure, multiplexed, congestion-controlled transport. RFC 9221 adds unreliable datagrams that share the connection's authentication and congestion context without being retransmitted. In principle, streams fit control/bulk data while DATAGRAM fits snapshots.

The managed .NET QUIC implementation has been stable since .NET 9, but as researched on 2026-08-09, the public `System.Net.Quic` datagram API request remains open. A V1 implementation would therefore need a native MsQuic binding or put hot realtime traffic on reliable streams, undermining the main reason to choose QUIC. Reconsider when the target runtime exposes a supported DATAGRAM API and game-oriented behavior has been measured.

### Option E — WebRTC DataChannels

WebRTC DataChannels run SCTP over DTLS over UDP and support ordered/unordered and partial-reliability policies with congestion control and encryption. Mojang's current NetherNet documentation shows that Minecraft uses a WebRTC-based transport for peer-to-peer connectivity, including HTTPS signaling, ICE, DTLS/SCTP, and identity assertions.

That is strong evidence that WebRTC can support a shipping voxel game, but it also brings signaling, ICE, certificate/fingerprint, and SCTP concerns that VibeCraft's desktop dedicated-server V1 does not presently need. It becomes more attractive for browsers, difficult NAT traversal, platform networking requirements, or peer hosting.

### Option F — Custom UDP reliability, encryption, and congestion control

This option offers complete wire control and no native dependency. It also assigns VibeCraft responsibility for congestion fairness, pacing, retransmission, acknowledgement strategy, replay defense, key establishment, migration/NAT behavior, MTU handling, fragmentation, connection state, abuse resistance, metrics, and years of edge cases.

RFC 8085 explicitly requires UDP applications to implement congestion control and account for loss, duplication, reordering, and reliability where needed. “UDP + Protobuf” supplies none of those properties. This option is rejected for V1.

## Evidence

### Minecraft and voxel-engine precedents

- Mojang's current [NetherNet onboarding guide](https://github.com/Mojang/bedrock-protocol-docs/blob/main/NetherNetOnboardingGuide.md) describes a Minecraft peer-to-peer transport built on WebRTC, with HTTPS signaling, ICE, DTLS/SCTP, and identity assertions. Minecraft itself is therefore evidence against freezing “Minecraft networking” to one historical transport.
- Mojang's [Bedrock protocol documentation repository](https://github.com/Mojang/bedrock-protocol-docs) publishes current protocol material separately from game simulation details. This supports keeping VibeCraft's wire protocol and transport implementation as distinct layers.
- Luanti's engine documentation describes [a small reliability layer over UDP](https://docs.luanti.org/for-engine-devs/network-protocol/) and warns that order is not preserved between channels. It explains that channels keep a large reliable block transfer from blocking unrelated small reliable messages. This directly supports traffic isolation, while also illustrating the custom transport code VibeCraft would inherit by copying that design.
- Veloren's current network crate declares [Quinn/QUIC, bincode, compression, and network metrics](https://gitlab.com/veloren/dev/veloren/-/blob/master/network/Cargo.toml?ref_type=heads), while its server enables that transport in the [server crate manifest](https://gitlab.com/veloren/veloren/-/blob/master/server/Cargo.toml?ref_type=heads). A modern open-source voxel RPG choosing QUIC is evidence that secure general transports are viable, not proof that QUIC is automatically optimal for VibeCraft's C# runtime.
- Veloren's historical [network authentication design issue](https://gitlab.com/veloren/veloren/-/issues/749) records how a blocking authentication flow complicated server ECS behavior and motivated protocol simplification. This supports explicit connection states and keeping network waits off the world loop.

### Candidate transport capabilities

- The official [GameNetworkingSockets repository](https://github.com/ValveSoftware/GameNetworkingSockets) documents reliable and unreliable messages, fragmentation/reassembly, encryption, detailed statistics, simulated network conditions, and connection lanes. It also states that entity serialization and compression are out of scope and that Steam is not required.
- GNS exposes a [flat C interface](https://github.com/ValveSoftware/GameNetworkingSockets/blob/master/include/steam/steamnetworkingsockets_flat.h), publishes [build instructions](https://github.com/ValveSoftware/GameNetworkingSockets/blob/master/BUILDING.md), and uses the [BSD 3-Clause license](https://github.com/ValveSoftware/GameNetworkingSockets/blob/master/LICENSE). These are prerequisites for, but not proof of, a maintainable C# deployment.
- Valve's [connection-lane documentation](https://partner.steamgames.com/doc/api/ISteamNetworkingSockets?l=english&language=english) states that reliable messages in one lane are delivered in order, lanes can have priorities and weights, and a small lane count is inexpensive. It also notes that lane zero is the most wire-efficient, informing the three-lane design below.
- LiteNetLib's [official repository and feature list](https://github.com/RevenantX/LiteNetLib) documents its C# support, Godot compatibility, delivery modes, channels, fragmentation, MTU discovery, IPv6, and packet simulation. The absence of advertised authenticated encryption or a complete congestion scheme is a reason to require a security review, not a claim that extensions cannot provide them.
- Godot documents that [ENetMultiplayerPeer uses ENet over UDP](https://docs.godotengine.org/en/stable/classes/class_enetmultiplayerpeer.html), with channels and bandwidth configuration. Its [high-level multiplayer guide](https://docs.godotengine.org/en/stable/tutorials/networking/high_level_multiplayer.html) explains UDP loss/reordering, transfer modes, and channels, while also showing how RPC configuration is embedded in the engine API.
- [RFC 9000](https://www.rfc-editor.org/rfc/rfc9000.html) specifies QUIC as a secure, multiplexed transport and advises avoiding IP fragmentation. [RFC 9221](https://www.rfc-editor.org/rfc/rfc9221.html) adds non-retransmitted QUIC datagrams that still share connection security and congestion control.
- Microsoft's [.NET QUIC overview](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/quic/quic-overview) documents `System.Net.Quic`, its .NET 9 stability, and platform dependencies. The runtime's [public QUIC DATAGRAM API issue](https://github.com/dotnet/runtime/issues/53533) remains the relevant integration gap as of the research date.
- [RFC 8831](https://www.rfc-editor.org/rfc/rfc8831.html) specifies WebRTC DataChannels over SCTP/DTLS/UDP, including ordered/unordered and partial-reliability policies. It also notes that without message interleaving, a large message can monopolize an SCTP association—another reason to cap and schedule bulk messages.

### UDP and serialization constraints

- [RFC 8085](https://www.rfc-editor.org/info/rfc8085/) says UDP applications need congestion control and must cope with loss, duplication, reordering, and any required reliability. Bare UDP is a datagram substrate, not a production game transport.
- UDP itself has an [eight-byte header](https://www.rfc-editor.org/info/rfc768/), while IPv6 requires links to support a [1280-byte minimum MTU](https://www.rfc-editor.org/info/rfc8200/). Tunnel and transport overhead consume part of that budget. VibeCraft should avoid IP fragmentation and establish a conservative measured message limit.
- Protobuf's [wire encoding guide](https://protobuf.dev/programming-guides/encoding/) shows that field tags, varints, fixed-width fields, and length-delimited values all consume bytes. Its [techniques guide](https://protobuf.dev/programming-guides/techniques/) says a protobuf message is not inherently self-delimiting, so a stream transport needs framing. GNS already preserves message boundaries.
- Protobuf's [compatibility guidance](https://protobuf.dev/best-practices/dos-donts/) warns that clients and servers are not updated in lockstep and that removed field numbers must be reserved. This supports explicit protocol negotiation and disciplined schema evolution.

## Rate and bandwidth implications

Tick interval alone does not measure responsiveness. The intervals are:

| Rate | Interval |
|---:|---:|
| 16 Hz | 62.5 ms |
| 32 Hz | 31.25 ms |
| 64 Hz | 15.625 ms |
| 128 Hz | 7.8125 ms |

The following is a planning model, not measured GNS overhead. It assumes one message per update and 64 bytes total for IP, UDP, transport, security, and acknowledgement-related overhead:

`wire bitrate = updates/second × (application bytes + 64) × 8`

| Traffic example | 16 Hz | 32 Hz | 64 Hz | 128 Hz |
|---|---:|---:|---:|---:|
| 24-byte input | 11.264 kbps | 22.528 kbps | 45.056 kbps | 90.112 kbps |
| 1000-byte snapshot | 136.192 kbps | 272.384 kbps | 544.768 kbps | 1.090 Mbps |

For 32 clients each receiving a hypothetical 1000-byte snapshot, server outbound traffic is approximately 4.358 Mbps at 16 Hz, 8.716 Mbps at 32 Hz, 17.433 Mbps at 64 Hz, and 34.865 Mbps at 128 Hz. This excludes retransmissions, chunk streaming, control traffic, voice, and provider overhead. Delta compression and interest management may reduce payload sizes, while loss may increase them. The useful conclusion is the linear scaling, not the assumed packet size.

Inference: start the integrated prototype with one 20 Hz redundant input bundle and no more than one coalescible snapshot per world tick. NET-06 must replace illustrative payload assumptions with representative captures and perceptual tests before changing packet cadence.

## Proposed design and interfaces

### Game-owned abstraction

The shared networking assembly owns an interface similar to:

```csharp
public enum DeliveryClass
{
    RealtimeSuperseding,
    ReliableControl,
    ReliableBulk,
}

public readonly record struct ConnectionId(ulong Value);

public readonly record struct OwnedTransportMessage(
    ConnectionId Connection,
    DeliveryClass Delivery,
    IMemoryOwner<byte> PayloadOwner,
    int PayloadLength);

public interface IMessageTransport : IAsyncDisposable
{
    ValueTask<BoundEndpoint> ListenAsync(Endpoint endpoint, CancellationToken cancellationToken);
    ValueTask<ConnectionId> ConnectAsync(Endpoint endpoint, CancellationToken cancellationToken);
    SendResult TrySend(ConnectionId connection, ReadOnlySpan<byte> payload, DeliveryClass delivery);
    int Poll(Span<OwnedTransportMessage> destination);
    TransportStats GetStats(ConnectionId connection);
    void Close(ConnectionId connection, DisconnectReason reason);
}
```

`BoundEndpoint` reports the actual OS-selected port needed by local hosting. `SendResult` distinguishes accepted, would-block/over-budget, too-large, closed, and fatal errors. Each polled payload is an explicit disposable lease (a managed copy is also permitted); it can never outlive native storage accidentally. Exact zero-copy choices remain prototype work. Simulation code depends only on this interface. Godot adapters translate messages into presentation events and never define protocol identity through node paths or RPC names.

### Three bounded lanes

| Logical lane | GNS behavior | Examples | Initial policy |
|---|---|---|---|
| Realtime | Unreliable, no-delay; lane 0 | Redundant inputs, owner corrections, remote snapshots | Most common lane; app sequence/baseline; discard superseded queued data; target payload at or below 1000 bytes |
| Control | Reliable ordered; high priority | Hello/auth, spawn/despawn, inventory and block results, teleport, disconnect | Sparse; command IDs for application idempotence; 256 KiB per-peer queue ceiling |
| Bulk | Reliable ordered; low priority/weight | Chunk baselines, resource manifests | Pieces no larger than 16 KiB initially; 4 MiB per-peer producer ceiling; pause generation under congestion |

The numbers above are prototype defaults, not protocol constants. Lane zero is assigned to realtime because it is the frequent path and GNS documents it as most efficient. Control can have higher scheduling priority while remaining sparse. Bulk must never share a reliable ordering domain with control.

GNS guarantees transport delivery properties, not application semantics. Every inventory or world mutation request still carries a command ID; the server records a bounded response window so a retry cannot apply a transaction twice. Unreliable snapshots carry sequence, server tick, baseline ID, and state revision so stale or undecodable deltas can be dropped and a new baseline requested.

### Threading and backpressure

- A dedicated network pump owns native polling and sends. It communicates with world shards through bounded single-producer/single-consumer or otherwise measured queues.
- The world thread never performs socket I/O, compression, DNS, or a blocking authentication call.
- Realtime data is coalesced by semantic key before enqueue: only the newest unsent snapshot for a peer/entity set survives.
- Bulk production pauses before configured/native queue ceilings, using conservative admission derived from configured caps, queue age, pending bytes, and measured delivery. GNS does not grant the application an exact consumable congestion budget. A player may wait for a chunk; the server may not allocate without bound.
- Reliable-control overflow is treated as a broken or abusive connection and closes with a diagnostic reason. Silently dropping a transaction is forbidden.
- Queue age, not just byte count, is exported. Persistent realtime queueing indicates congestion and triggers snapshot-rate/interest reduction before disconnect.

### Handshake and trust boundary

1. Establish the GNS encrypted connection and complete its transport handshake; do not treat that alone as application identity or server trust.
2. The client sends a bounded `ClientHello`: protocol range, build, capabilities, supported payload codecs/compression, authentication material or token reference, nonce, and content/mod manifest hash.
3. The server selects one exact protocol and codec set, validates identity and compatibility, then returns session ID, server tick/rates, limits, and trust result.
4. Only after admission does the server allocate a player entity, chunk subscriptions, replay window, and significant world state.

All pre-auth message counts, sizes, CPU work, and timeouts are capped. Server identity and account-token details belong to NET-07/NET-08/NET-09, but this transport cannot be greenlit without a credible trust hook. Direct-IP encryption without authenticated server identity remains vulnerable to an active intermediary.

### Framing and payload codecs

GNS messages already have boundaries, so each top-level transport message needs only a compact versioned envelope, not a redundant stream length prefix:

```text
magic | protocol-major | family | flags | payload-version | payload
```

- Use Protobuf for the handshake, structured control messages, transaction results, and infrequent metadata.
- Begin the prototype with Protobuf for inputs and snapshots too, then retain it only if measured wire size and CPU meet NET-06 budgets.
- A packed hot-path codec may replace those payloads, but it must remain inside the same negotiated envelope, have golden vectors, explicit bounds, and its own version. “Custom packed” is not permission for native struct dumps.
- Validate lengths and element counts before allocation. Unknown top-level families are rejected or ignored only according to the negotiated protocol rule.
- Reserve deleted Protobuf field numbers and preserve unknown fields where the generated implementation permits forwarding.
- Compress only bulk payloads above a measured threshold. Never mix secrets and attacker-controlled text in one compression context.

### Packet-size policy

Keep unreliable application payloads at or below 1000 bytes until path-MTU measurements justify a different limit. Split state by interest region or entity group rather than relying on IP fragmentation. Reliable bulk messages may be larger at the API level because GNS fragments them, but small application pieces improve queue control. Do not claim per-message cancellation: after interest changes, stop producing fragments and ignore already queued stale epochs at the receiver.

### DDoS boundary

GNS improves connection admission and transport robustness; it cannot absorb an attack that saturates the host's link. Steam Datagram Relay can conceal and protect server addresses for eligible Steam integrations, as described in Valve's [networking overview](https://partner.steamgames.com/doc/features/multiplayer/networking), but VibeCraft cannot assume access to or dependence on it. NET-08 must choose hosting firewall policy, rate limiting, observability, upstream filtering/relay, and incident behavior. The V1 claim should be “bounded and hardened application admission,” not “DDoS safe.”

### Singleplayer

Use the same protocol semantics for solo and multiplayer. `ARCH-04` compares loopback GNS with an embedded host adapter; both must pass identical ordering, loss-policy, disconnect, serialization-vector, and replay conformance suites. Local topology cannot become an alternate game implementation.

## Prototype plan

Build a disposable transport harness before integrating world simulation:

1. Pin a reviewed GNS revision and build its native library for Windows x64 and Linux x64; add macOS for V1 if it remains a launch target. Generate or hand-maintain only the required P/Invoke declarations from the official flat header.
2. Connect a minimal Godot C# client to a standalone C# server. Exercise connect, authenticated server-key/channel-binding hook, all three traffic classes, cross-lane reorder, disconnect, reconnect, stale-epoch suppression, and process shutdown.
3. Run the declared acceptance load plus a separate 64-peer stress fixture. Each peer sends a 24–40 byte input bundle at 20 Hz and receives at most one coalescible snapshot per world tick while a 10 MiB paced bulk object and periodic reliable control messages share the connection.
4. Test 0–250 ms RTT, variable jitter, 0%, 1%, 5%, and 10% loss, reordering, duplication, and constrained bandwidth using GNS simulation plus an external network emulator where available.
5. Capture actual wire bytes, retransmissions, message queue age, lane latency, native/managed allocations, GC pauses, CPU, disconnect reasons, and baseline recovery.
6. Fuzz envelopes and state transitions; send oversized lengths, invalid versions, connection floods, stalled handshakes, duplicate commands, and disconnects during bulk transfer.
7. Run a 30-minute impairment soak followed by an overnight clean-network soak on release builds.

## Measurable success and failure criteria

Greenlight GNS only if all mandatory criteria pass on named reference client/server hardware:

- With the 10 MiB bulk transfer active and 5% packet loss, p99 additional realtime queueing latency stays below 10 ms relative to the same impairment run without bulk traffic. A repeatable reliable-lane head-of-line stall is failure.
- Every accepted reliable bulk object arrives with the expected hash or the connection reports an explicit failure; no silent corruption occurs.
- Reliable control messages are observed in order, and duplicate transaction requests apply exactly once through application command IDs.
- Per-peer and global send/receive queues remain within configured byte and message ceilings. Memory must return to the steady-state envelope after churn; monotonic native memory growth is failure.
- The 30-minute impairment run has no crash, use-after-free symptom, deadlock, unhandled callback exception, or managed buffer invalidation. The overnight run shows no statistically significant native leak.
- Report transport CPU, queue age, delivery, and allocation at the declared acceptance load and separate stress load on recorded hardware. Set a production budget only after that fixture exists; do not generalize from an arbitrary 64-peer number.
- Managed allocation and GC measurements show no recurring full collection caused by the network pump during steady state. Exact allocation budgets are set from the first baseline and then made regression gates.
- Captured application secrets are not plaintext on the wire. A client rejects an intentionally incorrect server identity/trust configuration.
- Oversized and malformed traffic cannot crash the process or grow memory beyond admission/queue limits. Significant world state is never allocated before admission succeeds.
- Measured clean-network wire rates are explainable within 20% of the rate model after documenting batching, transport overhead, acknowledgements, and encryption. A larger unexplained gap blocks rate decisions.
- Native binaries load and shut down cleanly from packaged Godot and standalone server builds on every V1 platform. Requiring an undocumented machine-global runtime is failure.

Volumetric DDoS survival is deliberately not a harness success criterion because a local process cannot validate upstream capacity. NET-08 owns that experiment.

If GNS fails native deployment, lifetime safety, or latency isolation after one focused remediation pass, mark this decision Needs Experiment and prototype LiteNetLib with an explicit security/congestion design. Do not erase failed measurements, and do not replace GNS with custom UDP by default.

## Risks and mitigations

| Risk | Consequence | Mitigation |
|---|---|---|
| Native ABI or packaging drift | Client/server startup failures after upgrades | Pin GNS revision, bind the flat API narrowly, run packaged smoke tests in CI, and expose the native version in diagnostics |
| Managed/native lifetime error | Leaks, crashes, invalid buffers | Single owner for handles, safe-handle wrappers where practical, copy/lease rules, churn and sanitizer testing of the native build |
| False security claim | Token theft, server impersonation, or exploitable admission path | Separate transport encryption, server trust, account authentication, authorization, and upstream DDoS controls in docs and tests |
| Bulk traffic starves motion/control | Rubber-banding and timeouts during chunk load | Separate lanes, bounded pieces, priority/weight policy, queue-age metrics, and congestion-triggered bulk pause |
| Reliable queues grow without bound | Memory exhaustion under slow clients or abuse | Hard per-peer/global byte caps, producer backpressure, timeouts, and explicit disconnect policy |
| Conservative 1000-byte realtime limit wastes path capacity | More messages and headers | First optimize interest/deltas and batching; raise only from PMTU and capture evidence |
| Protobuf hot-path overhead | Excess bytes or CPU at high rates | Benchmark generated C# codec against a bounded packed format using real captures; retain Protobuf for control regardless |
| Transport abstraction hides useful GNS signals | Poor adaptation and debugging | Include queue delay, RTT, loss, send rate, and congestion estimates in `TransportStats`; avoid lowest-common-denominator design |
| Direct-IP server is link-flooded | Unreachable service despite healthy process | Hosting firewall, upstream mitigation or relay, hidden origin where possible, and NET-08 operational plan |
| Three rate knobs drift into incompatible configurations | Bursty or unstable gameplay | Server negotiates permitted profiles; NET-06 owns measured profiles, not arbitrary client selection |

## Dependencies

- `ARCH-01`/`WORLD-08` define authority and the 20 Hz `WorldTick`; `NET-06` owns packet-cadence experiments.
- NET-02 defines input redundancy, snapshot sequencing, reconciliation, and transaction idempotence.
- NET-06 must measure serialization, interest management, payload distributions, compression, CPU, and rate profiles.
- NET-07 owns protocol/version compatibility and schema evolution details.
- NET-08 owns trust, abuse prevention, DDoS infrastructure, connection admission, and operational limits.
- WORLD-04 owns durable transaction acknowledgement where a network reply alone is insufficient; NET-09 owns client content/mod agreement.
- The future build/release design must package and test the pinned native GNS client/server artifacts reproducibly before this choice can graduate from prototype-gated to greenlit.
- The platform roadmap determines whether WebRTC, relay services, browser support, or console-certified transports become mandatory.

## Rejected or deferred alternatives

- **Custom UDP + Protobuf:** rejected. Protobuf is a payload codec, not congestion control, encryption, reliability, admission, MTU handling, or operational telemetry.
- **64/128 Hz authoritative world simulation:** rejected for v1. It multiplies
  packet/history/CPU cost and cannot overcome Internet RTT by itself. Later packet
  cadence experiments do not change the 20 Hz `WorldTick`; any future simulation-rate
  change requires a new architecture and compatibility decision.
- **TCP-only gameplay:** rejected for the realtime path because retransmission-induced stream ordering can delay newer state behind old loss. It remains acceptable for external control-plane services where latency supersession is irrelevant.
- **QUIC through `System.Net.Quic`:** deferred until a supported managed DATAGRAM API and game-specific benchmarks exist, or until native MsQuic integration is justified.
- **WebRTC DataChannels:** deferred until peer hosting, browser targets, or difficult NAT traversal justify ICE/signaling/SCTP complexity.
- **Godot high-level multiplayer as the protocol contract:** rejected because the headless server must not depend on Godot scene/RPC identity.
- **LiteNetLib as the immediate production default:** deferred as the fallback because its documented feature set leaves security/congestion questions to integration work.
- **One reliable ordered channel:** rejected because chunk transfer can delay critical control messages.
- **Reliable movement snapshots:** rejected. Obsolete state should be superseded, not retransmitted behind newer state.
- **Unbounded transport queues:** rejected under all implementations.
- **Claiming transport encryption makes the game “DDoS safe”:** rejected as a category error; upstream network capacity and application admission are separate concerns.

## Source-quality notes

- GNS, LiteNetLib, Luanti, Veloren, and .NET claims are linked to their official source repositories or vendor documentation. Repository READMEs establish advertised behavior, while only the prototype can establish VibeCraft-specific performance and packaging quality.
- The RFCs are normative primary sources for UDP, QUIC, and WebRTC transport behavior. They do not constitute benchmarks of the selected libraries.
- Mojang's NetherNet guide is first-party protocol documentation. It establishes current Minecraft use of WebRTC for the documented peer-to-peer path, not that every Minecraft edition or connection mode uses WebRTC.
- The bandwidth table is an explicit VibeCraft planning calculation with an assumed 64-byte overhead. It is neither a sourced GNS overhead claim nor a capacity promise; packet captures must replace it.
- Statements labeled as recommendations or inferences combine the cited mechanisms with VibeCraft's requirements. They should not be attributed to the source projects.
