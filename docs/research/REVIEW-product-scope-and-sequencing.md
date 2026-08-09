# Product scope and architecture sequencing review

Status: Review complete — the research packet is useful, but it is not an implementation checklist  
Date reviewed: 2026-08-09  
Role: skeptical technical producer and architecture sequencing review

> Snapshot note: the sequencing and gate recommendations remain current. The
> contradiction list near the end records the first-wave snapshot and is retained as
> review evidence; accepted corrections are tracked in
> [`INTEGRATION-RESOLUTIONS.md`](INTEGRATION-RESOLUTIONS.md).

## Verdict

VibeCraft should not implement “Minecraft 1.0 plus better networking, renderer, persistence, assets, and mods” as one milestone. That is several products sharing a voxel world.

The first playable should prove one much smaller claim:

> Two clients can enter one server-owned voxel world, move responsively, receive nearby terrain, place and break a few blocks, explicitly save, survive a forced server restart, reconnect, and observe the same authoritative result.

Singleplayer must exercise the same `ServerCore`, rules, command handlers, and persistence path, but the child-process-versus-embedded host choice may remain open until it is measured. The first playable is a building and recovery slice, not yet a survival game.

The packet already contains most of the right long-term boundaries. Its main sequencing failure is that proposed production mechanisms, research fixtures, and eventual product aspirations are often written at the same level of urgency. The result is a dependency graph in which a block-placement prototype appears to require a public mod ABI, secure Wasm runtime, final pack resolver, advanced lighting, public-server trust, multiple generator epochs, and a mature backup product. It does not.

The current set is therefore **not ready to greenlight as a unit**. Greenlight the invariants below, test the mechanisms in dependency order, and leave the rest explicitly unimplemented.

This review agrees with the central clock, transport, trust, and host-topology corrections in [the adversarial architecture/networking review](REVIEW-architecture-networking.md). In particular, use one provisional 20 Hz world clock for the first experiment, treat GNS and a child server as candidates, keep support-loss grace off, and do not mistake content hashes for attestation.

## What belongs in the first playable

### Required player-visible loop

- One original Overworld-like test dimension.
- One deterministic flat or very simple rolling-terrain generator with a finite natural Y band.
- A tiny fixed content set: air, one ground block, two or three build blocks, and one visible missing-content block.
- One local player and one remote player or bot, both controlled through the same authoritative command path.
- Walk, look, jump, collide, place, break, disconnect, reconnect, save, stop, and recover.
- Nearby full-detail terrain only, with a finite radius and ordinary distance fog.
- One first-party visual pack using namespaced assets and one shared terrain material path.
- Explicit save progress and an unambiguous durable-save acknowledgement.
- Diagnostics for tick time, network queue age, section lifecycle, mesh queue, dirty revisions, save health, and memory.

An infinite/debug block palette is acceptable in this slice. Inventory conservation, drops, tools, crafting, hunger, and combat are deliberately absent; otherwise the slice becomes a survival milestone and stops isolating the architecture.

### Required engineering proof

- A Godot-free authoritative core and shared voxel movement kernel.
- One world writer with immutable/revisioned worker inputs and results.
- Sparse three-dimensional section addressing, negative-coordinate correctness, and a floating client render origin.
- Bounded materialization, network, render, and persistence queues.
- Current-time, revision-checked, idempotent block actions.
- Crash-safe old-or-new persistence for the records touched by the slice.
- A deterministic impairment and fault-injection harness that can replay failures.
- Packaged client and server smoke tests on the declared first-slice platforms.

### Explicit non-goals

- Inventory, crafting, tools, equipment, health, food, death, mobs, weather, portals, or multiple dimensions.
- Redstone, pistons, fluids, random ticks, broad block-entity behavior, or offline simulation.
- Combat rewind, projectile compensation, per-player phantom support, or predicted collision edits.
- Far-terrain LoD, volumetric fog, light pages, GI, SSR, OIT, refraction, or procedural materials.
- General resource-pack dependencies/overrides, Minecraft conversion, GLB rigs, animation graphs, or hot reload.
- Sandboxed mods, trusted native plugin loading, plugin persistence, a public ABI, or a marketplace.
- Anonymous public-server exposure, account services, server browsing, relays, or a claim of DDoS protection.
- Generator upgrades, mixed generator epochs, seam adapters, structures, biomes, caves, or worldgen plugins.
- PostgreSQL/RocksDB backends, region simulation, or multiple authoritative tick profiles.

