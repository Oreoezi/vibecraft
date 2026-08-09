# NET-04 Block interaction and combat lag compensation

Status: Proposed

Owner: Networking architecture sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Keep the live simulation and terrain fully server-authoritative and greenlight only idempotent, current-time block/action reconciliation for v1. Treat combat rewind and remote support-loss grace as independent later experiments, both disabled by default and neither required by the movement, transport, or block-edit architecture.

One-sentence rationale: Current-time validation is the smallest secure baseline; historical combat and per-player phantom support add fairness, abuse, history, and gameplay-rule costs that must demonstrate a concrete benefit before becoming contracts.

This is **not** a recommendation for whole-world rollback. Historical state is queried to validate an action; accepted consequences are committed once to the current authoritative world.

## Context and constraints

- The server must remain authoritative enough that a modified client cannot assert positions, hits, block states, or action times.
- VibeCraft wants high-ping play to be tolerable and specifically wants to avoid a large downward correction when another player removes support that the local player had apparently already left.
- Blocks change collision immediately, so ordinary player-only rewind is insufficient: a target can become air, a wall can appear, or the floor can disappear while an action is in flight.
- Initial combat is Minecraft-like melee and projectile combat, not a hitscan shooter. Rewinding every dynamic body and voxel is disproportionate for v1.
- UDP loss, duplication, and reordering are expected. Every mutating action must therefore be idempotent and explicitly reconciled; transport reliability alone is not an application-level outcome acknowledgement.
- The server cannot promise that all effects of 300+ ms latency disappear. An unlimited forgiveness window conflicts with anti-cheat, defender fairness, and bounded memory/CPU. The current spec's wording should be treated as an experience target, not an absolute guarantee.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| A. Receive-time authority plus idempotent edit reconciliation | Small, easy to reason about, hard to exploit | High-ping melee misses; stale edits snap back; remote floor edits can still correct | **Recommended v1 baseline** |
| B. Trust client action results or positions | Immediate feedback and little server history | Teleport, reach, through-wall, duplicate-item, and forged-time cheats; clients can disagree | Rejected |
| C. Rewind the complete world and re-simulate | In theory reconstructs what the actor saw | Mutable voxel histories are large; ordering simultaneous edits is ambiguous; rewinding collision can place entities inside current blocks; expensive plugin determinism requirement | Overbuilt and unsafe for v1 |
| D. Selective historical validation plus edit reconciliation and support grace | Responsive local edits; potentially fairer melee; bounded state; live world has one truth | More protocol/state-machine work; support grace changes edge-case physics and creates collusion/trap semantics | Experiment only; split combat and support branches |

## Evidence

Labels used below: **Fact** is directly supported by the linked implementation/document; **Inference** is a conclusion drawn from those facts; **Recommendation** is VibeCraft policy.

### Minecraft

