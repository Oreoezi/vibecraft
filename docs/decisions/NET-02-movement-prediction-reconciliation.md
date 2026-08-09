# NET-02 Movement prediction, reconciliation, and anti-cheat boundary

Status: Proposed

Owner: Networking research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)  
Parent decision: [`NET-01-network-simulation-model.md`](NET-01-network-simulation-model.md)

## Decision

Recommended choice: Send bounded redundant input bundles on the single `WorldTick` timeline; simulate a shared custom voxel controller on client and server; acknowledge authoritative progress; reconcile by restoring the last acknowledged state and replaying unacknowledged input against confirmed collision plus a byte-capped local cell-change journal; interpolate remote entities from snapshots.

One-sentence rationale: Input replay preserves responsive local control and server ownership, while a bounded change journal addresses nearby voxel edits without making multi-second full-section history or bulk-stream health a prerequisite for movement convergence.

Do not use client animation state, position, velocity, grounded state, or client elapsed time as authority. Animation intent can be transmitted for presentation, but locomotion is derived from authoritative controller state.

## Context and constraints

- The player must feel local even when the round trip is 100–200 ms; waiting for the server is not acceptable.
- The server must stop speed, fly, phase, no-fall, timer, and packet-rate movement from changing authoritative state, without treating ordinary lag or stale chunks as proof of cheating.
- Voxel collision changes at runtime. Breaking the supporting block, placing a block into a path, receiving a delayed chunk, teleporting, knockback, water, ladders, and future moving blocks all invalidate naive “restore position and replay current world” logic.
- Client and server are both C#, but only the client has Godot. Shared movement code must use plain value types and voxel collision queries, not Godot physics objects.
- Godot explicitly says its physics is not deterministic across seemingly identical situations ([Godot physics introduction](https://docs.godotengine.org/en/stable/tutorials/physics/physics_introduction.html)). Replaying a `CharacterBody3D` result on a standalone server is therefore not a sound authority model.
- `WORLD-08` owns one 20 Hz V1 `WorldTick`. Rendering, camera, and input-device sampling remain frame-rate independent; prediction removes RTT but not fixed-step granularity. Only if the 20 Hz branch fails a measured feel test should the prototype add a controller substep nested exactly twice per world tick.
- “Make movement cheats almost impossible” is not an acceptance criterion. The measurable criterion is that malformed or impossible input cannot create movement outside the server's legal controller, while honest impaired clients remain stable.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| Trust client position with speed/distance thresholds | Very simple; client and Godot own movement feel | Threshold exploits, timer abuse, weak collision validation, false positives around world changes, constant anti-cheat patching | Reject. This recreates Java Minecraft/Luanti's retrofit problem. |
| Server movement, client waits for state | Exact authority and minimal reconciliation code | Adds roughly one RTT to movement; poor Internet play | Debug mode only. |
| Client predicts, server accepts client end position when “close enough” | Familiar and tolerant of minor mismatch | A tolerance is an exploit budget; client time and collision remain partially authoritative | Reject as the primary rule. Position may be diagnostic only. |
| Shared input simulation + ack/restore/replay | Server owns legal state; responsive; differences converge; inputs are compact | Requires shared controller, history, clock/tick discipline, and visual smoothing | **Recommended.** |
| Full rollback of player plus nearby world | Can resolve late collision changes exactly | State capture and side-effect rollback become complex; interactions with other players still require a global policy | Defer. Keep immutable collision history for replay, not authoritative world rollback. |

## Evidence

### Minecraft versions and ecosystem lessons

- Early Classic's community-reconstructed protocol lets a client send a position/orientation update described as a player teleport ([wiki.vg Classic archive](https://c4k3.github.io/wiki.vg/Classic_Protocol.html)). Java 1.12.2's reverse-engineered protocol likewise contains absolute serverbound position/look and clientbound correction/teleport-confirm packets ([wiki.vg Java 1.12.2 protocol archive](https://c4k3.github.io/wiki.vg/Protocol.html)). These are community sources, but they establish that Java's compatibility burden is built around position-reporting rather than a clean input protocol.
- Mojang's current Bedrock protocol notes show `PlayerAuthInputPacket` being queued and processed under a tick policy so sending more packets cannot move a client faster. It co-locates movement input and block actions when their relative order matters ([Mojang Block Breaking Overview](https://github.com/Mojang/bedrock-protocol-docs/blob/main/additional_docs/BlockBreakingOverview.md)).
- The same Mojang notes describe client-predicted block destruction and item durability, server validation, and explicit corrections. This supports predicting reversible feedback while retaining authoritative commit; it also demonstrates how inventory and world prediction quickly become more complex than movement alone ([Mojang Block Breaking Overview](https://github.com/Mojang/bedrock-protocol-docs/blob/main/additional_docs/BlockBreakingOverview.md)).
- Bedrock exposes a strict authoritative-movement mode but documents that stricter tracking impacts movement at high latency ([Microsoft Bedrock server properties](https://learn.microsoft.com/en-us/minecraft/creator/documents/bedrockserver/server-properties?view=minecraft-bedrock-stable)). This is direct evidence against treating tolerance, fairness, and cheat resistance as one monotonic “strictness” slider.
- GrimAC, a third-party Java Minecraft anti-cheat, says it replicates possible movement and keeps a per-player view of world changes queued until they should have reached that player; it specifically cites blocks broken under a player as a latency-compensation problem ([GrimAC source/README](https://github.com/GrimAnticheat/Grim)). This is implementation evidence from a community project, not a guarantee that its marketing claims are correct. The architectural lesson is the cost of reconstructing each client's collision knowledge after the protocol was designed around position reports.

### Clones and mature engines

- Luanti's local client applies movement and voxel collision in `LocalPlayer::move` ([Luanti client source](https://github.com/luanti-org/luanti/blob/master/src/client/localplayer.cpp)), while its protocol sends position, speed, look, keys, and movement values to the server ([Luanti protocol source](https://github.com/luanti-org/luanti/blob/master/src/network/networkprotocol.h)).
- Luanti's server movement check comments that the server should handle player physics as the client does and compare against that result ([Luanti server source](https://github.com/luanti-org/luanti/blob/master/src/server/player_sao.cpp)). That maintainer note is direct evidence that speed-budget checking is a compromise, not equivalent to authoritative movement.
- Quake III shares its `pmove` code between client prediction and true server movement ([id Software `bg_public.h`](https://github.com/id-Software/Quake-III-Arena/blob/master/code/game/bg_public.h)). It is a primary source for the shared movement-kernel pattern, although its static BSP collision is much easier than mutable voxel collision.
- Unreal's mature controller stores moves, combines compatible moves to reduce bandwidth, reproduces them on the server, sends an ack or correction, restores corrected state, and replays the remaining saved moves. Remote simulated proxies use network smoothing instead ([Epic Character Movement networking](https://dev.epicgames.com/documentation/unreal-engine/understanding-networked-movement-in-the-character-movement-component-for-unreal-engine)).
- Valve's client/server networking paper describes the same core loop: run shared movement immediately, retain commands, receive the last acknowledged command and authoritative state, then replay pending commands. Valve also moved movement and weapon logic into shared code to reduce prediction divergence ([Valve latency-compensation design](https://developer.valvesoftware.com/wiki/Latency_Compensating_Methods_in_Client/Server_In-game_Protocol_Design_and_Optimization)). This is a vendor-hosted historical paper; released Half-Life movement code is available in [`pm_shared`](https://github.com/ValveSoftware/halflife/blob/master/pm_shared/pm_shared.c).
- Source's remote-entity interpolation deliberately renders behind the newest state so loss and jitter still leave two snapshots to blend; extrapolation is bounded because prediction error grows ([Valve Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking)).

### Inferences for VibeCraft

1. Sharing movement code is necessary but insufficient. VibeCraft must also share the collision *version* used by that code.
2. A server that receives only bounded input axes/buttons can enforce legal movement by construction. Separate heuristic “speed checks” become anomaly telemetry rather than the primary authority mechanism.
3. Correction should always update logical state immediately, then smooth only a presentation offset. Smoothing the collision body itself can leave it visually or logically inside blocks.
4. Honest divergence must not trigger an automatic ban. It can indicate loss, an unreceived world edit, a controller bug, a platform numeric difference, overload, or malicious input; those cases need different evidence.

## Proposed design

### Shared controller contract

Implement a custom kinematic controller in a Godot-free C# assembly. It owns:

- swept player AABB/capsule collision against block collision shapes;
- horizontal acceleration, friction, gravity, jumping, step-up, crouch/edge behavior, swimming, climbing, knockback, and grounded/fall-distance state;
- explicit movement modes and mode transitions;
- stable collision ordering and a maximum iteration/contact count;
- finite-value checks and world-coordinate safety limits.

Godot receives the resulting logical transform and renders it. It may use Godot queries for camera or cosmetic effects, but those results do not feed authoritative movement.

```csharp
public readonly record struct MovementStep(
    uint InputSequence,
    uint ClientPredictionStep,
    InputButtons Held,
    InputEdges Pressed,
    short MoveX,
    short MoveZ,
    ushort Yaw,
    ushort Pitch);

public readonly record struct ReconciliationState(
    ulong WorldTick,
    uint LastProcessedInput,
    PlayerControllerState Controller,
    ulong CollisionStreamEpoch,
    long ContiguousCollisionRevision,
    CorrectionKind Kind);

public interface ICollisionChangeJournal
{
    bool TryGetView(ulong effectiveWorldTick, long requiredRevision,
        out ICollisionView view);
}
```

Inputs are quantized before *both* local and server simulation so the client never predicts with higher-precision input than it transmits. The controller uses a fixed delta and explicit movement rules negotiated at join. It must not consume wall-clock `delta` supplied by either peer.

### Input generation and transport

At each 20 Hz predicted `WorldTick`:

1. Sample held controls and input edges accumulated since the previous step.
2. Quantize movement axes and look direction.
3. Assign wrapping `uint32` `InputSequence` and `ClientPredictionStep` values.
4. Run the local controller immediately using the currently selected authoritative collision view plus tracked provisional overlays.
5. Store input, before/after state, collision revision, and predicted side-effect identifiers in a fixed-size history ring.
6. Send an unreliable no-delay `InputBundle` containing the newest frame and the previous two frames. Sequence numbers make duplicates idempotent.

Three transmissions per frame are redundancy, not three simulations. At independent packet loss `p`, the probability that all three copies are lost is `p^3` (for example, 0.0125% at 5% loss); real loss can be bursty, so this is a model, not a guarantee. Never retransmit old movement through a reliable ordered queue: stale movement arriving behind a lost packet causes head-of-line latency.

Latency-sensitive world intents such as place, break completion, attack, and use carry an idempotent `ActionId`, reference an included input sequence, and are repeated in bounded unreliable input bundles until an authoritative result acknowledges them. The server deduplicates them and never relies on cross-lane arrival order. Inventory, crafting, chat, administration, and other non-movement-sensitive transactions use reliable control messages. Transport semantics are defined in `NET-03`.

### Server input admission

For each player, maintain a fixed receive window and no unbounded collections:

- Drop already processed or duplicate sequences using wrap-safe comparison.
- Accept at most 20 queued future frames (one second at 20 Hz); reject frames beyond a small measured lead window (prototype start: four ticks).
- Parse only finite, in-range quantized fields; unknown button bits are invalid for the negotiated protocol.
- Process at most one movement frame per authoritative player step. Sending faster never grants more simulated time.
- If the next input is missing, wait only within the normal tick queue. For up to two consecutive simulation steps, reuse held axes/look from the last valid frame but clear all edge-triggered actions. On the third missing step, use neutral movement and clear all edges until fresh input arrives.
- Late frames for server steps already simulated are discarded rather than rewinding the authoritative world. The owning client converges through reconciliation.
- Rate-limit bytes, datagrams, decoded input frames, and gameplay commands separately. Disconnect persistent protocol abuse; do not allocate in proportion to claimed counts.

Server time determines acceleration, gravity, cooldown, exhaustion, and fall distance.
`ClientPredictionStep` is used only for connection-local ordering, diagnostics, clock
estimation, and later lag-compensation bounds; it never contributes elapsed
simulation time.

### Authoritative response

Include `ReconciliationState` for the owning player in normal snapshots at the chosen snapshot rate and immediately for teleports, movement-mode changes, severe divergence, or authoritative impulses. It contains:

- `WorldTick` and `LastProcessedInput`;
- logical position, velocity, grounded/fall state, stance and movement mode;
- movement-rules revision;
- the local collision-stream epoch and highest contiguous applied collision revision;
- correction kind: normal ack, impulse, mode change, teleport, respawn, or forced safety recovery.

Do not echo a client end position as an accepted result. An optional predicted-state checksum may be sent by debug builds to locate the first divergent step, but the server never uses it to update state.

### Collision history and block prediction

Every authoritative collision-affecting block delta carries an `EffectiveWorldTick`, cell/section coordinate, old and new collision-shape identity, and section revision. Inside the local player's measured collision safety neighborhood, retain these changes in a journal bounded by both duration and bytes. Start the experiment at 250 ms–1 s of history with a hard 4 MiB cap; measurements may reduce or revise those values.

On replay, the journal reconstructs only the cells that changed during the affected steps. If the required epoch/revision is unavailable, reset the **logical** controller immediately to authority, clear incompatible prediction, request the missing baseline, and retain only collision-safe visual smoothing. Do not render a stale predicted path for 500 ms while movement waits on bulk terrain.

For local block prediction:

- Start hand/tool animation, particles, sound, and crack progress immediately.
- Track a provisional overlay by `CommandId`; do not modify the authoritative chunk cache.
- V1 block prediction is visual only: a completed local break does not remove confirmed collision and placement does not add it. A later experiment may enable local-only provisional collision only if it stays within the same journal cap and materially improves a measured scenario.
- Acceptance promotes the authoritative block delta and removes the overlay. Rejection removes the overlay and invokes normal revision-aware reconciliation.
- Remote player/world rendering never treats another client's provisional overlay as real.

The exact fairness policy for a block broken under a moving player, and whether the server grants short support grace, belongs to `NET-04`; this document supplies the mechanism to implement and measure it.

### Reconciliation algorithm

When an owning-player state arrives:

1. Validate snapshot/baseline and collision-history dependencies.
2. Discard history through `LastProcessedInput` and cancel/commit predicted effects named by reliable transaction results.
3. Replace the logical controller with the authoritative state immediately.
4. Replay remaining input frames in sequence using the collision/rules version effective for each step.
5. Compare the new predicted logical transform with the transform displayed before reconciliation.
6. Store their difference as a presentation-only offset. Decay it over a tuned 80–150 ms window for ordinary small error.
7. Hard reset presentation for teleport/respawn, movement-mode discontinuity, missing history timeout, an error over 2 blocks, or any smoothing path that would put the camera/body through solid collision.

Small errors are not ignored logically. Always restore/replay so errors cannot accumulate; only the visual treatment uses a threshold. Camera rotation is never rewound from server yaw unless the server explicitly changes view/mode. Camera translation can use a shorter smoothing constant than the body mesh to reduce nausea.

### Remote entity rendering

- Snapshot each relevant remote entity with `ServerTick`, lifecycle generation, transform, velocity, movement mode, grounded/stance state, and discrete action event identifiers.
- Maintain a time-ordered buffer and render at `estimatedServerTime - interpolationDelay`.
- Set `interpolationDelay` adaptively to at least two snapshot intervals and enough to cover measured arrival jitter, clamped to 75–200 ms for V1. At 16 snapshots/s, two intervals are 125 ms.
- Interpolate position and orientation; derive locomotion animation from velocity/mode. Do not replay another player's controls.
- If the buffer underruns, extrapolate using authoritative velocity/mode for at most 100 ms, then hold/fade rather than continue through walls. Teleport, respawn, or generation change resets the buffer and snaps.
- Remote interpolation delay is included in any later hit-rewind calculation; it is not hidden from `NET-04`.

### Animation and anti-cheat

Locomotion animation state is output, not input:

```text
authoritative velocity + grounded + stance + movement mode
    -> idle/walk/run/fall/swim/climb animation parameters

validated action event
    -> swing/use/hurt/death/gesture trigger
```

A client may send desired sprint/crouch/jump/attack/use as input. It may not assert “running,” “grounded,” “falling,” or “attack completed.” Server-replicated animation does not need to match every cosmetic client transition exactly.

Anti-cheat layers:

1. **Protocol safety:** lengths, ranges, finite values, sequence windows, rate caps, authentication/replay protection.
2. **Authority by construction:** simulate bounded inputs once per server step against authoritative collision and movement rules.
3. **Invariant enforcement:** legal modes, server-issued impulses, permissions, inventory/equipment effects, world bounds.
4. **Anomaly telemetry:** repeated divergent predicted checksums, impossible request patterns, suspicious view/action timing, or persistent flooding.
5. **Enforcement policy:** reject illegal state immediately; kick clear protocol abuse; require corroborated, versioned evidence before temporary sanctions; never permanently ban only because corrections occurred.

This prevents common movement packets from granting an illegal position. It does not detect aim assistance, wall information from an overbroad interest set, malicious client cosmetics, or every bot.

## Greenlight criteria

- Client and server execute the same movement test corpus and agree within the defined numeric tolerance for at least 100,000 recorded steps on each supported desktop platform.
- No input packet contains an authoritative position/velocity/grounded/animation result, and a malicious client cannot create motion beyond the server's controller rules.
- At 150 ms RTT, 30 ms jitter, 5% loss, 1% duplication, and 2% reordering, ordinary traversal produces no hard snap; p99 presentation correction remains under 0.25 block after tuning.
- At 250 ms RTT and 10% burst loss, the controller converges after input stops, bounded buffers do not overflow silently, and missing block history uses the explicit recovery path.
- Break/place, knockback, water entry/exit, ladder transition, step-up, edge crouch, respawn, and teleport each have deterministic correction tests.
- A block removed under the player cannot produce a permanent client/server collision disagreement or an anti-cheat sanction solely because the client learned of the edit late.
- Remote entities remain visually continuous with one lost snapshot and never extrapolate longer than 100 ms.
- Correction, missing-input, duplicate-input, discarded-future-input, collision-history miss, hard-snap reason, queue depth, RTT, jitter, and loss estimates are observable per connection.

## Prototype or benchmark

Required: yes

Smallest useful experiment: Implement only the shared 20 Hz controller, authoritative input queue, input/action bundling, owner-state ack, bounded cell-change journal, immediate history-miss reset, presentation offset, and remote snapshot buffer. Use a tiny fixed world with solid blocks, slabs/stairs if supported, water, ladder, and one editable support block. Drive one real Godot client, one observer, and scripted bots through the `NET-03` impairment harness.

### Required scenarios

| Category | Cases |
| --- | --- |
| Controller | Start/stop, diagonal normalization, sprint, jump, low ceiling, step-up, edge/crouch, fall damage state, water, ladder, knockback |
| Mutable collision | Break support before/on/after movement tick; place into path; reject local break; delay section delta; unload/reload section |
| Network | 0–250 ms RTT, asymmetric delay, 0–50 ms jitter, 0–10% random and burst loss, duplication, reordering, 100/500 ms server hitch |
| Abuse | NaN/infinity if a debug codec permits them, illegal bits, huge sequence leap, duplicate edge action, 10× input rate, timer acceleration, stale replay, invented state |
| Lifecycle | Teleport, respawn, dimension/world transfer, reconnect, movement-rules revision change |

### Success metrics

- Camera/look feedback occurs in the next render frame; translated controller response occurs no later than the next 20 Hz local controller step.
- For the 150 ms/5% profile, p95 logical reconciliation error is under 0.05 block and p99 presentation correction under 0.25 block after tuning; hard snaps are zero outside explicit discontinuities.
- For the 250 ms/10% profile, position is within 0.01 block of authority within 1.5 seconds after controls return to neutral.
- Three-copy input redundancy reduces missing simulated input steps relative to one-copy input in the expected direction under both random and burst loss; actual counts, not the `p^3` model, decide.
- A reliable 10 MiB chunk/background transfer does not delay p99 input arrival or owner-state processing by more than 10 ms beyond the impairment baseline; this cross-checks `NET-03` lane isolation.
- Movement processing reports p50/p95/p99 cost for the declared acceptance load and a separate stress load on recorded reference hardware; an owner-approved headroom budget is required before production greenlight.
- Input and history queues stay at their configured caps for a 30-minute soak; no memory growth correlates with test duration.
- Every forced correction has one machine-readable reason and the first divergent input/world revision can be reproduced from a trace.

## Risks and open questions

- The journal duration and 4 MiB prototype cap are hypotheses. Report retained bytes and forced resets under edit storms; do not expand memory automatically to preserve replay.
- Client and server C# floating-point results may still diverge across hardware. Quantized inputs, stable collision ordering, explicit tolerances, and continuous authoritative replay limit impact; fixed-point movement should be considered only if measurements show persistent platform drift.
- A 20 Hz controller may feel coarse despite prediction. `NET-06` must first run a blind 20 Hz feel/correction test; only failure unlocks one 40 Hz nested-substep branch. Godot's default physics rate does not define the shared controller.
- Controller rules are gameplay. Changing friction, step order, hitboxes, or collision ordering requires a movement-rules version and coordinated protocol release.
- Predicting collision removal can feel better but increases correction complexity. The overlay path can be disabled initially while keeping immediate visual block feedback.
- Moving platforms, vehicles, pistons, portals, and cross-dimension transfer need explicit reference-frame/history rules and are deferred from the first controller prototype.
- Target player count and platform matrix are still unspecified.

## Dependencies

- Requires: `NET-01` authority model; `WORLD-01` stable section coordinates/revisions; `GAME-01` movement rules and block collision registry; `NET-03` delivery classes and impairment harness.
- Blocks: `NET-04` block/combat lag compensation; `NET-05` snapshot interest; `NET-06` final controller/input/snapshot rates; animation protocol; movement-related plugin APIs.

## Rejected or deferred alternatives

- Client-authoritative position plus threshold checks: rejected because tolerance becomes an exploit budget and stale collision becomes a false-positive source.
- Reliable ordered movement frames: rejected because late movement is superseded and head-of-line retransmission adds latency.
- Trusting client timestamps for elapsed movement: rejected because a modified clock becomes a speed control.
- Correcting only when error exceeds a threshold: rejected logically because sub-threshold error accumulates; thresholds apply only to visual smoothing and telemetry.
- Replaying against current collision: rejected because a changed support/path can produce a different legal result than the historical step.
- Automatic bans from movement divergence: rejected because network loss, world revision gaps, bugs, overload, and platform differences can all cause divergence.
- Godot physics as the client controller: rejected unless the dedicated server also runs the identical engine/version/platform and determinism is proven; that conflicts with the current standalone-server direction.
- Full fixed-point controller from day one: deferred until floating-point cross-platform traces demonstrate a real problem.
- Whole-world rollback for late input: rejected under `NET-01`; targeted player/combat history remains possible.

## Source-quality notes

- Mojang, Microsoft, Epic, Godot, id Software, Valve source, and Luanti source are primary/vendor evidence.
- Java protocol archives and GrimAC are labeled community evidence.
- Buffer sizes, 20 Hz behavior, journal duration/bytes, smoothing windows, interpolation bounds, and numeric success thresholds are proposed VibeCraft contracts. They are intentionally falsifiable and must be tuned or rejected by the prototype rather than copied from another game.