If one of those appears in a first-playable pull request, it needs a written explanation of which first-playable exit criterion it is necessary to satisfy.

## Decision disposition

### Greenlight-now contracts

These are architectural constraints worth adopting before implementation because violating them creates predictable rework or unsafe behavior. Greenlighting the contract does not greenlight every mechanism proposed in its brief.

| Contract | Why it should be fixed now | Primary briefs |
| --- | --- | --- |
| Server accepts intent and owns durable outcomes | Retrofitting authority after clients can write positions, inventory, or blocks is expensive and insecure | [ARCH-01](../decisions/ARCH-01-authority-and-simulation.md), [NET-01](../decisions/NET-01-network-simulation-model.md) |
| Godot is a presentation adapter, not simulation state | Keeps dedicated server, tests, prediction, and tools engine-independent | [ARCH-03](../decisions/ARCH-03-godot-client-boundary.md) |
| One live-world writer; workers return immutable revisioned proposals | Permits useful parallel work without making completion order authoritative | [WORLD-02](../decisions/WORLD-02-chunk-job-scheduling.md), [WORLD-08](../decisions/WORLD-08-ticking-and-activation.md) |
| Sparse 3D section identity; columns are derived views | Avoids a hidden fixed-height allocation model | [WORLD-01](../decisions/WORLD-01-chunk-coordinate-and-memory-model.md) |
| Stable namespaced persistent IDs are distinct from compact runtime/session IDs | Prevents pack order and missing content from reinterpreting saves | [GAME-01](../decisions/GAME-01-content-registries.md) |
| Every asynchronous product has key, revision, lifetime/epoch, ownership, and a bounded queue | Prevents stale publication, use-after-unload, and burst-driven memory growth | [WORLD-05](../decisions/WORLD-05-chunk-lifecycle.md), [RENDER-02](../decisions/RENDER-02-mesh-job-pipeline.md) |
| Save acknowledgement means a defined durable barrier | “Queued” and “saved” must never be synonyms | [WORLD-03](../decisions/WORLD-03-world-storage-layout.md), [WORLD-04](../decisions/WORLD-04-crash-safe-persistence.md) |
| Network, save, pack, generator, gameplay, and mod versions are separate domains | One marketing/build number cannot safely evolve all durable contracts | [NET-07](../decisions/NET-07-protocol-versioning.md), [WORLD-09](../decisions/WORLD-09-world-format-migration.md) |
| Resource packs are bounded data; Minecraft support is conversion | Prevents foreign formats and arbitrary Godot resources becoming the runtime ABI | [ASSET-01](../decisions/ASSET-01-packaging-and-namespaces.md), [ASSET-02](../decisions/ASSET-02-manifest-and-overrides.md) |
| Native .NET extensions are fully trusted; enforceable permissions require a real sandbox | Avoids shipping a false security claim | [MOD-01](../decisions/MOD-01-client-mod-runtime.md), [MOD-02](../decisions/MOD-02-capability-security.md) |
| Application abuse resistance is bounded work, not “DDoS proof” | Volumetric protection belongs upstream | [NET-08](../decisions/NET-08-server-abuse-and-ddos-boundary.md) |

### Prototype-gated mechanisms

These are credible leading candidates, not decisions to freeze from prose alone.