- **Fact (Java 1.21.5):** block-dig packets carry a position, face, action, and sequence, and block-use packets likewise carry a hit result and sequence. See the mapped [`PlayerActionC2SPacket`](https://maven.fabricmc.net/docs/yarn-1.21.5%2Bbuild.1/net/minecraft/network/packet/c2s/play/PlayerActionC2SPacket.html) and [`PlayerInteractBlockC2SPacket`](https://maven.fabricmc.net/docs/yarn-1.21.11%2Bbuild.1/net/minecraft/network/packet/c2s/play/PlayerInteractBlockC2SPacket.html) APIs.
- **Fact (modern Java protocol; community-maintained because Mojang publishes no protocol specification):** the server acknowledges a block-change sequence; after that acknowledgement the client replaces its prediction with server block state. The acknowledgement is processing progress, not proof that the edit succeeded. See [wiki.vg's Acknowledge Block Change description](https://wikivg.booky.dev/Protocol#Acknowledge_Block_Change) and the independently generated [PrismarineJS 1.20.5 protocol schema](https://prismarinejs.github.io/minecraft-data/protocol/pc/1.20.5/).
- **Fact:** the mapped modern server API exposes current-position block/entity interaction checks (`canInteractWithBlockAt`, `canInteractWithEntity`) and the interaction packet does not expose an action timestamp. See [`ServerPlayerEntity`](https://maven.fabricmc.net/docs/yarn-1.21.5%2Bbuild.1/net/minecraft/server/network/ServerPlayerEntity.html) and [`PlayerInteractEntityC2SPacket`](https://maven.fabricmc.net/docs/yarn-1.21.5%2Bbuild.1/net/minecraft/network/packet/c2s/play/PlayerInteractEntityC2SPacket.html).
- **Inference:** contemporary vanilla Java performs prediction/reconciliation for block interaction but does not expose a Source-style timestamped combat-rewind protocol. Minecraft is useful evidence for edit sequencing, not evidence that its high-ping combat behavior is a good target.
- **Version warning:** early Minecraft around 1.0 predates the modern sequence acknowledgement. Reproducing its gameplay scope does not require reproducing its older synchronization weaknesses.

### Luanti (formerly Minetest)

- **Fact:** Luanti's current server packet handler reads a client-reported position and velocity, applies the position, then runs a movement-cheat check and sends a correction if it fails. An interaction packet also embeds player-position data. See [`process_PlayerPos`](https://raw.githubusercontent.com/luanti-org/luanti/master/src/network/serverpackethandler.cpp#L410-L480) and [`handleCommand_Interact`](https://raw.githubusercontent.com/luanti-org/luanti/master/src/network/serverpackethandler.cpp#L825-L953).
- **Fact:** the interaction handler checks privilege and reach against a last-good/eye position, tracks dig start versus completion, and marks map blocks for resend when validation fails or placement prediction may differ. See the [reach/reversion path](https://raw.githubusercontent.com/luanti-org/luanti/master/src/network/serverpackethandler.cpp#L899-L979) and [placement-prediction repair](https://raw.githubusercontent.com/luanti-org/luanti/master/src/network/serverpackethandler.cpp#L1105-L1158).
- **Inference:** this is a pragmatic clone implementation, but accepting a result-like client position and detecting excessive movement afterward is weaker than VibeCraft's desired input-authoritative movement. The useful pattern is explicit repair of client prediction, not the authority model.

### Other engines and networking libraries

- **Fact (Valve Source):** Source estimates command execution time, keeps player history, rewinds targets for the query, and restores them afterward. Valve documents the unavoidable defender-side paradox (“hit behind cover”) and says only players are rewound by default. Its default history is one second. See [Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking#Lag_compensation) and [Lag Compensation](https://developer.valvesoftware.com/wiki/Lag_compensation).
- **Fact (Valve Source):** Source has a validity option that avoids restoring a rewound entity to a position it cannot occupy in the current world. That is direct evidence that rewinding bodies against non-rewound collision creates invalid-position edge cases. See [`sv_lagcompensationforcerestore`](https://developer.valvesoftware.com/wiki/Lag_compensation#Configuration).
- **Fact (Unity Netcode for Entities):** Unity provides a bounded physics-world history for server lag-compensation queries, while its prediction model keeps the server authoritative and has the client roll back/replay its own predicted state. See [`LagCompensationConfig`](https://docs.unity.cn/Packages/com.unity.netcode%401.0/api/Unity.NetCode.LagCompensationConfig.html) and [Introduction to prediction](https://docs.unity.cn/Packages/com.unity.netcode%401.5/manual/intro-to-prediction.html).
- **Fact (Lightyear):** Lightyear separates the owner's predicted entity from confirmed state and replays local inputs after a mismatch; non-owners interpolate. See its [advanced systems guide](https://cbournhonesque.github.io/lightyear/book/tutorial/advanced_systems.html).
- **Inference:** established systems rewind compact entity/physics history for a query; they do not establish that a large, mutable voxel world should be rolled back and committed from the past.

## Proposed design

### 1. Shared time and immutable action identity

The connection time-sync layer maps a wrapping `ClientPredictionStep` to an estimated
`WorldTick`. The server never accepts a raw client wall-clock value. It clamps
historical evaluation to:

```text
effective_world_tick = clamp(mapped_client_prediction_step,
                             current_world_tick - max_rewind_ticks,
                             current_world_tick)

default max_rewind = 150 ms
hard configurable range = 0..250 ms
```

The 150 ms default is deliberately much smaller than Source's one-second history because VibeCraft has short melee reach and player-created cover. A command older than the retained window is not disconnected merely for being late; it receives current-time validation with `compensation=none`.

Every action has a connection-scoped monotonically increasing `action_id`. The server retains recent results and returns the same result for a duplicate. Gaps are legal because packets may be abandoned, but replaying an old accepted ID cannot mutate state twice.

### 2. Protocol surface

Protobuf field numbers are intentionally omitted until NET-07 fixes schema/versioning rules, but the semantic shape is fixed:

```protobuf
message ActionRequest {
  uint64 action_id;
  uint32 client_prediction_step;
  uint32 latest_input_sequence;
  oneof action {
    BeginBreak begin_break;
    FinishBreak finish_break;
    PlaceBlock place_block;
    MeleeAttack melee_attack;
    UseEntity use_entity;
  }
}

message BlockTarget {
  sint64 x; sint64 y; sint64 z; // canonical BlockCoord
  uint32 expected_state_id;
  int64 expected_section_revision; // checked nonnegative SectionRevision
  Face face;
  Vec3 aim_origin;
  Vec3 aim_direction;
}

message ActionResult {
  uint64 action_id;
  ActionStatus status; // ACCEPTED, STALE, OUT_OF_REACH, BLOCKED, RATE_LIMITED, RETRYABLE_UNLOADED
  uint64 committed_world_tick;
  uint32 last_processed_input_sequence;
  repeated AuthoritativeCell affected_cells;
  optional uint64 inventory_revision;
  CompensationKind compensation_used;
}
```

`aim_origin` is evidence for diagnostics, not trusted authority. The server reconstructs the actor's eye pose from history. `expected_section_revision` makes the target conditional on the owning section snapshot: breaking stone at revision 41 cannot accidentally break the chest that replaced it at revision 42. Inventory revision is a distinct unsigned domain and is never compared with a section revision.

### 3. Block breaking, placement, and stale targets

The client may immediately render a predicted local block state and animation in a separate overlay. It must not overwrite the last confirmed world state. Collision prediction may consult that overlay only for the local predicted player and must be replayable.

For every block action the server:

1. processes already queued movement inputs for the tick before interactions;
2. reconstructs the actor eye pose at `effective_tick`;
3. requires the current cell state and revision to equal the request's expectation;
4. raycasts/reach-checks from the historical eye pose, but requires line of sight to be clear in both the historical view and the current world;
5. checks tool, inventory revision, permissions, cooldown, and edit rate in current authoritative state;
6. commits once at the current tick and returns authoritative affected cells and inventory revision.

The “clear both then and now” rule favors newly placed cover and prevents an action from passing through either a wall that existed when the player acted or a wall built before the server committed it. Recent voxel history is a ring of change records `(cell, old_state, new_state, revision, tick)`, not snapshots of chunks. A short ray reconstructs only cells changed during the rewind window.

`BeginBreak` records the server-validated target and a compensated start tick. `FinishBreak` must reference that begin action and satisfy tool-specific duration. This removes one network-delay penalty without allowing a client to claim an arbitrary old start. Placement and instant-use actions never commit into a past world.

If the target is stale, unloaded, protected, or out of reach, the server sends a result plus the minimum authoritative cells/inventory slots needed to remove the prediction. It must not synchronously load a chunk for an untrusted request.

### 4. Combat and entity interaction

This section specifies a **deferred experiment**, not v1 behavior. Do not allocate pose/voxel rewind history until the combat rules and target concurrency are defined.

Keep a ring buffer of authoritative poses for players and other explicitly lag-compensated entities: tick, position, orientation, stance, and hitbox. Do not include arbitrary mobs by default.

- Melee/reach queries sample the attacker eye pose and target hitbox at `effective_tick`.
- Current and historical voxel line of sight must both pass.
- Damage, knockback, cooldown, health, and death are applied only to current state.
- A target that is currently dead, disconnected, in another dimension, or otherwise non-interactable cannot be resurrected by a historical hit.
- Persistent projectiles are **not** rewound in v1. The server spawns and simulates them from current authoritative state; the client may predict visuals. Projectile fast-forward is a separate experiment if bows later feel unacceptably delayed.

The server derives mapped time from ongoing clock synchronization, enforces monotonic action ticks, and caps rewind by policy. A forged old tick can therefore gain at most the configured window and still cannot bypass current cover.

### 5. Remote support-loss grace

This section specifies an **off-by-default A/B branch**, not required behavior. It must be tested against current-time authority plus presentation-only smoothing, including collusion, repeated bridging, traps, knockback, jumping, explosions, and replay. Delete it if no material user benefit survives those cases.

Whole-world rewind does not solve the floor-removal case cleanly. Instead, when a committed edit by another actor changes a supporting cell from collidable to non-collidable, the server may create a **support lease** for a player whose authoritative collider was grounded on that face:

```text
duration = clamp(estimated_one_way_delay + jitter_margin, 50 ms, 150 ms)
scope    = only the removed cell's upward support face, only for that player
ends     = player reaches other support, jumps, moves vertically by ability,
           leaves the cell footprint, dies/teleports, or the deadline expires
limit    = one lease per grounded episode; no renewal by further edits
```

During the lease, only downward collision for that player treats the removed top face as support. The cell remains air for rendering, raycasts, all other players, and all other collision faces. Gravity does not advance that player downward while the lease is active. The owner snapshot includes remaining lease ticks so local prediction can reproduce it.

This is a small “coyote time caused by remote world edits,” not general hovering. It does not apply when the player removed their own support, voluntarily jumped, or was already airborne. The one-lease-per-grounded-episode rule prevents collaborators from repeatedly breaking supports to hold a player up. Whether explosions receive the same grace is deferred to gameplay policy; v1 applies it only to direct player edits.

If a late movement command shows the player reaching adjacent confirmed support before expiry, no fall or large downward reconciliation occurs. Otherwise the player begins falling when the lease expires. Latency beyond 150 ms is deliberately not hidden indefinitely.

### 6. Abuse and failure behavior

- Token-bucket limits apply separately to movement inputs, edit attempts, combat actions, and stale retries. Rejected attempts consume a token.
- Action history and change history are bounded by time and count. Disconnect clears them; dimension transfer starts a new action epoch.
- Invalid vectors, non-finite numbers, impossible sequence jumps, and coordinates outside loaded/authorized interest are rejected before raycasting.
- Action outcomes use a reliable ordered application channel, but duplicate result requests remain safe. Visual swing/mining progress may be unreliable.
- If history is missing because of server overload, migration, or teleport, validation falls back to current state and reports `compensation=none`; it never trusts client history.
- Metrics: compensation window used, stale-edit ratio, correction magnitude, support-lease count/outcome, validation rejection reason, history fallback, and per-action validation time.

## Greenlight criteria

- The prototype demonstrates idempotent edit/inventory outcomes under loss, duplication, and reordering.
- Current-time validation never accepts duplicate damage/drops, extra reach, stale inventory spend, or a through-current-cover action.
- Combat rewind and support grace each receive their own `greenlight`, `defer`, or `reject` result. Failure of either branch cannot block the v1 block-edit protocol.
- Any enabled compensation history meets byte and CPU caps derived from the declared first-playable concurrency target.

## Prototype or benchmark

Required: yes  
Smallest useful experiment: a headless C# harness with two players, a 16³ mutable test volume, authoritative input simulation, and current-time block prediction/reconciliation. Add pose rewind and the support lease as separately toggled branches after the baseline passes. Drive all branches through the same deterministic network fault injector.

Test matrix:

- RTT: 0, 50, 100, 200, and 350 ms;
- jitter: 0, 20, and 50 ms;
- random loss: 0%, 1%, and 5%, plus duplicate/reordered action packets;
- scenarios: stale break target replaced with another block; competing placements; movement as support is removed; wall placed/removed during melee; forged old/future ticks; action replay; unloaded target; declared acceptance load plus a separate stress load. Support-grace runs must add collusion, repeated bridges/traps, jump, knockback, and explosion cases.

Success metrics:

- zero divergent final block/inventory states and zero duplicate accepted effects across the fault matrix;
- zero accepted melee hits where the voxel ray is blocked at either historical or current evaluation time;
- for a player whose valid movement reaches adjacent support before lease expiry, at least 99.9% of runs at RTT <= 200 ms and jitter <= 20 ms avoid a downward correction over 0.10 block;
- report p50/p95/p99 validation cost and retained history bytes at the declared acceptance and stress loads; freeze budgets only after targets exist;
- pose and voxel-change history remains within its configured hard cap and missing history falls back to current-time validation;
- every rejected prediction is repaired within one action-result delivery plus one simulation snapshot.

## Risks and open questions

- The support lease is a novel gameplay rule, not a copied standard. It may feel like momentary hovering or alter trap/PvP timing; the prototype needs playtesting, not only correctness tests.
- “Clear both historically and currently” favors defenders/builders and can reject a hit that looked valid to the attacker. A different fairness choice cannot eliminate the paradox; it only chooses who sees it.
- The exact rewind cap may need mode-specific tuning. Competitive servers may choose 100 ms; cooperative servers may choose 200–250 ms, but the server must publish the value.
- Reconstructing historical collision from a change log assumes NET-05 keeps the relevant cells resident. Missing history must safely degrade to current-only validation.
- Godot and server collision implementations must agree closely enough for prediction. This decision does not require bitwise-deterministic general physics, but player voxel collision needs shared test vectors.
- Native plugins must not be allowed to mutate live state while a historical query is in progress or retain references to historical views.

## Dependencies

- Requires: ARCH-01 authority model; NET-01 authoritative simulation choice; NET-02 movement prediction/reconciliation; NET-03 transport channels; NET-06 tick/time mapping; NET-07 protocol evolution.
- Blocks: combat implementation; block edit protocol; anti-cheat thresholds; gameplay trap/explosion semantics.

## Rejected or deferred alternatives

- Client-declared hits, positions, or successful edits: rejected because post-hoc plausibility checks cannot make result authority secure.
- Full terrain rollback and re-simulation: rejected for v1 because it multiplies history, plugin determinism, conflict-ordering, and collision-restoration complexity.
- One-second rewind copied from Source: rejected because short-range melee plus player-built cover makes defender paradoxes much more severe.
- Per-client permanent collision worlds: rejected because they destroy a single authoritative simulation and create plugin/redstone/entity inconsistencies.
- Projectile rewind/fast-forward: deferred until a bow prototype proves current-time server spawning is insufficient.
- Hiding arbitrary high latency: rejected as an impossible absolute requirement; beyond the bounded window, VibeCraft degrades to current-time authority.
