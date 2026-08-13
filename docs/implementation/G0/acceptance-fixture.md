# G0 first-playable benchmark and acceptance fixture

**Fixture ID:** `VC-G0-FP-0.1.0`
**Status:** **PROVISIONAL — owner acceptance required**
**Scope:** First-playable benchmark and acceptance evidence only

This fixture turns the current owner decisions and proposed requirements baseline
into one reproducible first-playable test envelope. It is not a shipping promise,
minimum-specification announcement, capacity commitment, or a claim that any
benchmark has passed. A result is meaningful only when its report identifies this
fixture version and fills the required run metadata.

Owner decisions take precedence over the proposed baseline. Update this document
with a new fixture version before interpreting evidence against changed platforms,
hardware, workload, radii, network conditions, durability terms, or exposure.

## First-playable acceptance loop

The accepted loop is a small multiplayer vertical slice, not a survival milestone:

1. Start a private local session or connect by invitation to a server.
2. Two playable clients enter one original test dimension, move, look, jump,
   collide, see each other, stream nearby terrain, and place/break a small fixed
   block set through authoritative actions.
3. Disconnect and reconnect to the same committed world state.
4. Explicitly save/quit, then recover correctly after a forced server termination
   and restart.

The server remains authoritative at one fixed 60 TPS `WorldTick`; local movement
prediction and remote interpolation do not create a second authority clock. The
fixture does not add survival systems, mobs, redstone, executable mods/plugins,
Minecraft conversion or compatibility, additional dimensions, public-anonymous
hosting, or far-terrain LoD. Ordinary distance/height fog closes the full-detail
view for this first-playable fixture.

## Fixed build and display envelope

| Item | Fixture requirement |
| --- | --- |
| Client and server platforms | Windows x64 and Linux x64 |
| Runtime | .NET 10 |
| Client engine | Godot **4.7.1 .NET** (future pinned target; record the exact delivered build when available) |
| Build configuration | Release |
| Display target | 1,920 × 1,080 at 60 Hz |
| Authority clock | 60 TPS `WorldTick` |
| Exposure | Private local and invite-only sessions; public-anonymous/direct-IP exposure is outside this fixture |

Both supported operating systems require their own packaged-build evidence. A pass
on one does not establish a pass on the other.

## Workload and visibility envelope

| Measure | Fixture value | Meaning |
| --- | --- | --- |
| Playable load | 2 players | Required end-to-end first-playable acceptance case. |
| Capacity acceptance load | 8 human-scale players | Acceptance workload, not a shipping capacity promise. |
| Stress observation | 16 bots | Non-promised stress evidence only; it cannot be represented as supported player capacity. |
| Full-detail radius | 8 horizontal section radius; 4 vertical section radius | **Provisional test input.** It is not a view-distance promise and must not freeze a section edge/side before G1 evidence selects one. |
| Far terrain | None | No far LoD is required to pass this first-playable fixture. |

The workload report must state bot behavior, player movement/teleport pattern,
world age/content, warm-up, duration, and any deliberate edit, reconnect, save, or
fault sequence. “Human-scale” requires a documented behavior profile; it does not
mean that an unattended bot result automatically proves player experience.

## Network fixture — provisional values pending owner capture

Network impairment and usable bitrate are fixture inputs, but no numeric envelope
has been owner-selected in the source decisions. Do **not** substitute an assumed
LAN, clean-network, or internet profile and present the result as general
performance evidence. Each run must capture the following values:

| Required network field | Captured value |
| --- | --- |
| Direction and topology (loopback/LAN/invite remote) | _to capture_ |
| Baseline RTT and imposed RTT | _to capture_ |
| Jitter distribution and injection method | _to capture_ |
| Packet loss rate, burst model, and direction | _to capture_ |
| Usable per-client uplink and downlink bitrate | _to capture_ |
| Queue/backpressure policy and observed queue age/debt | _to capture_ |

