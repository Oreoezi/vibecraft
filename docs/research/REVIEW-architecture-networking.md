# Adversarial architecture and networking review

Status: Review complete — decision set is **not ready to greenlight as a unit**  
Date reviewed: 2026-08-09  
Scope: `design_doc.md`, `FOUNDATION-00`, `ARCH-01` through `ARCH-04`, `NET-01` through `NET-09`, `WORLD-01`, `WORLD-02`, `WORLD-05`, `WORLD-08`, and `PROTOTYPE_PROGRAM`

> Snapshot note: this review intentionally preserves findings against the first-wave
> briefs. Its accepted corrections have since been applied. Read present-tense defect
> descriptions and the edit matrix as historical red-team evidence; current authority
> is [`INTEGRATION-RESOLUTIONS.md`](INTEGRATION-RESOLUTIONS.md) plus the proposed
> requirements baseline.
>
> **Owner update, 2026-08-13:** `questions2.md` selected fixed 60 TPS,
> standalone GNS, and receive-time/current-state action validation. Any 20 Hz,
> nested-controller, or GNS-as-candidate recommendation below is superseded rather
> than a current implementation choice.

## Verdict

The broad direction is sound: authoritative server state, a Godot-free shared movement kernel, local prediction, remote interpolation, one live-world writer, sparse finite sections, revisioned publication, bounded queues, and explicit persistence barriers all reinforce each other.

The current briefs nevertheless describe more than one architecture. The largest split is not cosmetic: networking documents build their ordering, replay, history, overload, and benchmarks around a 32/64 Hz master simulation, while `WORLD-08`, `WORLD-02`, and `WORLD-05` build around one 20 Hz/50 ms world tick. Actions are simultaneously specified as part of the unreliable ordered input timeline and as separate reliable commands on another lane. GNS is called the recommended public transport before its C#/Godot packaging, self-hosted certificate path, direct-IP identity, address-validation behavior, and buffer ownership have been proven. Several acceptance tests are physically impossible at their stated bandwidth.

Use this coherent v1 baseline while the experiments run:

1. One authoritative `WorldTick` at 20 Hz owns player movement, interactions, world mutation, entities, and deterministic commit order. Rendering is independent. Input and snapshot *transmission* may be separately paced, but no packet creates a second simulation clock.
2. Clients send bounded input intent; the server commits outcomes. The owner predicts movement only, initially against confirmed collision. Remote entities interpolate authoritative snapshots.
3. Keep three semantic traffic classes—realtime, control, and bulk—but do not equate a traffic class with a GNS lane or with application ordering. Every cross-class dependency uses explicit IDs, epochs, revisions, and acknowledgements.
4. Keep the server core host-agnostic. A supervised child process over loopback is the leading desktop topology, but remains an experiment until packaging, startup, shutdown, and native transport behavior pass on the declared platforms.
5. Do not ship support leases, combat rewind, speculative collision, or four seconds of section-version history as v1 defaults. Test them against a current-time baseline and adopt only the smallest mechanism that produces a measured benefit without a new exploit.
6. Do not expose a public direct-IP server until peer identity, channel binding, admission cost, and operator/upstream DDoS responsibilities are concrete and tested. Encryption, server identity, player authentication, authorization, and DDoS mitigation are separate gates.

## Classification summary

| Area | Disposition | Coherent resolution |
| --- | --- | --- |
| Server authority and narrow owner prediction | **Greenlight** | Server accepts intent and owns all durable outcomes; owner predicts movement/presentation; remotes interpolate. |
| One live-world writer and immutable worker publication | **Greenlight** | One authoritative world thread publishes generation, lifecycle, network-observation, and save state at named barriers. |
| V1 authoritative rate | **Greenlight at 20 Hz** | Use one 20 Hz `WorldTick`; remove 32/64/128 master profiles from v1. Higher-rate player substeps are deferred behind evidence. |
| 32/64/128 Hz global or player-facing simulation profiles | **Reject for v1** | They create incompatible replay grids and content timing without demonstrated value. |
| Input/snapshot send cadence | **Experiment** | Start at one 20 Hz input bundle and at most one 20 Hz snapshot; measure batching/coalescing before increasing sends. |
| Three traffic classes | **Greenlight semantically** | Preserve realtime/control/bulk semantics, with explicit app-level dependencies and bounded queues. |
| GNS as the selected implementation | **Experiment** | It is the first candidate, not the decided production transport. Reject it if authenticated direct IP, native packaging, ownership, or lane isolation fails. |
| Standalone direct-IP GNS without peer certificates/shared trust | **Reject for public use** | Valve documents this mode as vulnerable to MITM. Loopback with a one-time local token is a separate, lower-risk case. |
| Separate-process singleplayer | **Experiment** | Keep child and embedded host adapters around one server core; ship the child only after platform gates pass. |
| Confirmed-collision movement prediction | **Greenlight** | It is the safe baseline and contains world-history coupling. |
| Four-second copy-on-write section collision history and 500 ms wait-on-miss | **Reject as the baseline** | Prototype a byte-capped local cell-change journal; reset immediately on a missing dependency. |
| Idempotent, revision-checked block actions | **Greenlight** | Current-time validation, explicit `ActionId`, expected state/revision, and authoritative repair are sufficient for the first slice. |
| 150 ms combat/entity rewind | **Defer** | Combat is not required to select the movement/transport architecture; design it with the combat ruleset. |
| Server-side support-loss lease | **Experiment, disabled by default** | Compare against current-time authority and presentation-only smoothing; do not make it a v1 dependency. |
| Layered 3D interest scopes and bounded scheduling | **Greenlight conceptually** | Replace impossible settling metrics and transport-supplied “budget” assumptions with measured bytes, queue age, and admission gates. |
| Application abuse resistance | **Greenlight as a responsibility boundary** | Bounds, rate limits, no pre-auth world/plugin work, and graceful degradation are required. |
| “DDoS safe/proof” | **Reject** | Volumetric protection is an infrastructure/operator property, not an in-process transport feature. |
| Content-lock hashing | **Greenlight for compatibility/integrity** | A cooperating client verifies local bytes and reports the selected lock. It does not prove possession or execution to a hostile server peer. |