| Mechanism | Prototype question | Greenlight output |
| --- | --- | --- |
| 16³ authoritative sections | Does 16³ beat 32³ for memory, edits, saves, meshing, and section count on the target fixture? | Freeze `SectionSide` before any user world is created |
| Adaptive `Uniform / Paletted / Direct` block storage | Does the representation save material memory without unacceptable hot-read/edit cost? | Select internal representation; keep public snapshot contract |
| One provisional 20 Hz `WorldTick` | Does predicted movement feel and converge acceptably under the declared network envelope? | Keep 20 Hz, or test exactly one nested 40 Hz movement branch if 20 Hz demonstrably fails |
| SQLite WAL with `synchronous=FULL` | Do durable commit, read, WAL, crash, and storage-failure tests pass on target disks? | Select SQLite v1 or compare one measured fallback |
| Custom shared voxel controller | Can client/server traces converge across target platforms and mutable collision? | Freeze movement rules version and correction contract |
| GNS direct-IP/loopback transport | Can the pinned native build package safely, expose buffer ownership, isolate traffic, authenticate a public server, and bound admission? | Greenlight or reject GNS; never silently fall back to custom UDP |
| Child-process singleplayer | Is its crash isolation worth startup, memory, packaging, signing, and supervision cost versus an embedded host adapter? | Select desktop default without changing `ServerCore` semantics |
| Hidden-face versus greedy meshing | Does greedy reduction improve the real Godot frame/upload workload enough to justify its merge rules? | Hidden-face baseline is always valid; greedy is an internal optimization |
| `ArrayMesh`/nodes versus low-level rendering-server instances | Where is the measured object/upload/disposal bottleneck? | Select the simplest backend meeting the declared radius/frame target |
| Minimal `.vcpak` ZIP, strict paths, and logical digest | Can one base pack load reproducibly and safely within startup/memory limits? | Freeze only the minimal pack-identity and path contract used by the slice |

The mechanism is allowed to fail. A failed experiment should select a smaller fallback, not pull an even more ambitious system into scope.

### After-first-playable work, not first-playable dependencies

These are reasonable next product increments after the integrated building slice is healthy:

1. Inventory, hotbar, drops, crafting, one tool progression, health, death, and respawn.
2. One hostile and one passive creature, simple AI, day/night, and sound/animation sufficient for that loop.
3. Original biomes, caves, a small structure vocabulary, weather, furnace-like processing, and generator-version fixtures.
4. A deliberately small deterministic circuit milestone after general block-update persistence is proven.
5. A general pack resolver, Minecraft visual converter, animated entity assets, and stronger content-agreement UX after the first-party pipeline has real needs.
6. Sandboxed extension dogfooding only after two first-party mechanics can use a narrow host API without exposing internals.

### Post-v1 aspirations unless the owner explicitly changes the release definition

- Far 3D voxel mip LoD and extreme render distance.
- Shader-sampled light pages, colored propagation, GI, volumetric fog, OIT, advanced reflection/refraction, and high-resolution material tiers.
- Procedural asset graphs or runtime-generated textures.
- General client/server Wasm ecosystems, public stable WIT ABI, trusted native plugin distribution, or a marketplace.
- Multiple generator epochs with pair-specific seam adapters and retained historical generator packages.
- Nether-like and End-like dimensions, bosses, broad mob rosters, or broad Minecraft-1.0-era survival completeness.
- Folia-style parallel simulation regions, PostgreSQL/RocksDB production backends, browser/console networking, relays, or remote attestation.

They may receive research notes now, but they should not receive production interfaces, schemas, schedulers, or cache formats before the simpler product supplies a measured requirement.

## Expensive lock-in versus reversible choices

### Freeze only after its gate passes

| Choice | Why it locks in | Required gate |
| --- | --- | --- |
| Section edge, coordinate division, key fields, and local index order | Appears in saves, network payloads, generation, light, meshes, and tools | Core data gate |
| Canonical block-state identity and world-local ID mapping | Every persisted voxel and missing-content recovery path depends on it | Core data gate |
| Authoritative clock ownership and tick phase ordering | Controls replay, action causality, scheduled work, plugins, and save observation | Authority gate |
| Movement rules and collision-shape semantics | Client prediction, anti-cheat, traces, and gameplay tuning depend on it | Movement gate |
| World record envelope, revision semantics, and migration policy | Existing worlds make mistakes expensive to correct | Persistence gate |
| Application message semantics, sequence epochs, action IDs, and field numbers | Old clients/servers and recorded traces become compatibility obligations | Network gate |
| Asset/package names, override meaning, and capability identifiers | Packs become public authored content | First-party pack dogfood gate |
| Generator fingerprint, RNG, stage semantics, and provenance | Generated terrain is durable even if code changes | World-identity milestone, not first playable |
| Public extension ABI/event ordering | Third-party code freezes scheduling and ownership assumptions | Post-slice dogfood gate |

