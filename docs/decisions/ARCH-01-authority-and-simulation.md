# ARCH-01 Authority and simulation boundary

Status: Proposed

Owner: Architecture research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Use a server-authoritative, fixed-tick simulation that accepts sequenced **input and action intent**, never client-authored outcomes. Predict only the local player's movement and reversible presentation; reconcile it from authoritative snapshots. Interpolate remote entities.

One-sentence rationale: This is the smallest model that makes invalid world, inventory, combat, and movement outcomes non-authoritative while keeping normal play responsive at Internet latency.

### Owner decision — 2026-08-10

The owner accepted option **D**. The concrete anti-cheat objective is that a client
cannot produce impossible movement/state outcomes such as speed, timer, fly, noclip,
teleport, or inventory/world mutations by lying in packets. This is deliberately not a
claim to reliably detect aim assistance, macros, bots, or every modified client. See
[`OWNER_DECISIONS.md`](../OWNER_DECISIONS.md#accepted-decisions).

This decision deliberately replaces two overstatements in the current spec:

- “Make movement cheats almost impossible just through good netcode” is not an achievable acceptance criterion. Authority prevents impossible state writes; it cannot reliably distinguish a skilled human from aim assistance, macros, pathfinding, information extracted from already-replicated chunks, or every timing exploit.
- “Lighting will be calculated fully client-side” is safe only for rendered light. If light affects spawning, crops, visibility, redstone-like sensors, or any other rule, the server needs its own low-resolution gameplay-light value. The client's realistic lighting remains cosmetic.

## Context and constraints

- The Godot/C# client is untrusted, including in the presence of native client mods.
- The standalone C# server must remain authoritative in dedicated and local singleplayer sessions.
- Immediate local movement response matters more than exact visual agreement on every frame.
- Blocks are collision geometry and mutable shared state; movement prediction and world-update ordering cannot be designed independently.
- Inventories, item durability, crafting, combat, entity AI, random outcomes, scheduled block ticks, and redstone are economically or competitively relevant.
- Rendering may run at an arbitrary frame rate. `WORLD-08` owns one fixed 60 TPS
  authoritative `WorldTick` for v1; `NET-06` studies input/snapshot transmission
  cadence without creating another simulation grid.
- The initial game should not depend on whole-world rollback or deterministic lockstep. Both would multiply the cost of voxel changes, mods, physics, and debugging.
- Prediction code must be headless, engine-independent C#. Godot physics objects are presentation adapters, not the canonical movement implementation.

## Options considered

| Option | Core mechanism | Strengths | Costs and failure modes | Fit for VibeCraft |
| --- | --- | --- | --- | --- |
| A. Client-authoritative state | Client sends positions and completed actions; server relays or applies threshold checks | Lowest latency and simplest prototype | Teleport, speed, inventory, reach, and race exploits become validation whack-a-mole; conflicting clients need resolution | Rejected |
| B. Deterministic peer lockstep | Every participant simulates the same ordered inputs | Very low state bandwidth; exact shared simulation when it works | A slow peer stalls everyone; deterministic C#/Godot physics, mods, chunk loading, and unbounded voxel state are a poor fit; no inherent secrecy | Rejected |
| C. Hybrid position authority | Client sends its resulting position; server validates bounds and corrects egregious differences | Easier than replayable input simulation and similar to modern Java movement packets | Validation tolerances trade false positives for bypasses; server cannot reproduce the exact path from intent alone | Deferred as a temporary prototype baseline, not the shipping contract |
| **D. Server-authoritative input simulation with narrow client prediction** | Client sends ticked controls/actions; server runs canonical rules; owner predicts and replays unacknowledged inputs | Secure state boundary, responsive movement, one source of gameplay truth, clear corrections | Shared movement code, state history, ordering, and reconciliation are real engineering work | **Recommended** |
| E. Broad rollback simulation | Predict players, entities, blocks, combat, inventories, and effects; roll all of it back on corrections | Can hide latency for many actions | High CPU/memory cost and explosive side-effect complexity; plugin and voxel mutations are difficult to undo | Defer unless a later mechanic proves it necessary |

## Evidence

### Minecraft

**Minecraft: Java Edition 1.21.8 — reverse-engineered mappings, not Mojang protocol documentation.** The mapped Java packet shape sends coordinates and collision flags in `PlayerMoveC2SPacket`, while server corrections use an explicit position packet. This is evidence of a hybrid position-report/correction model, not a pure input-command simulation ([Fabric Yarn `PlayerMoveC2SPacket`](https://maven.fabricmc.net/docs/yarn-1.21.8%2Bbuild.1/net/minecraft/network/packet/c2s/play/PlayerMoveC2SPacket.html), [`PlayerPositionLookS2CPacket`](https://maven.fabricmc.net/docs/yarn-1.21%2Bbuild.2/net/minecraft/network/packet/s2c/play/PlayerPositionLookS2CPacket.html)). Paper's issue history contains legitimate vehicles, teleports, latency, and overload triggering “moved wrongly/too quickly” behavior, illustrating the false-positive side of threshold validation ([Paper #289](https://github.com/PaperMC/Paper/issues/289), [Paper #4917](https://github.com/PaperMC/Paper/issues/4917)).

**Minecraft: Bedrock Edition, current published protocol documentation — primary source.** Mojang documents a newer server-authoritative block-breaking path in which client and server run corresponding `GameMode` logic, the client predicts destruction and durability changes, and the server accepts or corrects those predictions. Movement and block actions share `PlayerAuthInputPacket` specifically to preserve ordering; the server queues those packets through a tick policy to prevent faster movement from a higher packet rate. The same document also admits incomplete authority: the selected target block remains client-authored with a distance check, and creative mode follows a client-authoritative path ([Mojang `BlockBreakingOverview.md`](https://github.com/Mojang/bedrock-protocol-docs/blob/main/additional_docs/BlockBreakingOverview.md)).

The useful lesson is not “copy Bedrock.” It is:

- send causally related movement and interaction intent in one ordered input timeline;
- predict reversible local feedback and explicitly accept/reject it;
- do not call a feature server-authoritative while still trusting a client-authored target or result.

### Clones, engines, and networking libraries

**Luanti (master inspected 2026-08-09).** Its client API exposes `node_placement_prediction`, predicted digging/placement callbacks, and server-controlled restrictions on client-side mods ([Luanti client API](https://github.com/luanti-org/luanti/blob/master/doc/client_lua_api.md)). This is a pragmatic separation between responsive block feedback and server-side game/mod state. It is not evidence that Luanti's entire movement model should be copied.

**Veloren 0.18/current development.** Veloren exposes a moderator command to force server-authoritative physics for an account, and its current changelog records that some uses of client-authoritative physics now trigger that mode and that those force records later became persistent ([generated command reference](https://book.veloren.net/players/commands.html), [source changelog](https://gitlab.com/veloren/veloren/-/blob/master/CHANGELOG.md), [command implementation](https://gitlab.com/veloren/veloren/-/blob/master/server/src/cmd.rs)). **Inference:** authority retrofits become account policy, migration, and operational debt. VibeCraft should make server-simulated movement the protocol contract from v1 rather than add it as an anti-abuse mode later.

**Unity Netcode for Entities 1.0/1.5 — engine implementation.** Unity documents the recommended loop directly: the owner predicts from input, the server runs the authoritative simulation, snapshots reset client state, and the client re-simulates to its present. It also warns that every snapshot can trigger replay; at 300 ms its 1.0 example expects roughly 22 re-simulated frames ([prediction overview 1.5](https://docs.unity.cn/Packages/com.unity.netcode%401.5/manual/intro-to-prediction.html), [prediction cost 1.0](https://docs.unity.cn/Packages/com.unity.netcode%401.0/manual/prediction.html), [command stream](https://docs.unity.cn/Packages/com.unity.netcode%401.0/manual/command-stream.html)). This supports narrow prediction rather than “predict everything.”

**Lightyear for Bevy — open-source networking library.** Lightyear combines a server-authoritative model with owner prediction/rollback, snapshot interpolation for other entities, and transport-independent replication ([project source and feature description](https://github.com/cBournhonesque/lightyear)). Its explicit determinism requirement is a warning: shared prediction systems need controlled time, inputs, and state access even when authoritative correction makes bit-perfect lockstep unnecessary.

**Valve Source networking — engine documentation and source.** Source predicts the local player from user commands, interpolates remote entities in the past, and says other players cannot usefully be predicted because their future input is unknown ([Valve networking documentation](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking), [Source SDK 2013](https://github.com/ValveSoftware/source-sdk-2013)). This division remains appropriate for VibeCraft.

### Sourced conclusions versus inference

Directly sourced:

- Bedrock packs movement and block actions together for ordering and validates/corrects predicted block/inventory changes.
- Java 1.21.8 movement packets contain client positions, and server position corrections exist.
- Narrow prediction entails replay cost proportional to latency and snapshot cadence.
- Luanti and Veloren both expose client prediction/authority decisions rather than eliminating the distinction.

Engineering inference adopted here:

- Sending intent instead of resulting position gives VibeCraft a cleaner anti-cheat and replay contract than Java's position packets.
- Predicted visual block edits must be separated from collision state; otherwise a speculative missing/placed block contaminates movement replay.
- Server authority reduces the effect of cheats but cannot make a hostile client honest. Replication scope, rate limits, moderation signals, and server-side semantic checks remain required.

## Proposed design

### Ownership matrix

| State or behavior | Canonical owner | Client behavior before confirmation | Correction/replication rule |
| --- | --- | --- | --- |
| Local player transform, velocity, grounded/swimming state | Server | Predict with shared movement code | Reset to snapshot, replay unacknowledged inputs, smooth visual-only error |
| Remote players and ordinary entities | Server | Interpolate buffered snapshots; short bounded extrapolation only as a visual fallback | Snap hidden simulation proxy; smooth render proxy |
| Block identity, metadata, block entities, fluids, redstone | Server | Optional ghost overlay, crack animation, sound, particles | Apply only revisioned server deltas; reject stale action results |
| Collision voxels | Server replica on client | By default, predict against revision-addressable confirmed history; `NET-02` may permit a local-only speculative overlay if it is isolated, bounded, and fully replayable | Never mutate the authoritative chunk cache or expose provisional collision to remote entities/gameplay |
| Inventory, crafting, smelting, durability, drops | Server | UI may show `pending`; do not spend predicted outputs | Transaction response accepts/rejects by action id and inventory revision |
| Health, hunger, status, death, damage, knockback | Server | Camera/audio/VFX may anticipate where reversible | Authoritative values and causal event ids replace predictions |
| Combat target and hit result | Server | Client sends origin/input tick/aim intent and may play swing animation | Server validates against permitted history; `NET-04` defines rewind policy |
| AI, projectiles, weather, world time, RNG | Server | Render/interpolate; cosmetic particles may use client RNG | Replicate state/events; gameplay RNG is seeded and consumed server-side |
| Animation state | Derived; server owns gameplay-relevant pose flags | Client derives locomotion immediately | Never trust a client-authored animation as proof of speed, crouch, attack, etc. |
| Camera, HUD, accessibility, high-quality light, fog | Client | Immediate and local | No authority unless a rule explicitly consumes a reduced server value |
| Client mods | Client capability sandbox | Produce visuals or ordinary action intents | No direct authoritative component/world mutation API |
| Server plugins | Server | N/A | Mutate only through tick-owned command/event APIs; `ARCH-05` sets budgets |

### Simulation boundary

1. The server owns a single mutable world and advances it on a fixed simulation tick.
2. Network threads decode and validate packet shape, then enqueue immutable commands. They do not mutate the world.
3. Worker jobs may calculate chunk generation, paths, or lighting, but return immutable results. The simulation thread commits results at explicit tick boundaries.
4. Each connection has an input sequence, an action sequence, a last accepted client tick window, and rate/budget counters.
5. The server consumes at most one movement command per player per simulation tick. Duplicate sequences are idempotently ignored. Missing continuous input follows a bounded policy chosen in `NET-02`; one-shot actions are never invented or repeated.
6. Snapshots name the authoritative server tick and the latest processed input/action sequences. The client can therefore discard acknowledged history and replay exactly the remainder.

### Protocol-level contract

The following is a semantic sketch, not a final Protobuf allocation. `NET-07` owns field numbering and evolution.

```protobuf
message InputFrame {
  uint64 session_id = 1;
  uint32 client_prediction_step = 2; // wrapping connection-local ordering domain
  uint32 input_sequence = 3;
  uint64 last_snapshot_world_tick = 4;
  PlayerControls controls = 5;       // axes and held/edge buttons, not a transform
  repeated ActionIntent actions = 6; // break/place/use/attack with unique action_id
}

message AuthoritativeSnapshot {
  uint64 world_tick = 1;
  uint32 last_processed_input_sequence = 2;
  uint64 last_processed_action_id = 3;
  PlayerState local_player = 4;
  repeated EntityDelta entities = 5;
  repeated ChunkRevision world_revisions = 6;
  repeated ActionResult action_results = 7;
}
```

Rules:

- A transform, velocity, `on_ground`, animation name, tool result, or damage result supplied by a client is telemetry at most; it is never applied as truth.
- An action intent contains what the player attempted and the input timeline on which it occurred. The server derives reach, line of sight, cooldown, tool effectiveness, drops, durability, and resulting mutations.
- Movement and actions that depend on movement share one ordered `InputFrame` stream, following the useful part of Bedrock's design.
- Latency-sensitive world intents are repeated in bounded unreliable input bundles until a reliable authoritative result acknowledges them. Action IDs are idempotency keys; transport ordering alone is never the gameplay ordering or idempotency contract. Inventory, chat, administration, and other non-movement-sensitive transactions use reliable control messages.
- Snapshot deltas are based on explicit baseline/revision ids. Missing a baseline requests or triggers a full relevant-state refresh; it never applies a delta to guessed state.
- Input packets may redundantly include recent frames under the policy selected by `NET-03`; sequence semantics do not change.

### Client prediction and reconciliation

- Put movement/collision math in a Godot-independent C# library used by both server and client. It receives a fixed-step input, an immutable collision query, and prior movement state; it returns new movement state and emitted intents.
- Do not use rigid-body nondeterminism or Godot `CharacterBody3D` as canonical simulation. A Godot node mirrors the predicted render transform.
- Keep at least the unacknowledged input window plus a safety margin sized by `NET-02` (initial prototype: two seconds, hard-capped).
- On an authoritative snapshot, restore the acknowledged player state and confirmed voxel revision, then replay later inputs. Side effects use causal ids so replay does not duplicate sounds, particles, actions, or UI notifications.
- Simulation state is corrected immediately. The visible camera/body may smooth small positional error over a short bounded interval; it must snap when smoothing would cross solid geometry, hide a teleport, or exceed the threshold selected by `NET-02`.
- Block breaking/placement may show a translucent placement ghost, cracks, hand motion, sound, and particles immediately without modifying the authoritative chunk cache. The safe baseline keeps collision confirmed until the server delta arrives. `NET-02` may greenlight provisional collision for the local predicted controller only when old section revisions remain available for replay, rejection is explicitly tested, and the overlay cannot affect remote entities or any server decision. Unknown remote edits are never guessed.
- Remote entities render from an interpolation buffer. Do not replay local input against guessed remote-player movement.

### Validation and abuse resistance

- Validate packet length, enum ranges, finite numbers, sequence windows, per-action cost, target existence, dimension/world revision, reach, line of sight, cooldown, inventory preconditions, permissions, and per-connection budgets.
- Run movement from accepted controls and server state. Rate-limit input production so packet frequency cannot increase simulated time.
- Keep diagnostic counters for dropped stale/future inputs, replay depth, correction magnitude, illegal intents, and rate-limit hits. Start with evidence/logging; automatic punishment policy is outside this decision.
- Send only state relevant to a client's interest set. Server authority cannot prevent x-ray against ore data already delivered to a modified client.
- Never require client-mod hashes as proof that the running process is clean. Hash compatibility is useful for version agreement, not attestation.

### Failure behavior

- Late input outside the accepted history window is dropped and acknowledged as late; the server never rewinds the whole world for movement.
- A missing input produces the bounded missing-input behavior from `NET-02`, never extra elapsed simulation.
- A client prediction buffer overflow stops prediction and visibly waits for an authoritative snapshot rather than fabricating state.
- A world revision mismatch invalidates affected prediction history and requests a relevant full baseline.
- NaN/infinite/out-of-range values disconnect the sender after protocol-level logging; malformed input cannot reach simulation math.
- Repeated corrections are a product bug or network/cheat signal, not something to hide with ever-longer smoothing.

## Prototype or benchmark

Required: yes

Smallest useful experiment: Build a headless shared C# character controller, an authoritative server harness, and one predicted client. Use a tiny mutable voxel test world and a scripted second actor that places/breaks blocks near and under the player. Do not build rendering, inventory UI, or the final transport first.

Test matrix:

- fixed 60 TPS authoritative `WorldTick`, render-rate client presentation, and
  separately measured packet cadence;
- 0, 50, 150, and 300 ms round-trip latency;
- 0–50 ms jitter, 0%, 2%, and 5% loss, plus 1% duplication/reordering;
- walking, sprinting, jumping, stair/edge contact, water/ladder equivalents, knockback, teleport, chunk-boundary collision, and block edits on the movement timeline;
- malicious speed/fly/noclip controls, NaN values, future/duplicate sequences, and packet-rate floods;
- ten-minute seeded runs, replayable from captured input and world-delta traces.

Success metrics:

- Zero server world/inventory mutations originate from client result fields; malformed numeric input never reaches simulation.
- Legal clients converge after each usable snapshot; no permanent state divergence or duplicated one-shot action occurs in any seeded run.
- At 150 ms RTT, 2% loss, and 20 ms jitter, 99% of ordinary movement corrections are under 0.25 block and none exceed 1 block except an explicit teleport or deliberately invalid input.
- The under-player block-edit scenario produces no false client fall before the edit's authoritative tick and no correction over 0.5 block after replay.
- Prediction replay costs at most 25% of a 16.7 ms client frame at p95 under 150 ms RTT, and at most one full frame at p99 under the 300 ms stress case on the declared reference machine.
- Packet floods cannot advance a player through more simulated ticks than elapsed server ticks.
- Remote render motion remains bounded and does not alter authoritative collision/combat state.

Failure means revising the movement state/input representation, world-revision coupling, or prediction scope before building gameplay on it. Raising correction tolerance until the test passes is not an acceptable fix.

## Greenlight criteria

- The prototype meets the convergence, correction, replay-cost, block-ordering, and packet-rate metrics above.
- `NET-02` fixes the input sampling, missing-input, history-window, reconciliation, and visual-smoothing policies.
- `NET-03` supplies channels that preserve the input/action semantics under loss, duplication, and reordering.
- `NET-04` defines server-side history and fairness rules for block edits, interaction, and combat.
- `NET-07` assigns evolvable Protobuf schemas and capability negotiation.
- Gameplay and rendering leads accept the ownership matrix, including separate visual and gameplay lighting.
- A test proves that dedicated and local-singleplayer sessions use the same simulation and gameplay packet handlers.

## Risks and open questions

- A custom headless character controller is a substantial subsystem; keeping it small is more important than matching every Godot physics feature.
- Mutable collision history can dominate prediction complexity. The confirmed-collision rule intentionally sacrifices a little visual/physical immediacy to contain it.
- Client/server floating-point or content-definition differences can cause frequent corrections. Golden movement traces and registry/content hashes are required.
- High tick rates multiply simulation, history, bandwidth, and replay cost. No evidence here supports 128 Hz as a default.
- Server-side rewind can improve fairness while enabling “shot behind cover” outcomes. That policy belongs in `NET-04`.
- Server authority does not solve x-ray, bots, aim assistance, denial of service, compromised server plugins, or malicious native mods.
- Gameplay light is now specified as server-owned discrete 0–15 data for gameplay
  rules. Its propagation must be scheduled, coalesced, and budgeted rather than solved
  synchronously for every edit; `RENDER-04`, `WORLD-08`, and `GAME-02` own the exact
  queue/cadence fixtures.

## Dependencies

- Requires: `WORLD-01` collision/chunk addressing; `GAME-01` authoritative registry identity.
- Must be refined by: `NET-02`, `NET-03`, `NET-04`, `NET-05`, `NET-06`, `NET-07`, `ARCH-05`, `MOD-01`.
- Blocks: entity architecture details in `ARCH-02`, most multiplayer gameplay implementation, anti-abuse validation, and client-mod mutation APIs.

## Rejected or deferred alternatives

- **Client position as truth plus speed thresholds:** rejected as the shipping model. It inherits both bypass tolerance and false-positive debt.
- **Trust animation state to prove legal movement:** rejected. Gameplay pose is derived from accepted input and server state; animation is not evidence.
- **Predict all block/inventory/combat outcomes:** deferred. Add prediction per mechanic only when measured latency justifies reversible bookkeeping.
- **Whole-world rollback:** deferred indefinitely. Voxel edits, plugins, AI, redstone, persistence, and external side effects make the cost disproportionate to v1.
- **Bit-perfect deterministic lockstep/fixed-point everywhere:** rejected for v1. Authoritative snapshots permit bounded correction; require reproducible movement tests, not global lockstep.
- **Client-mod manifest equality as anti-cheat:** rejected. It proves declared file compatibility, not what code is actually running.

## Source quality note

Mojang's Bedrock protocol repository is primary. Minecraft Java implementation claims use Fabric's mapped API and Paper's open issue record because Mojang does not publish equivalent Java protocol/source documentation; those claims are explicitly limited to observable packet/API shape and reported behavior. Luanti, Veloren, Lightyear, and Source links point to their project documentation or repositories. Recommendations and cross-project conclusions are labeled as VibeCraft engineering inference.