## Prioritized blocker list

### P0 — must be resolved before implementation architecture is frozen

1. **There is no single authoritative clock.** `NET-01`, `NET-02`, `NET-03`, and `NET-06` assume 32 Hz or configurable 32/64/128 Hz simulation. `WORLD-08` declares 20 Hz; `WORLD-02` and `WORLD-05` benchmark against a 50 ms tick. Pause acknowledgements, collision revisions, lag-comp history, save observations, scheduled ticks, action timing, replay logs, and plugin callbacks consequently have ambiguous tick identities.
2. **The selected transport has no completed public-server trust story.** GNS says ordinary direct-IP connections without certificates or an out-of-band shared secret are vulnerable to MITM. `NET-03` acknowledges this in prose but still recommends GNS, expects an intentionally wrong server identity to be rejected, and delegates the missing mechanism circularly to `NET-07`/`NET-08`.
3. **Input/action ordering contradicts the lane design.** `ARCH-01` and the Bedrock-derived rationale place movement and world actions on one ordered input timeline. `NET-02` and `NET-04` place discrete actions on reliable control, which GNS may receive before or after unreliable movement. The bounded hold queue is a workaround, not the “one ordered timeline” claimed by the decision.
4. **The transport interface cannot safely express its proposed native implementation.** `TrySend` loses GNS error distinctions; `ListenAsync` cannot report the OS-selected endpoint needed by `ARCH-04`; `Poll(Span<TransportMessage>)` does not define whether native receive memory is copied or leased; and no per-message reliable cancellation API supports the claimed cancellation of in-flight bulk work.
5. **Movement reconciliation depends on an unbudgeted world-history subsystem.** The four-second section history has no byte cap, and `CollisionStamp` has no maximum cardinality. Missing history pauses the visible predicted path for up to 500 ms while requesting a bulk baseline—the same bulk path whose congestion is expected not to affect movement.
6. **The support-loss lease creates gameplay authority from latency estimates.** It can be triggered by a collaborator or alternate account, changes falling/trap behavior, has an ambiguous “grounded episode” reset, and grants different collision to different players. The cited lag-comp systems do not support this novel mechanism.

### P1 — blocks public multiplayer or a production-quality singleplayer host

7. **Child-process singleplayer is presented as a conclusion the evidence does not establish.** Minecraft, Luanti, Veloren, and Unity support reuse of the server role; most cited examples are embedded. The child preference is a reasonable VibeCraft inference, not a sourced best practice, and it depends on undeclared platform/package targets plus the unresolved native transport.
8. **Interest-management acceptance criteria violate the link budget.** `NET-05` asks a 3×3×3 safety neighborhood to become active within 500 ms at 2 Mbit/s while its fixture uses 256 KiB section payloads. Twenty-seven such sections are 7,077,888 bytes, about 56.6 Mbit, and require about 28.3 seconds at 2 Mbit/s before protocol overhead. Even one uncompressed section exceeds one second.
9. **Security boundaries are named but not connected.** The documents do not specify how a server key is provisioned, how a client trusts it, how capability negotiation is bound to that authenticated channel, or which resources GNS allocates before its application callback. The QUIC three-times anti-amplification rule is not evidence that GNS has the same contract.
10. **The prototype dependency graph is circular and incomplete.** `NET-03` needs `NET-08` trust/admission while `NET-08` requires `NET-03`; `NET-06` and `WORLD-08` require each other; `ARCH-04` needs `WORLD-04`, `WORLD-05`, `ARCH-05`, `NET-03`, and `NET-07`, but P2 treats all of them as one deliverable. `GAME-01`, `ARCH-05`, and mod documents are hard dependencies in briefs but absent from the architecture/network P2 gate.
11. **Content “proof” overclaims an unauthenticated report.** `NET-09` correctly concedes that a modified client can lie, but its decision still says the client “proves possession.” A hash response proves only what a cooperating client computed or chose to report.
12. **Capacity thresholds have no product fixture.** Many documents use 16 or 64 clients, radius 12, 512 sections, or 1,000-byte snapshots without a declared v1 player count, view distance, hardware, operating systems, or server uplink. Those are research loads, not greenlight thresholds.

## Detailed findings and resolutions

### 1. Collapse the 20 Hz versus 32/64 Hz split into one clock

The conflict is wider than one constant:

- `NET-01` “Context and constraints” and “Fixed authoritative tick and phase order” select 32 Hz and put movement before interactions.
- `NET-02` sizes input windows, redundancy, replay, collision history, and thresholds in 32 Hz steps.
- `NET-03` uses 32 Hz simulation/input and 16 Hz snapshots in its choice and capacity model.
- `NET-06` makes 32/64/128 Hz the master simulation profiles, changes snapshot rates with them, and expresses gameplay in nanosecond deadlines.
- `WORLD-08` owns a persisted 20 Hz `WorldTick`, tick-count gameplay semantics, scheduled queues, random ticks, entity phases, and replay hashes.
- `WORLD-02` and `WORLD-05` use a 50 ms simulation budget in their greenlight tests.
- `ARCH-04` reports `effective_server_tick`, but cannot say which clock it pauses.