### Keep reversible and private

Worker counts, queue sizes, cache grace, view distance defaults, autosave cadence, compression level, mesher algorithm, renderer backend, texture-bank size, fog tuning, GNS lane mapping, child versus embedded host, and storage backend are implementation/configuration choices. Do not expose them as permanent pack, plugin, save, or protocol promises before measurements require it.

## Product targets missing from the research packet

Correctness criteria such as “no stale revision commits” or “no half transaction” are meaningful now. Most absolute p95/p99, MiB, player-count, radius, and millisecond targets are not. They currently describe synthetic research loads without a product envelope.

| Missing target | Numeric criteria made meaningless without it |
| --- | --- |
| First supported OS/architecture and Godot renderer | GNS/Wasmtime packaging, child helpers, Forward+ features, render timing, cross-platform determinism |
| Named minimum client CPU/GPU/RAM, resolution, and frame target | Meshing, uploads, light/material budgets, fog, animation, pack limits, LoD |
| Named server CPU/RAM/GC mode/storage and uplink | Tick, worldgen, SQLite, plugin, interest, transport, and player-capacity thresholds |
| First-playable human player target and stress-bot target | Snapshot bandwidth, entity work, section residency, abuse limits, history memory |
| Horizontal and vertical full-detail radius plus simulation radius | Resident section count, mesh count, section payload traffic, lifecycle memory |
| Expected movement/flight/teleport speed | Prefetch, fog reaction, mesh latency, collision-envelope size |
| Network envelope: RTT, jitter, loss, and usable per-client bitrate | Prediction, snapshot cadence, baseline settling, lane/backpressure tests |
| World-size and save promise: expected active data, storage class, crash rollback/RPO | Commit cadence, WAL cap, backup duration, queue memory, startup checks |
| Public exposure model: private/LAN/invite/public anonymous | Server identity, account authentication, pre-auth admission, upstream mitigation |
| Expected first-party block/material/entity/animation counts | Registry, pack, texture-bank, model, and animation thresholds |

Before converting a proposed number into a release gate, record the target machine/workload beside it. Otherwise “passes p99” is not reproducible product evidence.

## Dependency-aware gate sequence

```text
G0 Product envelope
  -> G1 Core data and irreversible-format spike
       -> G2 Durable headless world ---------+
       -> G3 Authority and movement ----------+-> G5 Integrated first playable
              -> G4A Transport and interest --+
       -> G4B Godot client and base visuals --+
```

G2, G3, and G4B may overlap after G1 has frozen their shared value types. G4A starts after G3 defines application semantics; transport must not define gameplay causality. G5 starts only when every incoming branch has a written disposition.

### G0 — Product envelope

Write one acceptance-fixture sheet, not another architecture brief. It names platforms, machines, frame target, players, full-detail radius, network envelope, exposure model, and save promise.

Exit criteria:

- The owner accepts the first-playable loop and explicit exclusions in this review.
- Every performance test can name one client machine, one server machine, one workload, and one build configuration.
- First-playable completion can be answered yes/no without referring to “Minecraft-like.”

No implementation performance claim is greenlit before G0.

### G1 — Core data and irreversible-format spike

Build one Godot-free test/benchmark project containing coordinate types, floor division, 16³/32³ candidate sections, block-state containers, a tiny namespaced registry, stable/world/runtime ID mappings, revision types, and deterministic logical serialization. Use ephemeral fixtures only.

Exit criteria:

