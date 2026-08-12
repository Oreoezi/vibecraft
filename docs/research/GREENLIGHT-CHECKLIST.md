# Greenlight checklist

Status: Partially owner-reviewed through `questions2.md`

This checklist compresses the research packet into decisions you can approve without treating every brief as an independent implementation mandate. “Greenlight” approves the contract or prototype direction; it does not claim an unrun benchmark passed.

## A. Product envelope

Recommended first research fixture:

| Choice | Recommended starting decision | Owner answer |
| --- | --- | --- |
| First playable | Two-client creative building/streaming/save/reconnect/recovery slice; no survival systems | Accepted as mandatory vertical slice |
| Platforms | Windows x64 and Linux x64 first; one pinned Godot Forward+ release | Windows/Linux accepted; exact Godot release open |
| Client target | Named minimum machine, 1080p, 60 Hz |  |
| Server target | Named CPU/RAM, .NET/GC mode, SSD/storage contract, and uplink |  |
| Capacity | 2 human clients for playability; 8 human-scale acceptance; 16-bot non-promised stress |  |
| Visibility | Start the fixture at 8 horizontal and 4 vertical full-detail 16-block sections; no far LoD |  |
| Build height | Explicit initial dimension build range approximately 10,000 blocks tall | Accepted; exact min/max placement open |
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
| C2 | One fixed 60 TPS authoritative `WorldTick`; packet/render/slower-system rates do not create another clock | Owner greenlit; capacity-gated | `WORLD-08`, `NET-06` |
| C3 | Godot-free core; Godot presentation boundary; no block-per-node model | Greenlight boundary; `RENDER-01` architecture accepted, `RENDER-02` workflow details still need owner review | `ARCH-03`, `RENDER-01/02` |
| C4 | One live-world writer; workers publish immutable revisioned bounded proposals | Greenlight | `WORLD-02`, `WORLD-05`, `WORLD-08` |
| C5 | Sparse finite 3D section identity; signed coordinates; initial 10,000-block build policy and explicit borders | Owner greenlit policy; benchmark section side | `WORLD-01` |
| C6 | Stable namespaced IDs distinct from world/session `uint32` IDs; missing required gameplay content blocks normal open | Owner greenlit | `GAME-01`, `WORLD-09` |
| C7 | Durable save receipt has an explicit crash contract; corrupt/newer data fails closed | Greenlight | `WORLD-03/04/05/09` |
| C8 | Separate version domains for protocol, gameplay, saves, generators, packs, and mods | Greenlight | `NET-07`, `WORLD-09`, `ASSET-02`, `MOD-03` |
| C9 | `.vcpak` is bounded resource-only data; Minecraft support is offline conversion | Greenlight boundary | `ASSET-01/02`, `NET-09` |
| C10 | Native .NET is trusted; enforceable permissions require a real sandbox/capabilities | Greenlight trust split | `MOD-01/02`, `ARCH-05` |
| C11 | Bounded work/admission plus upstream DDoS responsibility; one authenticated session cannot crash/materially stall the declared fixture | Owner greenlit boundary; benchmark invariant | `NET-08` |

## C. Greenlight experiments, not mechanisms

| ID | Experiment | Leading candidate versus fallback | Decision produced |
| --- | --- | --- | --- |
| E1 | Core data | 16³ vs 32³ sections; adaptive palette vs simpler storage | Freeze section side/indexing/IDs before user worlds |
| E2 | Persistence | SQLite WAL/FULL vs one measured fallback if it fails | Freeze v1 store/durability/envelope |
| E3 | Movement clock | Fixed 60 TPS shared prediction/authority | Prove replay, correction, capacity, and overload behavior; rate already selected |
| E4 | Transport/trust | Selected pinned GNS; alternative only for a measured showstopper | Accept ownership, trust, admission, backpressure, and packaging |
| E5 | Local host | Validate the selected supervised child-loopback topology; retain embedded hosting only as a conformance/fallback adapter | Greenlight packaging, pause/save, crash isolation, and lifecycle behavior without changing `ServerCore` |
| E6 | Renderer | Accepted data-oriented chunk-mesh architecture; hidden-face vs greedy and nodes/`ArrayMesh` vs low-level server path remain measured choices | Explain/approve the `RENDER-02` job workflow, then select the simplest backend meeting the fixture |
| E7 | Resource pack | Minimal `.vcpak`, strict ZIP/VFS, logical digest, one base pack | Freeze only exercised resource identity/path contracts |
| E8 | Wasm sandbox | Wasmtime host/toolchain/compile/cache/capability hostile spike | Greenlight Wasm, choose another isolation, or defer mods |
| E9 | Minimal v1 far terrain | Fog-obscured bounded coarse representation after first playable; 3D mip is leading universal candidate | Select the cheapest representation/horizon meeting v1 silhouette and resource caps |
| E10 | Interest behavior | Layered full-detail/summary subscriptions with bounded priority queues; constants deliberately unset | Obtain owner approval of the player-visible degradation rules, then measure radii, budgets, and hysteresis |

Every experiment ends in `greenlight`, `revise`, `defer`, or `reject`, and failed measurements remain recorded.

## D. Defer by default

- Survival breadth until the integrated first playable passes G5.
- Combat rewind, support-loss grace, speculative collision, and whole-world rollback.
- Minecraft conversion, the native voxel model/rig format spike, user-authored animation graphs, and hot reload beyond the first-party need. Ordered whole-asset resource-pack overlays are already selected; a dependency DAG is not planned.
- Sandboxed/public mods and ABI 1.0 until sandbox tests and two first-party dogfood features survive refactoring. Private native forks remain outside the public extension ecosystem.
- Structures, multiple generator epochs, seam adapters, broad biomes/caves, and redstone until the basic durable world is healthy.
- Extended far-terrain quality/horizons, shader light pages, GI, volumetrics, OIT,
  advanced reflection/refraction, and procedural assets. Minimal fog-obscured far
  terrain is promoted to a post-first-playable v1 gate.
- Parallel live-region simulation, alternative production databases, anonymous public
  servers, production relays/account services, and alternate world-TPS profiles.

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
Experiments E1–E10: [greenlight / changes]
Deferrals: [accept / promoted items with reason]
Rejected claims: [accept / exceptions]
```

The implementation gate sequence and exact exit criteria are in [`REVIEW-product-scope-and-sequencing.md`](REVIEW-product-scope-and-sequencing.md).