The sources establish possibilities, not a VibeCraft optimum. Minecraft's documented 20 TPS is evidence that a voxel game can work at 20 Hz; Source/Unity demonstrate separate simulation and replication rates. Neither source justifies the exact 32/16 profile or a user-visible 128 Hz mode.

**Resolution:** make `WORLD-08` the owner of authoritative time and use exactly one 20 Hz `WorldTick` in v1.

- Player movement, projectiles, interactions, support state, entities, and block commits execute once in the documented world phase graph. There is no separate 32 Hz “gameplay step.”
- Client rendering and input-device sampling remain frame-rate independent. The client creates one quantized movement frame per predicted `WorldTick` and sends a bundle at 20 Hz containing the newest frame plus the previous two unacknowledged frames.
- Generate at most one authoritative snapshot per `WorldTick` for the prototype. The transport may coalesce an obsolete unsent snapshot; it may not invent a second simulation grid.
- AI, activation, persistence, and other slower systems run on divisors/deadlines of the 20 Hz clock.
- Tick-based mechanics persist absolute `DueWorldTick`. Real-duration configuration converts to ticks with a named rounding rule; it does not accumulate floating nanoseconds independently of replay.
- `ClientTick` is renamed or explicitly scoped as a client prediction sequence. Time synchronization estimates which `WorldTick` a view represented for future lag compensation; it never grants elapsed simulation.
- If the 20 Hz predicted controller fails a blind feel/correction test, the next experiment is a 40 Hz player substep nested exactly twice per world tick. Do not expose 32/64/128 profiles first; a second commit grid is an architecture change, not a setting.

Disposition: **greenlight 20 Hz as the v1 contract; reject 32/64/128 master profiles; defer a higher-rate nested movement experiment until 20 Hz fails measured criteria.**

### 2. Keep authority, but define one action timeline

The authority matrix is strong. The packet sketches are not consistent with it:

- `ARCH-01` embeds `repeated ActionIntent actions` inside `InputFrame` and says movement/actions share one ordered stream.
- `NET-02` sends place, break, attack, use, and inventory as separate reliable commands that may arrive before movement and be held.
- `NET-04` defines a separate `ActionRequest` with `client_tick` and `latest_input_sequence`.
- GNS explicitly gives no cross-lane receive-order guarantee. A reliable control action and an unreliable input cannot form one transport-ordered timeline.

**Resolution:** define one causal application contract without relying on cross-lane order.

- `InputBundle` carries redundant sequenced movement frames and a bounded set of pending latency-sensitive world intents (`begin_break`, `finish_break`, `place`, `attack`, `use`). Each intent has an `ActionId` and references one included input sequence.
- The client repeats a pending world intent in later unreliable bundles until an authoritative result acknowledges it or a bounded timeout expires. The server deduplicates by `ActionId`; repetition is reliability, not repeated execution.
- Inventory, crafting, chat, administration, connection lifecycle, and other non-movement-sensitive transactions use reliable control directly.
- `ActionResult` is reliable control and may also be summarized in snapshots. A result can be sent only after the referenced input was accepted or explicitly rejected, so result arrival never makes the client guess server ordering.
- Remove `session_id` from hot messages after the authenticated connection/session is established; the connection and replay epoch already identify it.
- Define wrap-safe sequence comparison, maximum pending actions, timeout behavior, and reconnect epochs before field numbering.

This preserves the useful Bedrock lesson—movement-dependent actions name the same input timeline—without claiming that two GNS lanes are ordered.

Disposition: **greenlight the semantic model after this rewrite; reject the current dual representation of the same world action.**

### 3. Narrow collision history to the local correction problem

`NET-02` currently asks the client to retain copy-on-write section versions for at least 128 controller steps, puts a set of touched section revisions in each reconciliation state, and waits up to 500 ms for missing history. Problems:

- “Copy-on-write references/deltas” is not a representation or budget. In a hot 3×3×3 neighborhood, edits from redstone, fluids, explosions, or other players can retain many palette/direct buffers. Duration alone does not cap bytes.
- The number of sections touched is assumed small but is not bounded by speed, collision-shape complexity, boundary position, or future vehicles.
- Section revisions received through terrain replication may lag the owner correction. This lets bulk-stream health become a prerequisite for movement convergence.
- Keeping the old rendered trajectory for 500 ms after logical history is missing produces a long, misleading movement path followed by a likely snap.
- The server does not rewind late movement, so it does not need a general authoritative terrain rollback facility for ordinary reconciliation.

**Resolution:** start with confirmed collision and a byte-capped client-local change journal.

- Do not give predicted block overlays collision in the first slice. Visual cracks, ghost placement, sound, and particles remain immediate.
- Within the local player's collision safety neighborhood, retain collision-affecting cell changes as `(effective_world_tick, position, old_shape, new_shape, section_revision)`.
- Size retention from measured RTT/jitter, clamped to 250 ms–1 s, with a hard 4 MiB initial cap. Duration and bytes are both enforced; the prototype may change the cap.
- Reconciliation snapshots carry the processed input sequence and a collision-stream epoch/contiguous revision, not an unbounded `CollisionStamp` list on every snapshot.
- Replay applies journaled changes at their effective world tick. If the epoch or required delta is missing, reset logical state immediately to authority, clear incompatible prediction, request the baseline, and use only collision-safe presentation smoothing. Do not freeze the old path while waiting.
- Compare this cell journal with section COW in the prototype. Section COW wins only if it meets the same byte cap and materially lowers replay CPU.

Combat/world historical queries in `NET-04` are a separate server-side history and must not reuse the client prediction journal by accident.