- Negative, large, and checked-overflow coordinate property tests pass.
- 16³ or 32³ is selected from measured memory/edit/snapshot/remesh amplification, not familiarity.
- No ordinary block is an object; hot reads allocate nothing.
- Stable names and world IDs round-trip independently of discovery/registration order.
- Unknown state data remains distinguishable from air.
- Logical serialization and canonical hashes are deterministic across the declared platforms.
- One `WorldTick` owner and one action/publication phase vocabulary are selected; 20 Hz is the first measured cadence, not a menu of profiles.

Freeze after pass: section key/side/indexing, persistent ID distinction, revision representation, and canonical record-key encoding.

### G2 — Durable headless world

Build a tiny `ServerCore` with a simple target-section generator, authoritative place/break, the G1 section model, SQLite candidate storage, revisioned save intents, one writer, and a parent process that kills it at persistence hooks. Do not add mobs, inventories, plugins, structures, or general generator epochs.

Exit criteria:

- Start, generate/load, mutate, durable-save, close, and reopen work without Godot.
- A dirty-during-save edit remains dirty after the older receipt.
- Every declared transaction recovers as old or new, never mixed.
- A durable receipt survives the supported crash model; an unacknowledged operation may disappear but never duplicate or half-apply.
- Disk-full, read-only, corrupt length/checksum, unsupported version, and writer stall fail closed within hard queue/memory limits.
- A killed world is never silently regenerated over persisted or modified data.
- The loaded native SQLite version and durability mode are observable.

Freeze after pass: v1 logical record envelope, durability vocabulary, save barrier, and minimal migration behavior. Online backup products, million-row scale claims, and alternate backends remain later work.

### G3 — Authority and movement over a test transport

Use an in-memory message transport plus deterministic impairment. Implement the shared voxel controller, one authoritative world clock, sequenced redundant input, owner acknowledgements, remote interpolation, current-time block actions with `ActionId` and expected revision, and bounded reconciliation. Confirmed collision is the baseline.

Exit criteria:

- No client message assigns authoritative position, velocity, grounded state, inventory, health, or block result.
- Legal traces converge under the G0 RTT/jitter/loss matrix with bounded history and no queue growth.
- Illegal rate, timer, NaN, future sequence, duplicate action, and invented-state inputs create no illegal authoritative state.
- Movement/block ordering is deterministic when support or path blocks change.
- Every correction has a replayable reason and source input/world revision.
- A blind feel/correction comparison accepts 20 Hz. Only a measured failure permits one nested 40 Hz controller experiment; it does not permit 32/64/128 whole-world profiles.
- Support-loss grace, historical combat rewind, and speculative collision remain disabled.

Freeze after pass: authority matrix, action causality, movement-rules version, missing-input policy, and reconciliation semantics. Do not freeze Protobuf field numbers or a transport implementation yet.

### G4A — Transport, trust, and interest

Bind the G3 message catalog to the first candidate transport. Use synthetic measured section payloads before integrating full terrain. Exercise realtime, control, and bulk traffic classes, bounded producer queues, baseline epochs/revisions, two clients, malformed admission, and the declared exposure model.

Exit criteria:

- Native receive/send ownership and all error states are represented safely in C#.
- Bulk transfer cannot create unbounded memory or unacceptable realtime/control queue age under the G0 link envelope.
- Cross-class correctness depends only on IDs/revisions/acknowledgements, never receive order.
- Actual compressed bytes and usable bitrate explain terrain settling time; no impossible fixed deadline is retained.
- Packaged client/server binaries load, connect, churn, and shut down on every G0 platform.
- For public exposure, a client authenticates a persistent server identity, admission work/allocation/amplification is measured and bounded, and operator/upstream DDoS responsibilities are documented. If that gate fails, the first playable remains private/invite-only.
- GNS receives a written `greenlight`, `revise`, or `reject`. A rejection triggers one focused alternative, not custom UDP.

Freeze after pass: application envelope and negotiated protocol major/capabilities actually used by the slice. Lane numbers, fragment sizes, and rates remain measured configuration where possible.

### G4B — Godot client, streaming renderer, and base visuals

