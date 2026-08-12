# NET-06 Tick and simulation rates

Status: Proposed

Owner: Networking architecture sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Owner-selected choice: Make `WORLD-08`'s fixed **60 TPS** `WorldTick` the only
authoritative v1 clock. Sample devices and render independently; process one predicted
movement frame per world tick while packet bundles and snapshots use measured,
independent cadences. Slower systems run from explicit deadlines/divisors.

Persist deterministic gameplay schedules as absolute `DueWorldTick` values and define explicit conversions from authored durations/rates. The server advertises the clock epoch/rate during handshake. The Godot client predicts on that timeline and renders through interpolation independently of packet and render rates.

One-sentence rationale: One 16.67 ms authority grid provides the owner-selected PvP
and bridging granularity without creating separate movement/world timelines, while
independent subsystem and packet cadences keep unrelated work from running 60 times
per second.

The current spec's “configurable between 32, 64, or 128 ticks per second” is rejected as a v1 profile menu. It changes replay grids and content timing while multiplying work without evidence. These frequencies remain comparison data, not compatibility promises.

## Context and constraints

- Movement, voxel collision, support-loss compensation, attacks, and projectiles need deterministic phase ordering and a constant delta. A variable delta changes collision behavior and makes client replay much harder.
- A voxel server also runs scheduled blocks/redstone, liquids, random ticks, block entities, AI, interest calculations, section publication, generation, and persistence. These workloads have different latency requirements and some are bursty.
- “Minecraft-like timing” cannot mean copying Minecraft's raw tick counts now that
  VibeCraft has a fixed 60 TPS clock. Authored durations must preserve intended elapsed
  time and convert through named rules.
- The client is Godot C#, while the authoritative server is separate C#. Godot's default physics rate therefore cannot silently define protocol or gameplay time.
- UDP input and snapshots need their own rates and congestion budget. Raising simulation rate must not multiply bulk terrain traffic or bypass NET-03's congestion control.
- A slow server must expose overload and degrade background work. It must not stretch the physics delta to consume arbitrary wall time, skip authoritative collision steps, or silently change profiles.
- Tick rate is not a cure for high latency. NET-04's current-time reconciliation
  addresses only part of latency; any future historical query is a separately
  negotiated capability. This decision limits quantization and scheduling delay.

## Options considered

| Option | Strengths | Costs/risks | Fit for VibeCraft |
| --- | --- | --- | --- |
| A. Fixed 20 Hz authoritative world loop, with slower scheduled systems | Simple; familiar mechanic timing; one replay/order grid; low per-tick overhead | Coarser than the selected PvP/bridging target | Superseded by owner decision |
| B. One configurable 32/64/128 Hz rate for every system and every packet | Easy configuration story; no cross-rate scheduler | AI, chunk activation, block entities, and snapshots scale needlessly; mechanic timing can accidentally change with profile; 128 Hz is expensive | Rejected |
| C. 20 Hz world with an exactly nested 40 Hz player substep | Preserves one commit clock while testing finer collision response | Two controller/world observation grids add complexity and still miss the selected target | Superseded |
| D. Variable-delta simulation driven by elapsed wall time | Can appear to keep game time aligned after a slow frame | Collision and timers become load-dependent; prediction/replay diverges; a long frame can tunnel through voxel terrain | Rejected for authoritative gameplay |
| E. Mandate 128 Hz globally | Lowest step quantization in isolation | 7.8125 ms budget; changes content/replay and multiplies fixed work | Reject for v1 |
| **F. Fixed 60 TPS authority with independently paced subsystems/network** | One responsive replay/commit grid; aligns with selected product feel; slower work remains schedulable | 16.67 ms capacity is a hard engineering constraint | **Owner selected; capacity-gated** |

## Evidence

Labels used below: **Fact** is directly supported by the linked implementation/document; **Inference** is a conclusion drawn from those facts; **Recommendation** is VibeCraft policy.

### Minecraft

