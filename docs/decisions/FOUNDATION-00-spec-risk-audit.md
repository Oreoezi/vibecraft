# FOUNDATION-00 Spec risk audit

Status: Proposed

This is a cross-cutting audit, not a binding architecture decision. Its purpose is to identify statements in `design_doc.md` that should be converted into measurable requirements or split into staged goals before implementation.

## Executive recommendation

Keep the product direction—Godot/C#, an authoritative standalone server, Minecraft-like survival, moddability—but stop treating the proposed implementation mechanisms as already decided. In particular, UDP, Protobuf for every message, 32/64/128 Hz simulation, “DDoS safe,” per-1/64-block lighting, unlimited height, and safe native mods are hypotheses or ambiguous goals, not requirements.

The owner has greenlit the first implementation target as a multiplayer vertical
slice: start/load a world, connect, move, stream terrain, place and break blocks,
save, crash/recover, and reconnect. Architectures for the Nether/End, broad mob sets,
advanced materials, full redstone, procedural assets, and untrusted mods must not be
allowed to block that slice.

## Findings

### 1. Separate product requirements from proposed mechanisms

| Current statement | Actual requirement | Decision or experiment |
| --- | --- | --- |
| UDP | Low-latency state plus dependable control/content transfer | Compare mature reliable-UDP, QUIC streams/datagrams, and a minimal custom protocol |
| Protobuf | Evolvable typed messages | Use where suitable; benchmark size/CPU against packed state snapshots |
| 32/64/128 tick | Responsive play under load | Owner-selected fixed 60 TPS world authority; measure capacity and choose packet/slower-system cadences independently |
| No max height | A very tall world without dense empty columns | Sparse signed vertical sections; initial dimension policy is approximately 10,000 buildable blocks tall |
| DDoS safe | Resist cheap amplification and resource exhaustion | Threat model, admission limits, stateless validation, observability, upstream mitigation |
| Native mods with scoped permissions | Useful untrusted extensions with explicit capabilities | A sandboxed runtime or process boundary; native .NET is trusted-only |
| 1/64-block lighting | High-frequency visual shading on 64×64 assets | GPU per-fragment shading; do not store a 64³ world-light lattice per block |

### 2. The server tick target is now an owner-selected product constraint

At 32, 64, and 128 Hz, one tick has approximately 31.25 ms, 15.63 ms, and 7.81 ms respectively. Those are end-to-end deadlines for every synchronous activity assigned to the tick. A voxel sandbox also has chunk activation, block updates, entities, inventories, plugins, and persistence coordination, so a single global high-frequency tick would spend CPU on systems that do not benefit from it.

Owner decision: use `WORLD-08`'s single fixed **60 TPS** `WorldTick` as the v1
authority clock, with sequenced input, local prediction, and interpolation at render
rate. Input and snapshot transmission may be paced independently, and slower systems
use deadlines/divisors without creating another authority clock. Do not expose
user-selectable world-tick profiles. The capacity prototype must prove the declared
workload can sustain the 16.67 ms nominal cadence; failure reduces scope or revises
the architecture rather than silently slowing/changing gameplay time.

The networking research should compare at least these transport strategies:

- a mature reliable-UDP library with multiple channels;
- QUIC reliable streams plus unreliable DATAGRAM frames;
- a small game-specific UDP protocol only after the required semantics are written down.