Implement the Godot adapter against G1 snapshots. Begin with hidden-face section meshes, whole-section rebuilds, a bounded revisioned worker/commit queue, one shared terrain shader/material bank, one minimal namespaced base pack, ambient/directional presentation light, and ordinary distance fog. Add greedy meshing only as the measured second implementation.

Exit criteria:

- Server/core projects compile and test without Godot.
- Workers touch no scene object or live Godot resource; stale meshes never commit.
- The selected full-detail radius holds the G0 frame target during movement, editing, unload, and render-origin rebasing.
- Mesh/upload/disposal queues and resident memory plateau during a soak and return to baseline after unload/reconnect.
- A local edit appears within the chosen visual-latency target without a synchronous frame spike.
- One section containing all first-slice materials uses a fixed small surface count independent of block material count.
- The base pack resolves reproducibly and cannot mount arbitrary Godot scenes/scripts or escape its path root.
- No far LoD, light pages, advanced transparency, GI, volumetrics, animated rigs, or procedural assets are needed to pass.

Freeze after pass: minimal block-render template, material ID, and base-pack identity contracts actually exercised. Keep renderer backend and mesher implementation replaceable.

### G5 — Integrated first playable and host selection

Integrate G2, G3, G4A, and G4B. Run the same `ServerCore` through a dedicated process and both local-host candidates if the platform permits. This is the first point at which “playable” may be claimed.

Exit criteria:

- A packaged client starts or connects to a server, enters one world, moves, streams terrain, places/breaks, sees another player, disconnects, and reconnects to the same result.
- Explicit save/quit reaches a durable barrier before reporting success.
- Forced client death, server death, control loss, and process restart produce the documented recovery outcome without orphaned locks/processes or false save success.
- Local and remote sessions execute the same authoritative movement/block handlers and produce equivalent protocol traces.
- A 30-minute G0 acceptance run and longer clean-network soak show no unbounded queue, handle, RID, native allocation, managed heap, or dirty-save growth.
- Every branch mechanism has a written disposition and failed evidence remains recorded.
- The child or embedded singleplayer host is selected from startup, steady memory, crash containment, packaging, pause, orphan, and save behavior—not from architectural taste.

Completion of G5 authorizes the first survival milestone. It does not authorize redstone, public mods, extreme rendering, or multiple dimensions.

## Compact owner decision card

These are the choices that make the proposed numeric gates meaningful. Recommended defaults are supplied so research can proceed without another broad design exercise.

1. **Slice definition:** approve the two-client creative building/save/reconnect slice above. Do not include survival systems.
2. **Platforms:** target Windows x64 and Linux x64 first with one pinned Godot Forward+ version; defer macOS, ARM, mobile, web, and console until packaging evidence exists.
3. **Performance fixture:** name the slowest supported client machine and target 1080p/60 Hz; name one server machine/GC/storage configuration. Use real named machines, not “mid-range.”
4. **Capacity fixture:** require two human clients for playability, eight human-scale clients for first acceptance, and sixteen bots as non-promised stress data.
5. **Visibility fixture:** begin at eight horizontal and four vertical full-detail 16-block sections, with no far LoD. Treat these as test inputs, not a shipping promise.
6. **Network exposure:** keep the first playable private/invite-only. Anonymous public direct-IP exposure waits for authenticated server identity and admission testing.
7. **Save promise:** explicit save/quit is durable; choose and publish the maximum crash rollback for ordinary autosave. A provisional 30-second RPO is reasonable until measured.
8. **Scope policy:** no executable mods/plugins, Minecraft conversion, redstone, advanced renderer, or additional dimensions before G5. Singleplayer may pause; “Open to LAN” is a separate later host mode.

If the owner rejects a recommended default, update the fixture and affected gates before interpreting benchmark results. Do not leave two incompatible targets active in different briefs.

## Scope-creep and architecture-astronaut audit

The following research is useful as future-risk mapping, but implementing it before G5 would be architecture-astronaut work:

