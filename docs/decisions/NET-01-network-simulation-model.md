# NET-01 Network simulation and authority model

Status: Proposed

Owner: Networking research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Use a server-authoritative hybrid: clients send sequenced input and interaction intent; the server alone commits gameplay state; the owning client predicts only its local controller and reversible presentation; other entities are rendered from buffered authoritative snapshots.

One-sentence rationale: This keeps a voxel world's durable state and anti-cheat boundary on one authority while hiding round-trip latency where it matters, without requiring deterministic lockstep or rollback of chunks, plugins, AI, inventories, and redstone.

This decision explicitly rejects three premises in the current spec:

- “Secure netcode” cannot make cheating almost impossible. It can prevent a client from directly creating illegal authoritative state; it cannot prevent aim assistance, information disclosure, collusion, compromised server plugins, or every automation strategy.
- A higher global tick rate is not inherently better. It shortens a fixed-step interval but scales CPU and usually network work while doing nothing to remove propagation delay.
- Lag compensation cannot guarantee that every player's private, stale view of a changing world becomes true. The contract is eventual authoritative convergence with bounded, explainable correction and explicit grace policies.

## Context and constraints

- The client is Godot with C# bindings; the dedicated server is a separate C# application. Singleplayer still uses the server authority path.
- Terrain collision, block edits, entities, inventories, damage, item drops, AI, redstone, plugins, and saves interact. Rewinding all of them for every late command would make the entire engine transactional.
- A client may be malicious. Client-provided position, velocity, grounded state, animation state, elapsed time, reach result, inventory result, or block result is evidence at most, never authority.
- Honest clients must remain playable under latency, jitter, loss, duplication, reordering, delayed chunks, and temporary server hitches.
- The client and server need a shared player-movement implementation. Godot documents that its physics is not deterministic, so the network controller must not depend on replaying a Godot `CharacterBody3D` or rigid-body simulation on the standalone server ([Godot physics introduction](https://docs.godotengine.org/en/stable/tutorials/physics/physics_introduction.html)).
- V1 is a survival sandbox, not a frame-tight fighting game. Whole-world rollback and 128 Hz simulation are disproportionate to the initial gameplay loop.
- Use `WORLD-08`'s one fixed 20 Hz authoritative `WorldTick` for v1, render independently, and permit slower scheduled subsystems. `NET-06` measures packet/snapshot cadence. Do not expose 32/64/128 Hz authoritative profiles in v1; only test a 40 Hz player substep if the 20 Hz predicted controller fails a blind feel/correction test.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| Client-authoritative state reports | Fast to build; low server physics cost; tolerant of mismatched client code | Position/fly/speed/world cheats become validation heuristics; contradictory clients; corrections are hard to reason about | Reject. Luanti and Java Minecraft show the long-term complexity of validating client-reported motion after the fact. |
| Server authority with no prediction | Simple truth model; easy replay and debugging | Every move and interaction waits roughly one RTT; unacceptable at ordinary Internet latency | Useful only as a diagnostic mode. |
| Deterministic input lockstep | Very low state bandwidth; exact shared simulation if determinism holds | Waits for late peers, exposes denial-by-lag, requires cross-platform determinism, and gives every peer more state; plugins and streamed world make desync recovery costly | Reject for the world simulation. |
| Peer/global rollback | Immediate inputs and fair shared timeline for small deterministic games | Requires frequent snapshots and replay of all relevant state; side effects, chunks, random generation, scripts, and many players explode cost | Reject globally; retain narrow history for later combat rewind only. |
| Server-authoritative hybrid with local prediction and snapshot interpolation | Durable truth on server; responsive owning client; remote motion tolerates jitter; scalable interest filtering | Requires a shared movement kernel, input history, reconciliation, world revisioning, and careful correction UX | **Recommended.** |

## Evidence

### Minecraft changed authority models rather than finding one universal answer

- The reverse-engineered Classic protocol documents a client-to-server “Position and Orientation” packet that is effectively a player teleport. This is evidence of early client-reported position, but it is a community reconstruction, not Mojang documentation ([wiki.vg Classic Protocol archive](https://c4k3.github.io/wiki.vg/Classic_Protocol.html)).
- The reverse-engineered Java 1.12.2 protocol still has serverbound absolute position/look packets and clientbound correction plus teleport confirmation. It also sends entity movement state on the regular game tick. Again, this is community protocol evidence, not a supported Mojang specification ([wiki.vg Java 1.12.2 protocol archive](https://c4k3.github.io/wiki.vg/Protocol.html)).
- Modern Bedrock's official protocol notes describe a different direction. `PlayerAuthInputPacket` contains movement input and ordered player actions, the server queues it for controlled processing, and the explicit reason is to prevent clients moving faster by sending input faster. In server-authoritative block breaking, the client predicts and the server accepts or corrects the result ([Mojang Block Breaking Overview](https://github.com/Mojang/bedrock-protocol-docs/blob/main/additional_docs/BlockBreakingOverview.md)).
- Mojang's dedicated-server documentation exposes stricter server-authoritative movement but warns that it affects movement at high latency. That is direct evidence that cheat strictness and correction quality are a policy tradeoff, not a free property ([Bedrock server properties](https://learn.microsoft.com/en-us/minecraft/creator/documents/bedrockserver/server-properties?view=minecraft-bedrock-stable)).
- Bedrock's documented gameplay clock is 20 ticks per second, demonstrating that a successful voxel sandbox does not require a 64/128 Hz whole-world loop ([Microsoft `tick.json` documentation](https://learn.microsoft.com/en-us/minecraft/creator/documents/tickjsonintroduction?view=minecraft-bedrock-stable)). This does **not** prove 20 Hz is optimal for VibeCraft; it only falsifies “high tick rate is automatically required.”

### Relevant engines and implementations

- Luanti performs local player collision and movement on the client, then sends position, speed, look, keys, and movement values in `TOSERVER_PLAYERPOS` ([Luanti protocol source](https://github.com/luanti-org/luanti/blob/master/src/network/networkprotocol.h)). Its server movement-cheat check contains a maintainer comment that the server should actually run player physics like the client and compare results ([Luanti `PlayerSAO::checkMovementCheat`](https://github.com/luanti-org/luanti/blob/master/src/server/player_sao.cpp)). This is unusually direct evidence of the architectural debt caused by client-reported motion.
- Quake III puts `pmove` definitions in code shared by client and server and states that the same function produces local prediction and true server movement ([id Software `bg_public.h`](https://github.com/id-Software/Quake-III-Arena/blob/master/code/game/bg_public.h)). The genre differs, but the shared-kernel boundary is directly applicable.
- Unreal's Character Movement Component records client moves, reproduces them on the authority, acknowledges or corrects the owning client, replays saved moves after correction, and smooths replicated movement for simulated proxies ([Epic networked movement documentation](https://dev.epicgames.com/documentation/unreal-engine/understanding-networked-movement-in-the-character-movement-component-for-unreal-engine)). This is the closest mature reference for the recommended local-player/authority/remote-proxy split.
- Source uses an authoritative server, client prediction for the owning player, and buffered interpolation for remote entities; its documentation also warns that raising tick/update rates consumes bandwidth and CPU and can make results worse when capacity is exceeded ([Valve Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking)). The page is vendor-hosted community documentation; the released [Source SDK 2013 code](https://github.com/ValveSoftware/source-sdk-2013) is the primary implementation reference.
- GGPO's rollback SDK is explicitly designed around saving, predicting, and rolling back compact game state to eliminate input delay ([GGPO repository](https://github.com/pond3r/ggpo)). That mechanism is excellent for small deterministic simulations; applying it to a streamed mutable voxel world would require defining and snapshotting a vastly larger rollback domain. The conclusion that it is a poor global fit for VibeCraft is an inference from its state-save/replay requirements.
- Glenn Fiedler's practitioner analysis notes that lockstep waits for all participants and that cross-platform floating-point determinism is hard; snapshot interpolation trades that requirement for state bandwidth ([Snapshot Interpolation](https://www.gafferongames.com/post/snapshot_interpolation/)). This is expert secondary analysis, not a VibeCraft benchmark.

### Findings derived from the evidence

The following are design inferences, not claims made by the sources:

1. VibeCraft should be stricter than Java Minecraft/Luanti about movement authority because it controls both protocol endpoints and can send input rather than retrofitting validation onto position reports.
2. VibeCraft should be narrower than GGPO rollback because world streaming, plugin side effects, AI, and block simulation are poor rollback participants.
3. Bedrock's ordering of movement and block actions in one input packet is worth copying conceptually: an interaction must reference the same command sequence and view used for movement, even if transport framing differs.
4. “Server authoritative” means state ownership, not delayed presentation. Camera motion, local movement, swing animation, particles, audio, and provisional block overlays can still run immediately.

## Proposed design

### Authority matrix

| State/action | Client may predict or propose | Server responsibility | Replication result |
| --- | --- | --- | --- |
| Camera and UI | Fully local | None, except mode/permission limits | Not replicated unless gameplay needs aim/view intent |
| Local player movement | Predict from local input and known collision revision | Simulate accepted input, collisions, impulses, movement mode, fall distance | Ack last input plus authoritative controller state |
| Remote players/entities | Interpolate; very short bounded extrapolation | Own simulation and visibility | Unreliable superseding snapshots plus reliable lifecycle events |
| Block breaking/placing | Immediate animation and provisional overlay; optional provisional collision under `NET-04` policy | Validate reach, timing, inventory, target revision, occupancy; commit block and item changes atomically | Reliable result/event; authoritative chunk delta and revision |
| Inventory/crafting/damage/drops | UI may display pending intent | Validate and commit transaction | Reliable transaction response and deltas |
| AI, fluids, redstone, weather, time | Presentation only | Full authority | Interest-filtered events/snapshots |
| Animation | Local cosmetic transitions; send action intent only | Derive locomotion from authoritative velocity/mode and validate gameplay actions | Snapshot state + discrete reliable/unreliable action events |
| Plugins/mods | Client mods can call only registered intent APIs | Server/plugin boundary validates through the same command path | No client extension can acquire authority by registration |

### Runtime topology

```text
input device
    -> InputFrame(sequence, buttons, axes, view)
    -> client shared movement kernel -> predicted local state -> render
    -> transport -> server input queue
    -> authoritative fixed tick + same movement kernel
    -> authoritative state/event log
    -> interest-filtered snapshots and reliable results
    -> client: reconcile local / interpolate remote / commit or reject overlays
```

Singleplayer starts the same server application and uses the same handshake, commands, validation, tick ordering, and result messages. A future in-memory transport is allowed only behind the same message-oriented `IConnection` interface and must pass the same protocol conformance suite; V1 should use loopback networking so multiplayer behavior is exercised continuously.

### Fixed authoritative tick and phase order

Use `WorldHz = 20` for the first playable prototype. The rate is a world-format/gameplay contract, not a player-facing tuning feature. Store persisted schedules as absolute `DueWorldTick` values with versioned conversion rules. Network transmission rates are independent of render rate, but neither packets nor presentation advance authoritative time.

For authoritative tick `T`, execute this order on one world owner thread:

1. Drain bounded inbound queues and map valid, new input sequences to players.
2. Apply server-scheduled impulses and authoritative events committed before `T`.
3. Freeze an immutable collision/world revision view `R(T)` for controller simulation.
4. Simulate every player once against `R(T)`, using the newest admissible input and server time.
5. Validate interactions against each player's post-movement state and the target revision. All movement in `T` therefore sees a block that another player removes in `T`; removal affects collision from `T+1`.
6. Run due entity/block subsystems under their own budgets; worker results may be published only if their input revisions still match.
7. Commit state atomically for the tick, advance entity/chunk revisions, and emit events.
8. Build per-client snapshots and enqueue transport messages without waiting for socket I/O.

The deterministic phase order is more important than running everything on one thread. Chunk generation, pathfinding, serialization, compression, and persistence can run on workers, but workers return immutable proposals; they do not mutate live simulation state.

### Core interfaces

The shared assembly must contain no Godot types and no socket code:

```csharp
public readonly record struct InputFrame(
    uint Sequence,
    uint ClientPredictionStep,
    InputButtons Buttons,
    short MoveX,
    short MoveZ,
    ushort Yaw,
    ushort Pitch,
    ulong LastReceivedWorldTick);

public readonly record struct PlayerControllerState(
    WorldPosition Position,
    Velocity3 Velocity,
    MovementMode Mode,
    bool Grounded,
    long CollisionRevision); // checked nonnegative SectionRevision

public interface IPlayerMovementKernel
{
    PlayerControllerState Step(
        in PlayerControllerState previous,
        in InputFrame input,
        ICollisionView collision,
        in MovementRules rules);
}

public interface IAuthoritativeCommand
{
    PlayerId Actor { get; }
    uint InputSequence { get; }
    CommandResult ValidateAndApply(WorldTransaction transaction);
}
```

`ICollisionView` exposes integer block/shape queries from a revisioned voxel snapshot. The controller should use explicit swept AABB/capsule rules suitable for blocks rather than Godot rigid-body results. Bit-for-bit cross-platform determinism is desirable but not a correctness requirement: authoritative snapshots still reconcile drift. Deterministic test fixtures are required for the supported .NET platforms.

### Snapshot and recovery policy

- Send reliable lifecycle and transaction events: join/leave, spawn/despawn, inventory results, block transaction results, mode changes, teleports, and chunk baselines.
- Send transform/controller snapshots as unreliable superseding state. Each snapshot carries `serverTick`, entity revision, baseline identifier, and `lastProcessedInputSequence` for the owning player.
- Keep a bounded authoritative history of player transforms and collision revision references for diagnostics and future targeted hit rewind. Do **not** store rollback snapshots of the entire world.
- If a client cannot apply a delta because its baseline/revision is missing, it requests a bounded resync; the server sends the relevant full entity/chunk baseline. Unknown baselines are never guessed.
- If the server misses an input, repeat held analog movement briefly but never repeat edge actions such as jump, attack, place, or inventory activation. The exact two-tick grace is defined in `NET-02`.
- If the simulation cannot keep real time, it must report overload and degrade snapshot/chunk/background rates before changing authoritative elapsed time. It may disconnect abusive peers. It must not silently process extra client commands to “catch up.”

### Security boundary

The server validates all numeric values for finite/range constraints before queueing, rejects duplicate/old/far-future sequences, caps messages and commands per tick, and derives elapsed time from server ticks. It simulates legal movement instead of comparing only distance traveled.

Animation is not trusted anti-cheat data. Locomotion animation is derived from authoritative velocity, grounded state, stance, equipment, and action state. The client may request a gesture or attack; the server validates the gameplay action and broadcasts an event. A mismatch is telemetry, not proof sufficient for an automatic ban.

## Greenlight criteria

- The server can run a local and a remote client through the same command/result path; no client packet can directly assign authoritative position, velocity, inventory, health, or block state.
- The shared movement kernel passes identical recorded-input fixtures in the standalone server and Godot C# client on every supported desktop OS.
- Under 150 ms RTT, 30 ms jitter, 5% random loss, 1% duplication, and 2% reordering, local input remains immediate and all clients converge after impairment without world corruption or an unbounded queue.
- A deliberately modified client that sends impossible positions, NaN/infinity, accelerated client ticks, duplicate commands, excessive command rate, or invented block results cannot move or mutate the world beyond legal server simulation.
- A missed chunk/entity delta triggers bounded baseline recovery; it never causes permanent desync.
- The 20 Hz prototype sustains the declared player/entity/view-distance workload with measured p99 headroom agreed after reference hardware and workload are fixed. Arbitrary bot counts or percentages are research fixtures, not product requirements.
- Product language is changed from “movement cheats almost impossible” and “DDoS safe” to measurable threat-model outcomes; volumetric DDoS mitigation remains `NET-08`.

## Prototype or benchmark

Required: yes

Smallest useful experiment: Build a headless C# world containing flat terrain, stairs, a low ceiling, water, one movable player, one observer, and block place/break. Run the same movement-kernel assembly in a Godot C# client and standalone server. Add a deterministic impairment proxy and a malicious scripted client. Record every input, world revision, authoritative tick, correction, and snapshot.

Test matrix:

| Scenario | Network profiles | Required observation |
| --- | --- | --- |
| Walk, sprint, jump, crouch/edge, stairs, head collision, water | 0/50/100/150/250 ms RTT; 0–50 ms jitter; 0/1/5/10% loss | Immediate local response; bounded corrections; final convergence |
| Break/place under and in front of a moving player | Same plus delayed chunk delta | Deterministic tick ordering; no permanent collision desync |
| 100 ms and 500 ms server hitches | 100 ms RTT, 2% loss | Bounded queues; no speed-up exploit; explicit overload telemetry |
| NaN, infinity, position injection, timer acceleration, flood, replay | Local and impaired | Rejected before state mutation; no crash; actionable logs |
| Disconnect/reconnect after missing baselines | 5% loss | Full resync restores exact authoritative state |

Success metrics:

- Predicted local response is visible by the next rendered frame.
- No hard snap occurs for ordinary movement at up to 150 ms RTT/5% loss; corrections above the prototype's soft threshold are counted and the p99 correction magnitude remains under 0.25 block after tuning.
- After input stops, position error falls below 0.01 block within 500 ms at 150 ms RTT/5% loss and within 1.5 seconds at 250 ms RTT/10% loss.
- No queue grows with test duration; all per-peer input/snapshot/history buffers have asserted limits.
- A 30-minute 64-bot run has no tick overrun cascade and no divergent authoritative hashes.

Failure of these metrics does not justify switching to client authority. It requires tuning the controller, reconciliation, rates, or scope, then rerunning the decision review.

## Risks and open questions

- Target player count, view distance, supported OSes, and minimum hardware are absent from the spec; production capacity cannot be greenlit without them.
- A custom voxel controller is substantial gameplay code, but using Godot physics only on the client would guarantee semantic mismatch with the standalone server.
- Server authority prevents illegal state from taking effect; it does not by itself detect every cheat or provide a fair ban policy.
- Client prediction across block revisions is the hardest correctness edge. `NET-02` defines history/replay mechanics; `NET-04` must define support grace and combat/block fairness.
- Plugins can still violate invariants if they receive mutable world access or block the tick. `ARCH-05` must constrain them to validated transactions and budgeted callbacks.
- Fixed 20 Hz is the coherent V1 baseline, not a claim of optimality. Godot's own default physics is unrelated to the standalone shared controller. `NET-06` must test 20 Hz prediction first and may add one exactly nested 40 Hz player-substep branch only if a measured movement problem remains.

## Dependencies

- Requires: `ARCH-01` authority boundaries to adopt this model; `WORLD-01` revisioned collision sections; `GAME-01` stable command and registry identifiers.
- Blocks: `NET-02` movement prediction/reconciliation; `NET-04` lag compensation; `NET-05` interest management; `NET-06` final rates; `NET-08` threat model; `ARCH-04` local server lifecycle.

## Rejected or deferred alternatives

- Client-reported authoritative position: rejected because it turns normal movement into post-hoc cheat inference and reproduces Luanti/Java Minecraft's validation burden.
- Server-only movement with no prediction: retained only as a debug mode because ordinary RTT becomes input latency.
- Global deterministic lockstep: rejected because slow peers stall progress and cross-platform deterministic world/plugin simulation is not credible.
- Whole-world rollback: rejected because state capture, side-effect reversal, memory, and replay cost grow with chunks, entities, block systems, and plugins.
- Client-authoritative creative mode: deferred; even creative should use explicit server-granted capabilities rather than a second authority model.
- Global 32/64/128 Hz selection: rejected for v1; higher-rate nested movement remains a future experiment, not a server profile.
- Peer-to-peer world authority: rejected for V1 because it conflicts with dedicated servers, simple anti-cheat boundaries, plugin ownership, and persistence.

## Source-quality notes

- Mojang's Bedrock protocol repository, Microsoft Minecraft documentation, engine source repositories, Epic documentation, and released id/Valve source are primary or vendor sources.
- Java Minecraft wire details are necessarily labeled community reverse engineering because Mojang does not publish a supported Java protocol specification.
- The suitability conclusions, VibeCraft phase order, 20 Hz baseline, correction thresholds, and rollback rejection are engineering inferences to be validated by the prototype, not facts asserted by the cited projects.