QUIC DATAGRAM supplies encrypted unreliable messages sharing a connection with reliable streams and congestion control, but datagrams are not fragmented and still require application flow identifiers and overload behavior ([RFC 9221](https://www.rfc-editor.org/rfc/rfc9221.html)). This makes it a real candidate, not an automatic choice.

### 3. Protobuf is a schema tool, not the whole protocol design

Protobuf supports additive binary evolution and retention of unknown fields, while changing field numbers is unsafe and removed numbers should be reserved ([official Proto3 guide](https://protobuf.dev/programming-guides/proto3/)). It does not decide:

- packet framing and maximum datagram size;
- reliable, ordered, sequenced, or superseding delivery;
- snapshot baselines and delta compression;
- congestion/backpressure;
- authentication and replay protection;
- compatibility negotiation;
- compact representation of hot arrays such as block palettes or transforms.

Recommendation: use Protobuf initially for handshake, capability, inventory, commands, and other low-frequency structured messages. Benchmark it before using it for high-frequency entity snapshots or large chunk payloads; permit opaque packed payloads inside a versioned envelope.

### 4. “Unlimited height” needs a finite contract

Computers, coordinate fields, physics engines, floating-point rendering, databases, and save keys all have limits. The useful requirement is that world storage be sparse in the vertical dimension and not encode a small fixed stack of sections.

Recommendation: use integer chunk coordinates `(x, y, z)` and fixed-size cubic
sections; only allocated sections consume meaningful memory or disk. The initial
dimension policy exposes an explicit build range approximately 10,000 blocks tall,
while the save key remains sparse/signed and can survive a later policy expansion.
Keep rendering coordinates local to an origin. Luanti's engine documentation provides
a concrete comparison point: its map is built from 16×16×16 MapBlocks and supports
selectable map database backends ([basic structures](https://docs.luanti.org/for-engine-devs/basic-data-structures/), [database backends](https://docs.luanti.org/for-server-hosts/database-backends/)). Minecraft's Anvil transition is a warning against baking height into monolithic chunk layouts: it introduced vertical sections and omitted empty sections from memory/disk ([Anvil format summary](https://minecraft.wiki/w/Anvil_file_format)).

### 5. The lighting statement has two radically different interpretations

If “1/64 of a block” means normal per-fragment shading of a 64×64 texture, the GPU already evaluates lighting at visible pixels and no persistent sub-block light field is required. If it means a volumetric 64×64×64 light lattice for each block, one 16³ section contains over one billion samples; even one byte per sample is about 1 GiB before metadata. That is not a viable primary world-light representation.

Recommendation: store low-resolution gameplay/world light, then shade visible surfaces per fragment using material textures, normals, ambient occlusion, shadows, probes, or screen-space effects. Prototype visual quality and frame cost before selecting GI/reflection/refraction techniques. Advanced transparent materials must be designed around batching and sorting, not specified only as asset features.

Godot itself is only partially thread-safe; its documentation recommends servers APIs for threaded work and warns that GPU/resource operations can stall on synchronization ([thread-safe APIs](https://docs.godotengine.org/en/stable/tutorials/performance/thread_safe_apis.html)). The C# boundary also incurs native interop and array/string marshalling costs ([Godot C# basics](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html)). Keep high-volume voxel storage, lighting, meshing, and job data in plain managed/native buffers, with narrow main-thread/Godot publication steps.

### 6. Scoped native C# mods cannot protect the process from malicious code

Loading assemblies into a separate `AssemblyLoadContext` helps dependency loading and unloading, but Microsoft's API documentation explicitly says it provides no security features and loaded code retains full process permissions ([AssemblyLoadContext](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext)). Therefore an arbitrary native client mod can read files, inspect memory, open sockets, or terminate the process regardless of a friendly permission manifest.

Recommendation:

- classify in-process native C# plugins as trusted;
- use a deliberately constrained scripting or WebAssembly capability API for untrusted mods, or isolate them in another OS process;
- treat permission declarations as enforceable runtime capabilities, not labels;
- keep client mod agreement separate from anti-cheat: matching hashes prove sameness, not safety or honesty;
- never let client mod agreement become authority over server state.

### 7. Asset compatibility should be implemented by conversion

Minecraft-compatible path and naming conventions are useful for author familiarity, but VibeCraft materials, animation, models, procedural generation, and permissions have different semantics. Native “semi-compatibility” risks freezing the engine around another game's evolving formats.

Recommendation: define a small namespaced VibeCraft pack contract with explicit format/capability versions, deterministic override order, integrity hashes, and import-time validation. Implement Minecraft support as a version-specific converter producing that native representation. Preserve provenance and emit a conversion report for unsupported constructs.

### 8. The gameplay list is not a first milestone

Three dimensions, terrain generation, structures, weather, caves, crafting, equipment, food, regeneration, multiple mob categories, redstone, multiplayer, modding, and an advanced renderer are several interacting products. “Similar to Minecraft 1.0” is also insufficient as a behavioral specification because mechanics and bugs varied by version and platform.

Recommendation: define compatibility inspiration rather than bug-for-bug compatibility. Build milestones as playable loops:

1. deterministic local/server world and crash-safe save;
2. one multiplayer player controller plus block interaction;
3. inventory, drops, crafting, and one tool progression;
4. one hostile and one passive mob;
5. day/night, health, food, and respawn;
6. minimal circuits after block-update semantics are measured;
7. additional dimensions and broad content after the extension contracts settle.

## Cross-system invariants to greenlight early

- The server owns durable gameplay state; client prediction is provisional and reconcilable.
- Singleplayer and multiplayer share simulation code, protocol semantics, and conformance traces. An embedded test/fallback host may avoid wire encoding behind the same typed message interfaces, but shipping topology remains prototype-gated and no gameplay behavior may depend on the shortcut.
- Simulation never mutates Godot scene objects or renderer resources directly.
- Chunk/section data is immutable or exclusively owned while worker jobs read it; publication is versioned.
- Save acknowledgement means a defined durability level, not merely that data entered a queue.
- Registry names are stable persistent identifiers; compact numeric IDs are negotiated or saved through mappings.
- Unknown network, pack, save, and mod data fails according to an explicit compatibility policy.
- Untrusted code never runs as unrestricted native code inside the client or server process.

## Immediate experiments

| Experiment | Pass condition |
| --- | --- |
| Network impairment harness | Reproducible latency, jitter, loss, duplication, and reordering; no unbounded queues or state corruption |
| Movement/block reconciliation slice | No hard correction for small error; deterministic recovery from rejected edits and changed collision terrain |
| Section pipeline | Generation → lighting → meshing → upload is cancelable, versioned, and stays within an explicit frame budget |
| Save crash matrix | Kill at every write stage; recovery yields old or new valid state, never an unreadable world |
| Lighting comparison scene | Objective GPU/frame-memory measurements plus blind screenshots for visual tradeoffs |
| Mod escape tests | Denied filesystem/network/process actions are actually impossible, not merely undocumented |

## Open product decisions

- Supported platforms and minimum hardware determine rendering, runtime, and sandbox options.
- Target player count and view distance determine networking and world budgets.
- Whether servers distribute packs/mods or only verify them determines onboarding and trust design.
- Whether singleplayer must pause determines whether the local server is an external process or an in-process host with a pause contract.
- Whether old Minecraft behavior is inspiration or compatibility determines how much historical behavior research is useful.