- **Fact (Java snapshot 23w43a):** Mojang describes Minecraft's normal rate as 20 ticks per second. Lower target rates slow player simulation; higher targets keep player simulation at 20 TPS while other parts run faster, may create visual interpolation artifacts, and can crash the game when the computer cannot keep up. See the official [Java Edition Snapshot 23w43a notes](https://feedback.minecraft.net/hc/en-us/articles/20707371679117-Minecraft-Java-Edition-Snapshot-23w43a).
- **Fact (Java snapshot 21w38a):** Mojang introduced simulation distance separately from render distance; entities outside simulation distance are not updated and a lower simulation distance permits a greater render distance with less CPU load. See the official [Java Edition Snapshot 21w38a notes](https://feedback.minecraft.net/hc/en-us/articles/4409891990285-Minecraft-Java-Edition-Snapshot-21w38a).
- **Inference:** Minecraft demonstrates both the simplicity of a low fixed gameplay cadence and the value of decoupling expensive simulation scope from what a player can see. It does not establish that 20 Hz is ideal for VibeCraft's predicted movement, nor that one frequency should drive every subsystem.
- **Version warning:** early Java around Beta 1.0 and release 1.0 did not have the modern simulation-distance control or `/tick` tooling. Matching their mechanics is a separate compatibility decision from copying their server scheduler.

### Luanti (formerly Minetest)

- **Fact:** Luanti's current example configuration defaults `dedicated_server_step` to 0.09 seconds and warns that this is a lower bound because actual steps are usually longer. The same configuration independently exposes active-object management, ABM, node-timer, liquid, and server-map-save intervals. See [`minetest.conf.example`](https://raw.githubusercontent.com/luanti-org/luanti/master/minetest.conf.example#L3194-L3272).
- **Fact:** the server loop records an actual elapsed `dtime`, passes it into the environment step, and performs network and maintenance work around that loop. See current [`src/server.cpp`](https://raw.githubusercontent.com/luanti-org/luanti/master/src/server.cpp#L105-L163) and the [server-thread loop](https://raw.githubusercontent.com/luanti-org/luanti/master/src/server.cpp#L623-L700).
- **Inference:** Luanti is evidence that a practical voxel clone needs independently configured cadences. Its “lower bound, usually longer” main step is not a good prediction contract for VibeCraft: the client would have to replay load-dependent deltas.

### Veloren

- **Fact:** Veloren's current clock has a fixed target duration but, when a tick overruns, returns the measured duration for that tick; its source comments state that all systems must use the supplied delta and that the clock deliberately does not catch up by averaging. See [`veloren_common::clock`](https://veloren.gitlab.io/veloren/src/veloren_common/clock.rs.html).
- **Inference:** this is a coherent variable-delta policy for that engine, but it trades deterministic fixed-step replay for wall-clock continuity. VibeCraft should make the opposite trade because local prediction and sharp voxel collision are first-order requirements.

### General-purpose engines and networking libraries

- **Fact (Valve Source):** Source distinguishes server simulation ticks, client command updates, and snapshots; the documented example sends snapshots at a lower rate than the roughly 66 Hz server simulation. Valve also warns that changing tickrate can cause timing issues and that mods may assume a particular rate. See [Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking#Basic_networking).
- **Fact (Unity Netcode for Entities):** Unity documents higher simulation tick rates as more responsive but more CPU-intensive, and warns that an overloaded server that cannot maintain its rate causes latency and rubber-banding. See [Ticks and update rates](https://docs-multiplayer.unity3d.com/netcode/2.1.1/learn/ticks-and-update-rates/).
- **Fact (Unity Netcode for Entities):** Unity's prediction loop uses a fixed `SimulationTickRate`, can use a lower `NetworkTickRate`, and recommends that the network rate be a common factor of simulation rate. See [Introduction to prediction](https://docs.unity.cn/Packages/com.unity.netcode%401.5/manual/intro-to-prediction.html).
- **Fact (Godot 4):** Godot's physics interpolation documentation says physics logic should run at a fixed predetermined rate, identifies 60 TPS as its default, and warns that variable frame-rate movement creates inconsistent motion and collision. It also notes that multiplayer games may need custom interpolation. See [Introduction to physics interpolation](https://docs.godotengine.org/en/stable/tutorials/physics/interpolation/physics_interpolation_introduction.html).
- **Inference:** the shared pattern is a fixed simulation contract plus separately paced input, snapshots, and rendering. The exact rate is a capacity/product choice that must be measured in VibeCraft's workload.

## Proposed design

### Normative v1 contract

- One monotonic unsigned 64-bit `WorldTick` advances at exactly 60 ticks per rational
  simulation second and is owned by `WORLD-08`. Do not accumulate a rounded integer
  nanosecond delta; derive elapsed time as ticks over 60 or use a remainder-safe clock.
- Player movement, interactions, entity commits, block updates, immutable network observations, and persistence observations use that one phase graph.
- Input-device sampling and rendering run independently. The client emits one quantized predicted frame per `WorldTick`; bundles repeat a bounded number of unacknowledged frames/intents.
- Begin snapshot generation at a measured 20 or 30 Hz divisor of `WorldTick`; input
  packets may carry several 60 TPS frames and bounded redundancy. Coalesce obsolete
  unsent snapshots under congestion.
- Slower AI, random ticks, activation, saves, and jobs use divisors/deadlines of `WorldTick`; they do not create another authority clock.
- Persist absolute `DueWorldTick` for deterministic scheduled mechanics. Authored real-time durations convert through a versioned, named rounding rule.
- No nested controller clock or configurable tick profile exists in v1. A future rate
  change is an explicit gameplay/protocol compatibility decision.

### Superseded multi-profile sketch (retained as comparison, not adopted)

The following 32/64/128 profile design was the first-wave proposal. It is retained
only as historical comparison after the owner selected fixed 60 TPS; none of its
profile tables or constants is normative.

> **ARCHIVAL BLOCK START — rejected first-wave design. Do not derive implementation
> constants, schemas, schedules, or plugin APIs from this block.**

### 1. Time model and profiles

The authoritative clock uses a monotonic `uint64 tick_id` and integer `sim_time_ns`. The supported master steps divide one second exactly:

| Profile | Master simulation | Snapshot generation | Client input sampling | Input packet sends | Status |
| --- | ---: | ---: | ---: | ---: | --- |
| Balanced | 32 Hz / 31,250,000 ns | 16 Hz | 32 Hz | up to 32/s | Default |
| Responsive | 64 Hz / 15,625,000 ns | 32 Hz | 64 Hz | up to 64/s | Opt-in after benchmark |
| Experimental | 128 Hz / 7,812,500 ns | 32 Hz | 128 Hz | up to 64/s, batching two commands | Not a shipping promise |

Input packets may batch consecutive commands and repeat the newest unacknowledged commands within NET-03's bounded redundancy policy. Command timestamps are tick identities mapped through time synchronization, not client wall-clock values. Increasing the master rate does not increase terrain baseline or event traffic.

The profile is chosen at server/world-session start and advertised in the handshake with `profile_id`, `simulation_hz`, `snapshot_hz`, and exact `fixed_delta_ns`. Hot switching is deferred: changing the replay grid while clients have prediction history and actions in flight adds risk without a v1 need.

All gameplay configuration uses durations or rates per simulated second:

```text
break_duration_ns
attack_cooldown_ns
fuse_duration_ns
scheduled_block_due_ns
random_tick_attempts_per_section_second
fluid_update_period_ns
```

A 50 ms legacy delay is due at an exact simulated timestamp. It runs on the first master step at or after that deadline, so quantization is bounded by one fixed step. Repeating work advances `next_due += period`, not `now + period`, preventing long-term drift. Save files persist remaining duration or due simulation time, not “ticks remaining” whose meaning changes by profile.

### 2. Deterministic simulation phases

Every master step executes one fixed phase graph:

```text
1. Drain validated network commands into per-player ordered queues
2. Apply player intent and abilities
3. Simulate dynamic movement/projectiles and resolve voxel/entity collision
4. Resolve block/entity interactions and NET-04 lag-compensated queries
5. Commit voxel changes, support leases, damage, spawns, and despawns
6. Run due gameplay schedulers within their budgets
7. Publish one immutable end-of-step state/revision view
8. At due cadence, build snapshots and interest deltas from that view
9. Queue persistence/chunk/background jobs; never wait for them here
```

Systems do not mutate shared collections in arbitrary worker completion order. Parallel systems produce command buffers sorted by deterministic keys before the commit phase. Chunk generation, lighting, compression, pathfinding, and persistence may run on workers, but results become visible only through a bounded publication queue at a named phase.

Voxel edits committed in phase 5 affect collision beginning with the next master step unless the same-step action explicitly defines a deterministic consequence. A remote floor removal also creates NET-04's support lease in that commit, preventing worker or packet timing from deciding whether gravity ran first.

### 3. Subsystem cadence

The master tick is a scheduling grid, not an instruction to run every system every step:

| Work | Initial cadence/policy | Reason |
| --- | --- | --- |
| Player/vehicle movement, active projectile motion, voxel collision, support leases | Every master step | Prediction and interaction-sensitive collision |
| Awake nearby physical entities | 32 Hz minimum; every master step in Balanced, every 2/4 steps in 64/128 unless interacting with a player | Bounds high-profile CPU while retaining a stable 32 Hz physical cadence |
| Sleeping items and low-risk object motion | 16 Hz or event-woken | Numerous and rarely latency-critical |
| Due scheduled blocks/redstone | Deadline queue checked every master step; per-tick work budget | Preserves elapsed-time semantics and bounds spikes |
| Liquids/fire/growth/random ticks | WORLD-08 duration/rate, normalized per simulated second; spatial work budget | Profile must not speed up the world |
| AI sensing/decision | 8 Hz default; event wake for damage/near contact | Decisions do not need movement cadence |
| Path search | Asynchronous, at most 2 requests/s/entity initially; stale result rejection | Expensive and bursty |
| Entity replication snapshots | 16 Hz Balanced, 32 Hz Responsive/Experimental | Independent network/product trade-off |
| AOI incremental diff | 8 Hz plus immediate section crossing, teleport, and policy changes | Avoid full interest recomputation every step |
| Simulation activation/deactivation | 2 Hz plus explicit tickets | Large-scale voxel scope changes slowly |
| Chunk generation, meshing inputs, compression | Bounded asynchronous jobs | Must not consume fixed-step latency budget |
| Persistence snapshot/flush | Asynchronous, nominal 0.2 Hz plus lifecycle events | Durability cadence is not gameplay cadence |

“32 Hz minimum” for awake physical entities means a fixed 31.25 ms entity delta even under a higher master profile; it is not a variable elapsed delta. An entity that becomes player-interactive, is struck, enters a fast-moving state, or approaches a collision-risk threshold is promoted to master cadence before resolving that interaction. The prototype must test whether this promotion rule is sufficient; if mixed-rate physical contact proves unstable, all awake collision bodies remain at master cadence and the capacity cost becomes part of profile admission.

Scheduled voxel work uses both temporal and spatial budgets. Work that misses its due time remains ordered and is reported as backlog; it is not discarded. Random ticks use a deterministic accumulator/PRNG keyed by world, section, and simulation-time bucket so changing the master rate does not change the expected attempts per simulated second.

### 4. Scheduler and plugin interface

The server exposes time explicitly:

```csharp
readonly record struct SimulationClock(
    ulong TickId,
    ulong SimTimeNs,
    ulong FixedDeltaNs,
    SimulationProfileId Profile);

interface IScheduledSystem
{
    SystemPhase Phase { get; }
    ulong PeriodNs { get; }             // 0 means every master step
    int MaxWorkUnitsPerRun { get; }
    void Run(in SimulationClock clock, SimulationCommands output);
}
```

The real scheduler also supplies deterministic continuation cursors and telemetry. Plugins may schedule `RunAt(sim_time_ns)` or periodic durations, but may not sleep, read wall time for gameplay, mutate authoritative state from workers, or infer that one tick equals 50 ms. APIs expose typed duration values and semantic events rather than raw “delay N ticks” convenience methods.

Rate-sensitive protocol values name their unit and version. Tick IDs are valid only with the connection's advertised profile/epoch; NET-04 maps client prediction ticks to server history and clamps the result. A client cannot request a higher profile, report a different fixed delta, or advance more commands than server time permits. Excess/future commands are rate-limited and rejected rather than creating simulation work.

### 5. Godot client prediction and rendering

The client uses a custom C# fixed-step prediction accumulator at the server-advertised `fixed_delta_ns`. It records one input command and predicted local state per prediction tick, then reconciles to authoritative state and replays unacknowledged commands. It does not assume Godot's default 60 Hz is the server rate.

Remote entities and confirmed terrain transitions render from buffered snapshots. Rendering remains frame-rate independent and interpolates with a deliberately chosen delay of at least one snapshot interval plus measured jitter margin. The local predicted player may use visual smoothing after reconciliation, but its collision state is corrected immediately. A snapshot contains the server tick/time used to create it, so 16/32 Hz snapshots can interpolate states produced by 32/64/128 Hz simulation.

Changing blocks are special: authoritative collision changes at a master-tick boundary, while rendering may animate or smooth only if doing so cannot misrepresent a still-solid cell as traversable. The local predicted block overlay and support lease are governed by NET-04, not generic transform interpolation.

### 6. Overload policy

The server measures step CPU time, wall-clock backlog, scheduler deadlines, worker queues, and snapshot queue age. It uses a fixed delta even while overloaded.

- Run at most four catch-up steps consecutively before yielding to network I/O and worker completion; continue catching up without discarding simulation steps when capacity returns.
- At 250 ms sustained wall-clock backlog, enter `DEGRADED`: pause speculative generation/prefetch, reduce far LoD and P3/P4 sends, slow noncritical AI/path requests, and stop increasing interest distance. Do not reduce near collision scope or P0/P1 outcomes.
- If backlog remains above two seconds for ten seconds, reject new joins and report an unhealthy server. An operator may restart with a lower profile or capacity limit; the process does not silently switch the active world's tick grid.
- Never feed one giant elapsed delta into movement, process unbounded overdue voxel work in one step, or let snapshot serialization block simulation.
- Snapshot generation may coalesce obsolete unsent snapshots under congestion, but the authoritative simulation remains complete and action results remain ordered.

This policy can make simulation time lag wall time during severe overload. That is preferable to tunneling, divergent prediction, or load-dependent mechanic durations, but it is still service failure and must be visible in metrics and the server browser.

> **ARCHIVAL BLOCK END — current rules resume below.**

## Greenlight criteria

- Product uses one fixed 60 TPS `WorldTick` as the v1 gameplay/replay contract;
  alternate master profiles are absent from normal configuration.
- Server and client share exact world-tick epoch/rate metadata and pass replay/ordering tests at 60 TPS.
- Input/snapshot packet cadence is measured independently and cannot advance authoritative time or multiply bulk terrain traffic.
- No authoritative gameplay system uses variable wall-clock delta, frame count, or an implicit engine-render rate.
- The declared v1 workload sustains the fixed cadence with measured headroom; failure
  revises workload scope, activation, budgets, or architecture rather than silently
  changing tick rate.
- Numeric CPU, player-count, entity-count, and bandwidth gates are frozen only after target platforms, hardware, concurrency, view distance, and uplink are declared.

## Prototype or benchmark

Required: yes  
Smallest useful experiment: a headless C# 60 TPS fixed-step server and Godot prediction
client using the planned collision library. Begin with player walking/jumping,
editable support/path cells, input bundling, independently paced snapshots,
deterministic command buffers, and synthetic asynchronous load. Add the declared
acceptance workload only after correctness passes.

Acceptance fixture:

- 16 clients in four dense groups across 512 active sections;
- 500 awake physical/AI entities, 2,000 items, and 100 active projectiles;
- 10,000 due block/redstone/liquid work items per second with controlled bursts of 50,000;
- fixed 60 TPS for correctness and load, with 60 TPS predicted movement frames and
  independently measured packet/snapshot divisors;
- 0/50/100/200 ms RTT, 0/20 ms jitter, and 0/1/5% loss for prediction/network runs;
- induced 100, 250, and 500 ms worker/CPU stalls, without sleeping the network receive path.

Success metrics:

- report p50/p95/p99 authoritative step CPU and GC behavior on recorded reference hardware; set the production headroom threshold only after the acceptance workload is approved;
- absent induced overload, p99 wall-clock start jitter is below half a master step, scheduled-work backlog stays below 100 ms, and at least 99.9% of ordinary due events execute in `[deadline, deadline + one master step]`;
- repeated 60 TPS traces are semantically deterministic across supported platforms;
- random tick, fluid, fuse, cooldown, and break-duration counts/times remain within their specified elapsed-time tolerance and do not scale with master Hz;
- under the 500 ms induced stall, no movement step uses a variable delta and no authoritative step is dropped; after the stall ends, backlog returns below 100 ms within two seconds on the acceptance hardware;
- client prediction produces no persistent divergence, and under 100 ms RTT / 20 ms jitter the p99 visible local correction caused solely by rate/snapshot quantization stays below 0.10 block;
- snapshot and input bandwidth remain within NET-03's configured budgets, with obsolete snapshots coalesced rather than queued indefinitely.

## Risks and open questions

- Mixed-rate physical entities can produce contact-order artifacts. Promotion to master cadence is a hypothesis requiring a collision corpus; a simpler all-awake-at-master policy may be worth its CPU cost.
- A 60 TPS clock does not imply Source-style subtick combat or bug-for-bug Minecraft
  ordering. `WORLD-08`, `NET-04`, and `GAME-02` explicitly define VibeCraft phases.
- Fixed-step catch-up preserves mechanics but can worsen latency during overload. Capacity limits, interest shedding, and operator-visible health are mandatory; scheduler design cannot create CPU that is not available.
- One second does not divide into an integer number of nanoseconds at 60 TPS. Clock
  code must use tick/rational arithmetic or remainder compensation; public duration
  types must prevent cumulative rounded-delta drift.
- Snapshot interpolation delay trades smoothness for latency. Begin at no more than one snapshot per world tick and tune coalescing/interpolation in the impairment harness; movement simulation rate alone does not determine feel.
- Asynchronous chunk publication must be deterministic enough for authoritative results. Bitwise-deterministic worker execution is unnecessary if publication order and generated content identity are fixed.
- Mods may genuinely require per-tick callbacks. Such callbacks consume the 60 TPS
  world budget and need declared quotas; compatibility cannot imply unlimited work.
- The first-playable hardware/concurrency target is not yet fixed. Benchmark results must record CPU model, runtime, GC mode, build, and workload rather than becoming context-free TPS claims.

## Dependencies

- Requires: ARCH-01 authority and thread model; NET-03 transport, command batching, and congestion control; NET-04 reconciliation/history mapping; NET-05 interest/scope shedding; WORLD-01 section representation; WORLD-08 voxel scheduling semantics; MOD-01 plugin API.
- Blocks: final server capacity limits; prediction buffer sizing; snapshot schema and interpolation delay; replay/debug tooling; performance presets and server-browser health reporting.

## Resolved, rejected, or deferred alternatives

- A single fixed 60 TPS authoritative world loop: owner selected, pending the explicit
  correctness/capacity prototype.
- One global configured rate for all systems and packet types: rejected because it couples responsiveness to unrelated CPU and bandwidth work.
- Variable-delta authoritative physics: rejected because load changes collision and undermines deterministic client replay.
- Dropping simulation steps to catch wall time: rejected because it skips collisions, deadlines, and action ordering.
- Silent runtime profile reduction under load: rejected because it invalidates prediction history and hides an unhealthy server.
- Sending a snapshot every simulation step: rejected; snapshot cadence is a separate congestion and smoothness decision.
- Encoding gameplay delays as profile-relative or unnamed tick counts: rejected.
  Persisted schedules use absolute `DueWorldTick` on the one fixed-rate clock, with
  authored durations converted by a versioned named rounding rule.
- 32/64/128 Hz authoritative profiles: rejected for v1; revisit only as a future architecture decision with evidence, not as a hidden configuration toggle.
- Delegating authoritative timing to Godot's project physics setting: rejected. The
  matching 60 default is coincidental; the standalone shared clock is the contract.
