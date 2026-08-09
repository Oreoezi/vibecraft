# Greenlight checklist

Status: Awaiting owner decisions

This checklist compresses the research packet into decisions you can approve without treating every brief as an independent implementation mandate. “Greenlight” approves the contract or prototype direction; it does not claim an unrun benchmark passed.

## A. Product envelope

Recommended first research fixture:

| Choice | Recommended starting decision | Owner answer |
| --- | --- | --- |
| First playable | Two-client creative building/streaming/save/reconnect/recovery slice; no survival systems |  |
| Platforms | Windows x64 and Linux x64 first; one pinned Godot Forward+ release |  |
| Client target | Named minimum machine, 1080p, 60 Hz |  |
| Server target | Named CPU/RAM, .NET/GC mode, SSD/storage contract, and uplink |  |
| Capacity | 2 human clients for playability; 8 human-scale acceptance; 16-bot non-promised stress |  |
| Visibility | Start the fixture at 8 horizontal and 4 vertical full-detail 16-block sections; no far LoD |  |
| Network | Declare RTT/jitter/loss and usable bitrate envelope; private/invite-only exposure |  |
| Save promise | Explicit save/quit durable; provisional ordinary-autosave RPO of at most 30 seconds |  |

These are fixture inputs, not shipping promises. Changing one is fine; leaving it undefined makes most numeric brief criteria meaningless.

Before a renderer or capacity mechanism is greenlit, expand the accepted client
fixture into named low/reference/high profiles and one dedicated-server workload.
Every threshold must record hardware, renderer/runtime build, scene or bot workload,
warm-up, percentile, duration, and overload response.

## B. Greenlight the architectural contracts now

| ID | Contract | Recommended disposition | Key briefs |
| --- | --- | --- | --- |
| C1 | Server accepts intent and owns every durable outcome; narrow owner prediction | Greenlight | `ARCH-01`, `NET-01`, `NET-02` |
| C2 | One 20 Hz authoritative `WorldTick`; packet/render rates do not create another clock | Greenlight for v1 | `WORLD-08`, `NET-06` |
| C3 | Godot-free core; Godot presentation boundary; no block-per-node model | Greenlight | `ARCH-03`, `RENDER-01/02` |
| C4 | One live-world writer; workers publish immutable revisioned bounded proposals | Greenlight | `WORLD-02`, `WORLD-05`, `WORLD-08` |
| C5 | Sparse finite 3D section identity; signed coordinates; explicit operational borders | Greenlight contract, benchmark section side | `WORLD-01` |
| C6 | Stable namespaced IDs distinct from world-local/session numeric IDs; unknown content is not air | Greenlight | `GAME-01`, `WORLD-09` |
| C7 | Durable save receipt has an explicit crash contract; corrupt/newer data fails closed | Greenlight | `WORLD-03/04/05/09` |
| C8 | Separate version domains for protocol, gameplay, saves, generators, packs, and mods | Greenlight | `NET-07`, `WORLD-09`, `ASSET-02`, `MOD-03` |
| C9 | `.vcpak` is bounded resource-only data; Minecraft support is offline conversion | Greenlight boundary | `ASSET-01/02`, `NET-09` |
| C10 | Native .NET is trusted; enforceable permissions require a real sandbox/capabilities | Greenlight trust split | `MOD-01/02`, `ARCH-05` |
| C11 | Abuse resistance means bounded work/admission plus upstream DDoS responsibility | Greenlight boundary | `NET-08` |

## C. Greenlight experiments, not mechanisms

| ID | Experiment | Leading candidate versus fallback | Decision produced |
| --- | --- | --- | --- |
| E1 | Core data | 16³ vs 32³ sections; adaptive palette vs simpler storage | Freeze section side/indexing/IDs before user worlds |
| E2 | Persistence | SQLite WAL/FULL vs one measured fallback if it fails | Freeze v1 store/durability/envelope |
| E3 | Movement clock | 20 Hz prediction; exactly nested 40 Hz only after a recorded 20 Hz failure | Freeze controller/rules/correction timeline |
| E4 | Transport/trust | Pinned GNS first; one focused alternative only if it fails | Select transport, ownership, trust, admission, backpressure |
| E5 | Local host | Supervised child-loopback vs embedded adapter | Select packaged desktop default without changing `ServerCore` |
| E6 | Renderer | Hidden-face baseline vs greedy; nodes/ArrayMesh vs low-level server path | Select simplest backend meeting the fixture |
| E7 | Resource pack | Minimal `.vcpak`, strict ZIP/VFS, logical digest, one base pack | Freeze only exercised resource identity/path contracts |
| E8 | Wasm sandbox | Wasmtime host/toolchain/compile/cache/capability hostile spike | Greenlight Wasm, choose another isolation, or defer mods |

Every experiment ends in `greenlight`, `revise`, `defer`, or `reject`, and failed measurements remain recorded.

## D. Defer by default

- Survival breadth until the integrated first playable passes G5.
- Combat rewind, support-loss grace, speculative collision, and whole-world rollback.
- General pack dependency/override ecosystem, Minecraft conversion, animated GLB rigs, and hot reload beyond the first-party need.
- Sandboxed/public mods, trusted native loading, and ABI 1.0 until sandbox tests and two first-party dogfood features survive refactoring.
- Structures, multiple generator epochs, seam adapters, broad biomes/caves, and redstone until the basic durable world is healthy.
- Far-terrain LoD, shader light pages, GI, volumetrics, OIT, advanced reflection/refraction, and procedural assets.
- Parallel live-region simulation, alternative production databases, anonymous public servers, relays/account services, and 32/64/128 Hz world profiles.

## E. Reject explicitly

- Client-authored authoritative movement/world/inventory outcomes.
- Custom improvised UDP as the silent fallback.
- Native .NET “sandboxing” via `AssemblyLoadContext` or permission labels.
- Executable code, Godot scenes/resources, PCK mounting, or arbitrary shaders inside `.vcpak`.
- Content hashes as proof of hostile-client possession/execution or as anti-cheat.
- Literal infinite coordinates/work/render distance or unbounded vertical columns.
- Persistent 64×64×64 light samples per block.
- DDoS-proof or “cheating almost impossible” product claims.
- Bug-compatible Minecraft redstone/update accidents as a default requirement.

## Suggested owner response

```text
Product envelope: [accept defaults / changes]
Contracts C1–C11: [greenlight / exceptions]
Experiments E1–E8: [greenlight / changes]
Deferrals: [accept / promoted items with reason]
Rejected claims: [accept / exceptions]
```

The implementation gate sequence and exact exit criteria are in [`REVIEW-product-scope-and-sequencing.md`](REVIEW-product-scope-and-sequencing.md).