- The complete Wasm Component Model/WIT capability system, per-callback quotas, client and server sandbox hosts, trusted native loaders, plugin migrations, and ABI stability program in `MOD-01`, `MOD-02`, `MOD-03`, and `ARCH-05`.
- The full dependency/override resolver, Java and Bedrock converters, GLB model/rig/animation pipeline, animation graph, and procedural texture graph in `ASSET-01` through `ASSET-05`.
- Shader light-page atlases, PBR texture-bank maximums, selected point lights, SSR/SSIL/SDFGI, water refraction, OIT, volumetric fog, and a 2,048-block 3D LoD pyramid in `RENDER-03` through `RENDER-07`.
- Generator tiles, epoch upgrades, pair-specific seam adapters, retained historical generator packages, and worldgen plugin contracts in [WORLD-06](../decisions/WORLD-06-versioned-world-generation.md), plus the durable candidate/plan/index/receipt system, conflict planner, eager loot, and cross-epoch reservations in [WORLD-07](../decisions/WORLD-07-structure-generation.md). The first slice needs one pinned simple generator ID/hash, no structures, and no mixed epochs.
- General deterministic redstone networks, persisted cross-boundary inboxes, pistons, 65,536-node circuits, and million-update abuse fixtures in `GAME-02`.
- Multiple 32/64/128 Hz master profiles, parallel live-region ticking, alternate production databases, exact public backup products, remote attestation, or public DDoS claims.

Do preserve seams that cost almost nothing: Godot-free projects, a transport interface, namespaced IDs, revisioned records, one generator ID/version, and opaque extension-facing handles. Do not implement the future subsystem behind each seam.

## Contradictions that must not leak into implementation

- `WORLD-08` selects one 20 Hz world writer while `NET-01`, `NET-02`, `NET-03`, and `NET-06` describe 32/64/128 Hz master simulation. For G1–G5, `WORLD-08` owns one provisional 20 Hz clock; network rates are experiments subordinate to it.
- `ARCH-04` selects a child server while `WORLD-05` says singleplayer is in-process. Both must become host adapters around one `ServerCore` until G5 selects the default.
- `ARCH-01` embeds latency-sensitive actions in the input timeline while networking briefs also send them on reliable control. G3 must define one causal application timeline independent of cross-lane arrival.
- `NET-04`'s support lease and combat rewind are product rules, not consequences of server authority. Neither is a prerequisite for block placement.
- `NET-09` can establish local byte agreement for a cooperating client; it cannot prove possession or execution to a hostile client.
- `WORLD-02` and `WORLD-06` use different stage vocabularies and some publication examples order only X/Z even though the canonical key is 3D. The first generator should use one target-section stage; the general stage graph remains unfrozen.
- `ASSET-02` lists mod-API work as a dependency even though a resource-only base pack can be proven independently. The first pack gate uses no executable component.
- Several render, network, storage, and mod tests use 16/64 players, thousands of sections, or exact millisecond budgets without G0 hardware/workload targets. Treat those values as benchmark scenarios, not approval criteria.

## Producer stop rules

- No persistent user world is created before G1 selects the section/key/registry contract and G2 passes crash recovery.
- No public wire compatibility promise is made before G3 fixes semantics and G4A selects a transport/trust path.
- No public pack or extension ABI is labeled stable before first-party content survives a real internal refactor.
- A prototype that fails its bound is revised, deferred, or rejected; its queue/memory limit is not silently raised until it passes.
- A future feature cannot become a dependency merely because a brief mentions it under `Requires`. The dependency must be an invariant needed by the current gate, a minimal stub, or a production requirement explicitly scheduled later.
- First playable is complete only at G5. A headless benchmark, renderer demo, pack compiler, or transport soak alone is evidence, not a game milestone.

## Reviewed source packet

This sequencing review is based on the current [product spec](../../design_doc.md), [spec risk audit](../decisions/FOUNDATION-00-spec-risk-audit.md), all decision briefs present on the review date, the [gameplay reference scope](GAMEPLAY_REFERENCE_SCOPE.md), [implementation survey](IMPLEMENTATION_SURVEY.md), and [prototype program](PROTOTYPE_PROGRAM.md). The comparison research supports the boundaries above; it does not make another project's constants into VibeCraft requirements.
