# NET-04 Block interaction and combat lag compensation

Status: Proposed

Owner: Networking architecture sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Keep the live simulation and terrain fully server-authoritative and greenlight only idempotent, current-time block/action reconciliation for v1. Treat combat rewind and remote support-loss grace as independent later experiments, both disabled by default and neither required by the movement, transport, or block-edit architecture.

One-sentence rationale: Current-time validation is the smallest secure baseline; historical combat and per-player phantom support add fairness, abuse, history, and gameplay-rule costs that must demonstrate a concrete benefit before becoming contracts.

### Owner decision — 2026-08-13

The owner selected option **A**. V1 validates placement, breaking, melee, and use from
the actor's receive-time authoritative state. It keeps action identity and prediction
repair clean enough that a later version can add a Source-2-like subtick/historical
capability, but v1 allocates no pose or voxel rewind history for action validation.

This is **not** a recommendation for whole-world rollback. V1 action queries use the
current authoritative world and commit once at the current tick.

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

### 1. Current time and immutable action identity

V1 sets `effective_world_tick = current_world_tick` for every gameplay action. A
wrapping `ClientPredictionStep`/latest input sequence remains in the request so action
ordering, prediction repair, diagnostics, and a future negotiated subtick capability
have a clean seam. The server never accepts a raw client wall-clock or lets the client
choose an evaluation tick.

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

`aim_origin` is evidence for diagnostics, not trusted authority. The server uses the
actor's current authoritative eye pose after processing admitted movement for the
step. `expected_section_revision` makes the target conditional on the owning section
snapshot: breaking stone at revision 41 cannot accidentally break the chest that
replaced it at revision 42. Inventory revision is a distinct unsigned domain and is
never compared with a section revision.

### 3. Block breaking, placement, and stale targets

The client may immediately render a predicted local block state and animation in a separate overlay. It must not overwrite the last confirmed world state. Collision prediction may consult that overlay only for the local predicted player and must be replayable.

For every block action the server:

1. processes already queued movement inputs for the tick before interactions;
2. reads the actor's current authoritative eye pose;
3. requires the current cell state and revision to equal the request's expectation;
4. raycasts and reach-checks only against the current authoritative world;
5. checks tool, inventory revision, permissions, cooldown, and edit rate in current authoritative state;
6. commits once at the current tick and returns authoritative affected cells and inventory revision.

`BeginBreak` records the server-validated target and current authoritative start tick.
`FinishBreak` must reference that begin action and satisfy tool-specific duration. The
client cannot claim an arbitrary earlier start. Placement and instant-use actions
commit only to current state.

If the target is stale, unloaded, protected, or out of reach, the server sends a result plus the minimum authoritative cells/inventory slots needed to remove the prediction. It must not synchronously load a chunk for an untrusted request.

### 4. Combat and entity interaction

This section specifies a **post-v1 negotiated subtick experiment**, not v1 behavior.
Do not allocate pose/voxel rewind history until a later product decision defines the
combat rules, target concurrency, and protocol capability.

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

This would be a small “coyote time caused by remote world edits,” not general hovering. It would not apply when the player removed their own support, voluntarily jumped, or was already airborne. The one-lease-per-grounded-episode rule prevents collaborators from repeatedly breaking supports to hold a player up. If a later experiment is authorized, its first branch should cover only direct player edits; explosions remain a separate gameplay-policy question. V1 has no support lease.

If a late movement command shows the player reaching adjacent confirmed support before expiry, no fall or large downward reconciliation occurs. Otherwise the player begins falling when the lease expires. Latency beyond 150 ms is deliberately not hidden indefinitely.

### 6. Abuse and failure behavior

- Token-bucket limits apply separately to movement inputs, edit attempts, combat actions, and stale retries. Rejected attempts consume a token.
- Action-result history is bounded by time and count. Disconnect clears it; dimension
  transfer starts a new action epoch. V1 keeps no action-validation rewind history.
- Invalid vectors, non-finite numbers, impossible sequence jumps, and coordinates outside loaded/authorized interest are rejected before raycasting.
- Action outcomes use a reliable ordered application channel, but duplicate result requests remain safe. Visual swing/mining progress may be unreliable.
- Metrics: stale-edit ratio, correction magnitude, validation rejection reason, and
  per-action validation time. Future compensation metrics exist only when that
  capability is implemented.

## Greenlight criteria

- The prototype demonstrates idempotent edit/inventory outcomes under loss, duplication, and reordering.
- Current-time validation never accepts duplicate damage/drops, extra reach, stale inventory spend, or a through-current-cover action.
- Combat rewind/subtick and support grace remain separately deferred and cannot block
  the v1 block-edit/combat protocol.

## Prototype or benchmark

Required: yes  
Smallest useful experiment: a headless C# harness with two players, a 16³ mutable test
volume, authoritative input simulation, and current-time block/combat prediction and
reconciliation. Do not add pose rewind or support leases to the v1 experiment. Drive
the baseline through the deterministic network fault injector.

Test matrix:

- RTT: 0, 50, 100, 200, and 350 ms;
- jitter: 0, 20, and 50 ms;
- random loss: 0%, 1%, and 5%, plus duplicate/reordered action packets;
- scenarios: stale break target replaced with another block; competing placements;
  movement as support is removed; wall placed/removed before receive-time melee;
  forged old/future ticks; action replay; unloaded target; declared acceptance load
  plus a separate stress load.

Success metrics:

- zero divergent final block/inventory states and zero duplicate accepted effects across the fault matrix;
- zero accepted melee hits where the receive-time authoritative voxel ray is blocked;
- report p50/p95/p99 validation cost at the declared acceptance and stress loads;
  freeze budgets only after targets exist;
- every rejected prediction is repaired within one action-result delivery plus one simulation snapshot.

## Risks and open questions

- The support lease is a novel gameplay rule, not a copied standard. It may feel like momentary hovering or alter trap/PvP timing; the prototype needs playtesting, not only correctness tests.
- Receive-time authority favors the current defender/world and will reject some actions
  that looked valid on a high-latency client. That is the selected v1 tradeoff. A
  future subtick capability must explicitly choose its rewind cap and cover paradox.
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
- Historical/subtick block and melee validation: deferred to a later negotiated
  protocol/gameplay capability after v1 current-time behavior ships cleanly.
- Hiding arbitrary high latency: rejected as an impossible absolute requirement; beyond the bounded window, VibeCraft degrades to current-time authority.