Disposition: **experiment with the bounded journal; reject the four-second COW/500 ms wait as the default contract.**

### 4. Remove support-loss grace from the required architecture

The support lease is novel policy, not a consequence of authority or the cited Source/Unity rewind mechanisms. Its abuse and coupling are substantial:

- A collaborator can remove support to grant another player temporary nonphysical support. “Another actor” does not distinguish attack from collusion.
- “One lease per grounded episode” has no exact reset. Reaching another support ends the lease and may immediately establish a new grounded episode, enabling chained bridges unless more hidden state is added.
- RTT/2 is not measured one-way delay on asymmetric paths. A latency estimate becomes a gameplay ability.
- During the lease, rendering/raycast/world state says air while one player's downward collision says solid. Plugins, traps, knockback, jumping, fall damage, and replay need to understand the exception.
- The 50–150 ms constants do not align cleanly with either the current 20 Hz or proposed 32/64 Hz clocks.
- It compensates before evidence shows that ordinary input prediction, deterministic movement-before-edit ordering, and visual smoothing fail the product scenario.

**Resolution:** use current-time authoritative support in the v1 baseline. Process movement before block interactions in each 20 Hz tick; reconcile late cross-client ordering normally. Smooth only presentation when the corrected path is collision-safe.

Keep one A/B experiment in the harness: an optional, at-most-one-`WorldTick` support grace that cannot permit jump/flight-like vertical gain, renew, or affect actions. It must be tested with colluding clients, repeated placement/removal, pistons/explosions, knockback, boundary movement, and trap fixtures. Adoption requires a blind playtest benefit and zero extension of legal horizontal/vertical reach in the abuse corpus. If it fails, delete it rather than adding more exception state.

Block actions should initially use current-time actor/world validation, expected target state/revision, server-owned break duration, and idempotent results. The 150 ms historical melee/target rewind is deferred until combat mechanics and defender-fairness goals exist.

Disposition: **greenlight revisioned current-time block transactions; experiment with support grace off by default; defer combat rewind.**

### 5. Treat GNS as a candidate, not a selected transport

The GNS capability claims are mostly accurate, but the recommendation outruns them:

- Valve's official interface says direct-IP connections get basic encryption, but without certificates or an out-of-band shared secret the peer is unknown and the connection is vulnerable to MITM ([`ConnectByIPAddress` documentation](https://github.com/ValveSoftware/GameNetworkingSockets/blob/master/include/steam/isteamnetworkingsockets.h#L2258-L2336)). IP-address identities are explicitly unauthenticated ([identity definitions](https://github.com/ValveSoftware/GameNetworkingSockets/blob/master/include/steam/steamnetworkingtypes.h)).
- Certificate APIs exist, but use an application/game-coordinator provisioning path; the documents do not show that VibeCraft can issue, distribute, validate, rotate, and bind these certificates in the standalone open-source build ([certificate API](https://github.com/ValveSoftware/GameNetworkingSockets/blob/master/include/steam/isteamnetworkingsockets.h#L979-L1001)).
- Valve lists the flat C API and third-party C# bindings; this is evidence of bindability, not an official supported C# package or a Godot export story ([GNS README](https://github.com/ValveSoftware/GameNetworkingSockets#language-bindings)).
- The listen API documentation says a specific local port must be selected. `ARCH-04` assumes `127.0.0.1:0`; whether the pinned build accepts port zero and reports an ephemeral endpoint must be tested, not inferred.
- GNS receive messages must be explicitly released. The current `ReadOnlyMemory<byte>` interface cannot represent that ownership safely without a copy or lease ([receive API](https://github.com/ValveSoftware/GameNetworkingSockets/blob/master/include/steam/isteamnetworkingsockets.h#L691-L708)).
- The public send API can return invalid, disconnected, ignored/no-delay, and queue-limit results. `bool TrySend` collapses operationally different outcomes.
- The public API exposes flush and close, but no per-message “unsend” for queued reliable data. Cancelling a chunk after enqueue therefore means stopping future fragments and ignoring stale received fragments, not retracting bytes already accepted by GNS.

**Resolution:** retain a VibeCraft-owned interface, but change its minimum contract before binding it:

```text
ListenAsync(...) -> bound endpoint/listener handle
ConnectAsync(endpoint, peer-trust policy, ...) -> connection
TrySend(...) -> Sent | WouldBlock | DroppedNoDelay | Closed | Invalid
Receive(...) -> owned message lease that must be disposed, or an explicit pooled copy
Stats -> send-rate estimate, queue bytes/age per class, RTT/loss, auth/peer identity state
```

Separate `TrafficClass` from delivery semantics in the API/message catalog. The initial mapping is:

| Class | Delivery | Content | Cross-class rule |
| --- | --- | --- | --- |
| Realtime | Unreliable, sequenced/superseding at app level | input bundles, owner/remote snapshots | Duplicates and stale messages are harmless; no dependency on receipt order with control/bulk. |
| Control | Reliable ordered within its lane | handshake after transport trust, action results, inventory, lifecycle, teleports | Must never depend on a bulk message having arrived. |
| Bulk | Reliable ordered fragments initially | immutable section/manifest baselines | Object ID, fragment index, epoch, length, and hash; stale data ignored. Stop producing on cancel, but assume already queued reliable fragments still arrive. |

GNS lanes schedule sending, not application causality. Valve states that priorities/weights affect send order, that different lanes may be received out of order, and that only reliable messages on the same lane have a strong order guarantee ([lane documentation](https://github.com/ValveSoftware/GameNetworkingSockets/blob/master/include/steam/isteamnetworkingsockets.h#L825-L887)). Do not hard-code lane priorities until the pinned build proves:

- control latency remains bounded;
- realtime does not queue behind bulk;
- bulk makes measurable progress under continuous realtime traffic;
- a lost reliable bulk fragment does not block control;
- queue limits and cancellation semantics match the wrapper;
- both send directions are configured and tested independently.

If authenticated direct-IP certificates/channel binding cannot be implemented with the standalone build, or if native packaging/lifetime fails after one focused remediation, reject GNS for public v1 and reopen the transport decision. LiteNetLib is only a fallback experiment; its missing documented security/congestion pieces cannot be waved away.

Disposition: **experiment.**

### 6. Make identity, authentication, admission, and DDoS four explicit gates

Current documents use “auth” for several different things:

1. **Transport confidentiality/integrity:** packets cannot be casually read or altered.
2. **Server identity:** the client knows which server key it reached.
3. **Player/account identity:** the server knows which login/session principal connected.
4. **Authorization:** that principal may join, mutate, administer, or use content.
5. **Address validation/admission:** a spoofed source cannot cause amplification or material allocation.
6. **Volumetric protection:** upstream capacity/firewall/relay can absorb traffic before the host link saturates.

GNS without certificates covers only part of item 1 and explicitly does not establish item 2. A bearer account token sent inside a MITM-able channel does not repair item 2; an attacker can terminate two transports. Capability “transcript binding” in `NET-07` is therefore not implementable until `NET-03` exposes an authenticated peer/channel binding.

`NET-08` also imports QUIC's three-times pre-validation amplification limit as a VibeCraft goal. That is a useful target, but the cited RFC proves QUIC's rule, not GNS behavior. GNS says a few basic packets may prevent trivial spoofing before creating a connection object; that is not a documented stateless-address-validation or amplification contract.

**Resolution:**

- Local private singleplayer uses loopback binding plus the one-time token delivered over the parent control channel. It makes no security claim against a privileged local user.
- A public/self-hosted server must present a transport-authenticated persistent server key. Clients pin it through a trusted directory or explicit first-use fingerprint policy. If GNS certificates cannot provide this in the standalone build, GNS fails the public gate; do not invent an unreviewed crypto wrapper during gameplay implementation.
- Player authentication runs only after server identity and transport integrity are established. The production provider may be selected later; the protocol exposes an opaque bounded credential exchange, not a hard-coded account vendor.
- Protocol/capability selection is cryptographically bound to the authenticated channel. Until that primitive exists, downgrade tests cannot pass.
- Before verified address/admission state, the measured pinned transport must perform fixed bounded work, allocate no world/player/plugin/chunk state, and stay within the declared amplification limit. Packet capture—not a README capability list—is the gate.
- Public server documentation must distinguish application floods from link saturation and require provider/firewall/relay support for the latter. Residential direct hosting remains vulnerable by design.
- No metric uses raw IP as unbounded cardinality; prefix/source limiter state itself needs fixed-size eviction and global caps.

Disposition: **greenlight the boundary; block public exposure until the candidate transport proves it. Reject “DDoS safe.”**

### 7. Preserve singleplayer parity at the server-core boundary, not only the socket boundary

The evidence supports one simulation role and explicit lifecycle. It does not demonstrate that a child process is universally superior. The child has real benefits—client fault isolation, independent logs, dedicated parity—but also adds native-library duplication, memory, helper packaging/signing, process supervision, pipe framing, firewall behavior, platform-store restrictions, and slower startup.

Several current statements are inconsistent:

- `ARCH-04` requires a supervised child and exact loopback UDP.
- `WORLD-05` says singleplayer starts the same server **in-process**.
- `FOUNDATION-00` permits local transport to bypass serialization behind the same interfaces.
- `PROTOTYPE_PROGRAM` treats the child topology as one behavior inside a larger network prototype rather than a decision comparison.
- `ARCH-04` assumes Windows, Linux, and macOS without a project-level supported-platform decision.

**Resolution:** factor the architecture as:

```text
ServerCore (Godot-free simulation, lifecycle, persistence, protocol dispatcher)
  -> DedicatedProcessHost
  -> ChildProcessHost candidate (stdio control + loopback gameplay)
  -> EmbeddedHost fallback (same dispatcher; in-memory message transport)
```

Parity means identical authoritative handlers, tick phases, validation, save barriers, and protocol conformance fixtures. It does not require every local test to pay native packetization overhead. Shipping may still use loopback specifically to exercise the real stack.

Prototype the child first on Windows x64 and Linux x64. Compare it with the embedded fallback for startup time, steady memory, pause/save semantics, crash containment, orphan behavior, export packaging, and protocol traces. Defer macOS as a shipping commitment until helper signing/notarization and native-library loading are demonstrated. Select the child only if all lifecycle gates pass and its startup/memory cost is acceptable; otherwise ship the embedded host without changing `ServerCore` or gameplay semantics.

The child process isolates the client from a server crash. It does not sandbox server plugins from the world, filesystem, credentials, or server process. All docs and UI must keep that distinction.

Disposition: **experiment; host-agnostic core is greenlit.**

### 8. Repair interest/backpressure contracts

The layered scopes in `NET-05` are appropriate, but several interfaces and tests assume powers the transport does not expose:

- GNS reports estimated send rate, pending bytes, and queue time. It does not hand the application a token budget that `NET-05` can consume atomically. The scheduler must derive an admission budget conservatively and react to queue age; GNS remains the final congestion owner.
- Strict lane priority can starve a lower lane. Application priority aging cannot force GNS to send bulk if the configured higher lane never empties.
- Baselines, deltas, unloads, and entity spawns cross reliable lanes. Their correctness comes from section epoch/revision and explicit application acknowledgement, never send order.
- A reliable baseline already accepted by GNS cannot be cancelled per message. `LEAVING` stops additional fragments and the receiver ignores the stale epoch.
- The 500 ms safety-neighborhood gate is arithmetically impossible for the stated fixture.

**Resolution:**

- Replace “transport-provided byte budget” with a controller using configured cap, GNS send-rate estimate, per-lane pending bytes/queue age, and measured delivered rate. The app stops producing before the native queue ceiling.
- Configure lanes only after a starvation test; require positive bulk progress and bounded realtime/control age rather than assuming priority does both.
- Do not require all 27 full section baselines before admission. Define a measured **collision envelope**: the containing section plus only neighbor cells/shapes reachable by the controller before the next streaming deadline. Gate player movement until that compact envelope is applied, then expand terrain normally.
- Replace fixed “500 ms at 2 Mbit/s” with `settle_time >= transmitted_bytes * 8 / usable_bitrate` plus protocol/processing margin. Acceptance uses actual compressed bytes and reports them.
- Section deltas need a dedicated P3 choice: reliable bulk fragments per section or unreliable revisioned deltas repeated until section acknowledgement. Do not silently route arbitrary block storms through the sparse control lane.
- Add an independently capped vertical entity radius; `entity_radius = horizontal_full_radius` is incomplete in a 3D interest model.

Disposition: **greenlight the layered model after these corrections; experiment with codec/radii/scheduler values.**

### 9. Fix dependencies and remove premature constants

The briefs often label future decisions as hard `Requires`, making the research impossible to execute in dependency order. Break dependencies into three categories:

- **Architectural invariant:** must be decided before the prototype (authority, one writer, finite section key, bounded ownership).
- **Stub contract:** prototype can define the smallest fake interface (registry IDs, persistence barrier, auth principal, plugin-disabled mode).
- **Production dependency:** must be complete before shipping, not before measuring the architecture.

Specific corrections:

- `NET-01` and `ARCH-01` duplicate the authority decision. Make `ARCH-01` normative and `NET-01` its network realization.
- `NET-06` and `WORLD-08` cannot own each other's clock. `WORLD-08` owns `WorldTick`; `NET-06` becomes packet/snapshot cadence and capacity research.
- `WORLD-08` says it requires `ARCH-02` for clock/phases, but `ARCH-02` defines the data model, not a clock. Change this to “coordinates with.”
- `NET-03` and `NET-08` are circular. `NET-03` must expose candidate admission/auth facts; `NET-08` evaluates them against a threat model.
- Core `NET-07` version negotiation does not require `ASSET-02` or `MOD-01`; those systems later register bounded namespaced capabilities. Remove them as blockers.
- `NET-09` does require the authenticated transport/session from `NET-03`/`NET-08`, which its dependency section currently omits.
- `ARCH-04` can prototype lifecycle with plugins disabled and a fault-injecting persistence stub. `ARCH-05` and full `WORLD-04` are production greenlight dependencies, not reasons to prevent topology measurement.
- `GAME-01` must supply stable registry/collision fixture IDs before persistent/network schemas freeze, but a tiny fixed registry is enough for P2.

Treat all queue lengths, smoothing windows, correction thresholds, view radii, worker counts, and memory percentages in these briefs as experiment inputs until target platforms, reference hardware, player count, view distance, and uplink are written. “Falsifiable” does not make an arbitrary threshold a product requirement.

## Source-claim audit

| Claim in the briefs | Review finding | Required correction |
| --- | --- | --- |
| Godot physics is not deterministic. | **Supported.** Godot explicitly warns that identical-looking situations are not guaranteed to run identically. | Keep the shared custom controller conclusion; do not broaden it into a claim that every custom float controller is deterministic. |
| Minecraft's 20 TPS proves a 20 Hz architecture. | **Partially supported.** It proves viability, not optimality for VibeCraft. | Use 20 Hz as the coherent low-complexity v1 baseline and retain a measured feel test. |
| Source/Unity history supports VibeCraft support leases. | **Unsupported leap.** They support bounded entity/physics query history; neither source establishes per-player phantom voxel support. | Label the lease novel and optional; do not cite general lag compensation as validation. |
| GNS direct IP supplies encryption. | **Supported with a critical qualifier.** It supplies basic encryption, but Valve explicitly says certless/shared-secret-less connections are MITM-vulnerable. | Public greenlight requires authenticated peer identity/channel binding. |
| GNS has a suitable C# path. | **Feasibility only.** Valve provides a flat C API and lists third-party bindings, not an official maintained Godot C# distribution. | Keep native binding/package work as a hard prototype gate. |
| GNS lanes isolate traffic and order it. | **Only within limits.** Lanes affect send scheduling; receive order across lanes is not guaranteed; only reliable same-lane order is strong. | Add explicit application epochs/revisions and starvation tests. |
| GNS supplies an application send budget. | **Not supported as written.** It exposes estimates and queue status, while its congestion controller owns actual sending. | Use conservative producer admission from stats; do not promise an exact consumable budget. |
| Bulk messages can be cancelled when interest changes. | **Not supported by the public per-message API.** | Stop future fragments and ignore stale epochs; bound already queued bytes. |
| A child process is the implementation proven by Minecraft/clones. | **Unsupported.** Sources prove reuse of a server role; cited implementations are commonly embedded. | Label child preference a VibeCraft hypothesis and compare host adapters. |
| QUIC's three-times anti-amplification rule supports the GNS admission claim. | **Category mismatch.** It is a QUIC normative rule. | Measure the candidate transport's pre-validation bytes/state directly. |
| Content hash challenge proves possession. | **False against a hostile client.** A modified client can return an expected digest without possessing or executing the package. | Say “declares a matching lock”; use hashes for local verification and compatibility only. |
| .NET QUIC DATAGRAM is unavailable. | **Currently supported by the cited issue.** The official runtime issue remains open and targeted at .NET 12; managed QUIC streams are stable but DATAGRAM is not yet a supported API. | Keep QUIC DATAGRAM deferred, but recheck at transport freeze rather than encoding the current runtime state permanently. |

## Exact document edit map

| Document | Sections needing edits | Required change |
| --- | --- | --- |
| `design_doc.md` | `Netcode`; `Server` | Replace “movement cheats almost impossible,” `32/64/128 tick`, and “safe from DDOS” with server-authority, measured cadence, bounded admission, and upstream-mitigation requirements. |
| `FOUNDATION-00-spec-risk-audit.md` | `The server tick target is prematurely fixed`; `Cross-system invariants`; `Immediate experiments` | Name 20 Hz as the coherent v1 world baseline; remove the ambiguous local serialization bypass; split transport identity/admission from application flood tests. |
| `ARCH-01-authority-and-simulation.md` | `Context and constraints`; `Protocol-level contract`; `Client prediction and reconciliation`; `Prototype or benchmark`; `Greenlight criteria`; `Dependencies` | Use `WorldTick`; move latency-sensitive world intents into the repeated input bundle; replace full collision-version assumptions with the bounded journal experiment; remove 32 Hz constants. |
| `ARCH-02-simulation-data-model.md` | `Dependencies` | Treat the tick/owner boundary as `WORLD-08`; do not imply the entity store chooses scheduling or parallelism. |
| `ARCH-04-singleplayer-server-lifecycle.md` | `Decision`; `Options considered`; `Components and ownership`; `Bootstrap contract`; `Public interfaces`; `Prototype or benchmark`; `Greenlight criteria`; `Dependencies` | Change “ship child” to candidate; define host-agnostic `ServerCore`; test child versus embedded fallback; use `WorldTick`; test GNS port-zero behavior; narrow platforms to proven exports. |
| `NET-01-network-simulation-model.md` | `Decision`; `Context and constraints`; `Fixed authoritative tick and phase order`; `Core interfaces`; `Snapshot and recovery policy`; `Greenlight criteria`; `Prototype or benchmark`; `Dependencies` | Make `ARCH-01` normative; select 20 Hz; remove 32 Hz profile; use one action timeline and one tick domain. |
| `NET-02-movement-prediction-reconciliation.md` | `Decision`; `Shared controller contract`; `Input generation and transport`; `Server input admission`; `Authoritative response`; `Collision history and block prediction`; `Reconciliation algorithm`; `Greenlight criteria`; `Prototype or benchmark` | Replace 32 Hz constants; remove separate reliable world-action requests; replace four-second section COW and wait-on-miss with confirmed collision plus a byte-capped local journal. |
| `NET-03-transport-and-reliability.md` | Opening recommendation; `Decision`; `Option A`; `Game-owned abstraction`; `Three bounded lanes`; `Threading and backpressure`; `Handshake and trust boundary`; `Packet-size policy`; `DDoS boundary`; `Singleplayer`; `Prototype plan`; `Measurable success and failure criteria`; `Dependencies` | Mark GNS experiment; add authenticated direct-IP gate; repair endpoint/send/receive ownership APIs; state no cross-lane order/per-message cancellation; use 20 Hz fixture; make child topology independent. |
| `NET-04-block-interaction-lag-compensation.md` | `Decision`; `Shared time`; `Protocol surface`; `Block breaking`; `Combat`; `Remote support-loss grace`; `Greenlight criteria`; `Prototype or benchmark`; `Dependencies` | Greenlight current-time idempotent block edits only; move combat rewind to deferred; make support grace an off-by-default A/B experiment with collusion tests. |
| `NET-05-interest-management.md` | `Section baseline and delta lifecycle`; `Priority scheduler and congestion behavior`; `Initial defaults`; `Greenlight criteria`; `Prototype or benchmark`; `Dependencies` | Replace exact transport budget/cancel assumptions; define cross-lane dependencies; replace impossible 500 ms safety test with measured collision-envelope gating; add vertical entity scope. |
| `NET-06-tick-and-simulation-rates.md` | Entire decision, especially `Time model and profiles`, `Deterministic simulation phases`, `Subsystem cadence`, and `Overload policy` | Recast as network/input/snapshot cadence research subordinate to `WORLD-08`; remove 32/64/128 master profiles and duplicate world phase graph. |
| `NET-07-protocol-versioning.md` | `Handshake`; `Greenlight criteria`; `Risks and open questions`; `Dependencies` | Require an authenticated channel-binding primitive before downgrade claims; cap/canonicalize capabilities; remove asset/mod docs as core blockers; add fixed-size pre-auth mismatch behavior. |
| `NET-08-server-abuse-and-ddos-boundary.md` | `Connection admission`; `Greenlight criteria`; `Prototype or benchmark`; `Dependencies` | Do not inherit QUIC properties by analogy; measure GNS pre-validation allocation/amplification; define server identity/player auth/upstream boundaries; block public exposure until they pass. |
| `NET-09-client-content-agreement.md` | `Decision`; `Negotiation`; `Greenlight criteria`; `Dependencies` | Replace “prove possession” with cooperating-client declaration/local verification; add authenticated-session dependency; preserve the explicit no-attestation limitation. |
| `WORLD-01-chunk-coordinate-and-memory-model.md` | `In-memory section`; `Interfaces affected` | Define one signed/nonnegative revision representation shared by network/lifecycle schemas and a bounded collision-shape identity usable by the movement journal. |
| `WORLD-02-chunk-job-scheduling.md` | `Greenlight criteria`; `Dependencies` | Name the 20 Hz `WorldTick` owner rather than a generic 50 ms benchmark; distinguish prototype stubs from production worldgen/storage dependencies. |
| `WORLD-05-chunk-lifecycle.md` | `Context and constraints`; `Dirty revisions and save protocol`; `Prototype or benchmark`; `Dependencies` | Remove “singleplayer in-process”; use host-neutral server core; align revision types/epochs and 20 Hz tick terminology. |
| `WORLD-08-ticking-and-activation.md` | `Decision`; `Clock and overload semantics`; `Tick phases`; `Dependencies` | Remain normative for time; remove promises that 32/64/128 input cadence has value; correct the `ARCH-02` clock dependency; expose one immutable observation boundary for networking. |
| `PROTOTYPE_PROGRAM.md` | `P2 — Authoritative multiplayer slice`; `P3 — Streaming and rendering slice`; `Execution rule` | Replace one oversized P2 with the shared matrix below; include transport trust, topology comparison, actual link arithmetic, and explicit disposition per branch. |

## Minimal combined prototype matrix

Use one repository solution and one tiny editable voxel world. Reuse the deterministic trace format, impairment layer, bot clients, metrics, and fault injector across all rows; do not create five unrelated demos.

| Gate | Build/variants | Required cases | Decision output |
| --- | --- | --- | --- |
| **M0 — Clock, controller, and authority** | Godot-free server core plus client predictor at one 20 Hz `WorldTick`; 20 Hz redundant input bundles; 20 Hz/coalesced snapshots | 0–250 ms RTT, jitter/loss/reorder/duplicate, hitches, malicious rate/timer/state input, mutable support/path cells, golden traces on each target OS | Greenlight/revise authority, 20 Hz feel, missing-input rule, snapshot cadence, correction thresholds. Only if 20 Hz fails, add one 40 Hz nested movement branch. |
| **M1 — Transport, lanes, and trust** | Pinned GNS flat binding in packaged Godot/server builds; one focused fallback only after a GNS failure | Direct IP and loopback, all three traffic classes, 10 MiB paced bulk, cross-lane reorder, queue saturation, receive-lifetime churn, port-zero bind, wrong/untrusted server key, malformed/pre-validation floods | Greenlight or reject GNS; exact wrapper ownership; lane mapping; authenticated server-key path; measured admission/amplification facts. |
| **M2 — Local host and durability lifecycle** | Same `ServerCore` through child-loopback and embedded host adapters; fault-injecting persistence barrier | 100 start/pause/save/stop cycles, client/server kill, parent EOF/heartbeat loss, stdout/stderr flood, world lock, spaces/non-ASCII paths, export/package smoke tests, same protocol traces | Select child or embedded desktop default; freeze pause/orphan/save semantics. This gate does not claim plugin sandboxing. |
| **M3 — World actions and compensation** | Current-time revisioned block actions as control; confirmed collision journal; optional support-grace branch disabled by default | Competing edits, delayed baseline, break under moving player, colluding support edits, repeated bridge/trap attempts, knockback/jump/explosion, forged ticks/action replay | Greenlight current-time transactions; greenlight/reject bounded journal; independently adopt or delete support grace. Combat rewind remains out of scope. |
| **M4 — Streaming and backpressure** | Sparse sections, collision-envelope admission, synthetic measured compressed payloads, GNS queue stats, section epochs/fragments | 2 Mbit/s and faster links, teleport/AOI exit, stalled bulk receiver, baseline/delta gap, old epoch delivery, continuous realtime plus bulk | Freeze realistic settling formula, queue ceilings, fragment size, baseline/delta delivery policy, and initial view/simulation limits. |

Common mandatory gates:

- Declare Windows/Linux reference builds, CPU, GC mode, bandwidth, initial 16-player acceptance load, and whether macOS is in or deferred before interpreting performance numbers.
- No test can pass by raising correction tolerance, dropping authoritative work, disabling authentication, or allocating beyond a configured cap.
- Every branch ends in `greenlight`, `revise`, `defer`, or `reject`; failed measurements remain in the decision record.
- P2 implementation may use a tiny fixed registry, plugins disabled, and a fault-injecting persistence stub. Stable content/plugin/save APIs remain production dependencies, not excuses to merge all architecture research into one prototype.

## Final architecture after these resolutions

```text
Godot client
  presentation + input sampling
  20 Hz predicted voxel controller
  confirmed collision replica + bounded local change journal
  realtime/control/bulk protocol client

ServerCore (Godot-free)
  one 20 Hz authoritative WorldTick and deterministic phase graph
  sequenced input/action admission
  shared voxel controller
  revisioned world transactions and immutable observations
  bounded lifecycle/job/persistence interfaces

Host adapter (decision pending experiment)
  dedicated process
  supervised local child over loopback
  embedded local fallback

Candidate transport (GNS pending experiment)
  authenticated peer/channel requirement
  realtime: unreliable + app sequencing/redundancy
  control: reliable ordered + idempotent transactions
  bulk: bounded immutable fragments + epoch/hash
  no assumed ordering across classes

Deployment boundary
  address/admission limits in process
  player authentication/authorization after trusted transport
  upstream relay/firewall/provider for volumetric DDoS
```

This architecture gives implementation one authority, one clock, one world mutation order, one set of protocol semantics, and explicit experiment boundaries. It preserves the useful ambitions in the spec without turning unproven tick rates, process topology, transport security, collision history, or latency forgiveness into accidental compatibility contracts.