These fields are **PROVISIONAL** until the owner accepts values in a later fixture
revision. Correctness tests may use deterministic, documented impairment cases;
they must report those cases and may not claim a universal latency, loss, or
bandwidth result.

## Durability and recovery fixture

| Case | Acceptance requirement |
| --- | --- |
| Explicit save/quit | Report success only after the documented server-owned durable barrier completes under its declared crash/storage contract. Queued, serialized, or OS-handed-off data alone is not a durable receipt. |
| Forced server termination and restart | Each declared atomic transaction recovers as the old valid state or the new valid state, never half-applied, silently regenerated, or substituted with default/air data. |
| Reconnect | A reconnect observes the committed authoritative state. |
| Ordinary autosave | Provisional recovery-point objective (RPO): at most 30 seconds of acknowledged ordinary progress may be lost after the declared crash event. This is not an explicit-save rollback allowance. |

The storage mechanism, exact crash model, and autosave implementation remain
benchmark/prototype decisions. A change to the stated durable barrier or RPO
requires a fixture-version change and owner acceptance; it is not solved by
weakening the meaning of "durable."

## Required machine and run metadata

The fields below are intentionally unfilled. Until they are captured, this document
defines a scope and correctness fixture—not a completed performance product profile.

| Category | Required fields |
| --- | --- |
| Client host | Manufacturer/model; CPU; GPU and VRAM; RAM; OS edition/version/build; GPU driver; display mode; renderer/backend; exact Godot 4.7.1 .NET build; exact .NET runtime; build commit/artifact hash; power/performance mode. |
| Server host | Manufacturer/model; CPU and core/thread allocation; RAM; OS edition/version/build; exact .NET runtime and GC mode; storage device/model, filesystem, free space, and durability-relevant configuration; network interface; measured uplink/downlink; build commit/artifact hash. |
| Run setup | Fixture ID/version; operating-system target; client/server topology; player and bot profiles; world/generator/content versions; section radius; network values above; warm-up and measurement duration; percentile/window definitions; diagnostic capture; pass/fail decision and owner approver. |

## Evidence and claim rules

- Every timing, FPS/frame-time, TPS, memory, queue, bandwidth, player-count, or
  throughput result MUST name the filled metadata and exact workload above.
- The fixture MUST NOT support universal claims such as “runs at 60 FPS,” “supports
  eight players,” “works on Windows/Linux,” or equivalent claims without the exact
  captured host, build, network, workload, duration, and failure policy.
- A clean-network or one-machine result cannot be generalized to all networks,
  hardware, worlds, drivers, or future Godot/.NET releases.
- The 8-player case is an acceptance workload; the 16-bot case is stress evidence;
  neither is a public service, server-capacity, or shipping promise.
- Private/invite-only exposure is not evidence for anonymous public hosting,
  discovery, NAT traversal, server identity, or DDoS resilience.
- Correctness, authority, serialization, coordinate, revision, and deterministic
  fault-injection work may proceed now using ephemeral fixtures.
- **No G1 performance benchmark or performance-related freeze may be accepted**
  until all applicable client and server host metadata is filled and the owner has
  accepted this fixture (or a superseding version). This prohibition does not block
  G1 correctness experiments or their evidence-driven, non-performance decisions.

## Acceptance record template

Record one entry per platform/workload/run combination:

| Field | Record |
| --- | --- |
| Fixture ID and build artifact | _to capture_ |
| Owner acceptance of fixture version | _to capture_ |
| Client/server host metadata | _to capture_ |
| Workload and network metadata | _to capture_ |
| Start/connect/move/stream/edit/reconnect result | _to capture_ |
| Explicit-save durable-barrier result | _to capture_ |
| Forced-termination recovery result | _to capture_ |
| Bounded-resource/diagnostic observations | _to capture_ |
| Result, exceptions, and owner disposition | _to capture_ |

Passing this fixture establishes only the recorded first-playable acceptance result
for the recorded environment. It does not create a shipping promise or freeze later
product, renderer, transport, storage, performance, or public-hosting policy.
