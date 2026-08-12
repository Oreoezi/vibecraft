# Owner decisions

This is the compact record of decisions made after the first research pass. It turns
owner review into explicit project direction without pretending that an unrun
prototype has passed. Individual briefs remain the detailed source for interfaces,
risks, and tests.

Sources: [`../questions.md`](../questions.md), reviewed 2026-08-10, and
[`../questions2.md`](../questions2.md), reviewed 2026-08-13.

## Accepted decisions

| ID | Decision | Scope and consequence |
| --- | --- | --- |
| OD-01 | Use server-authoritative input simulation with narrow local prediction. | The server must reject speed, timer, fly, noclip, and other impossible movement/state outcomes. This is not a claim to detect aim assistance, macros, bots, or every client modification. |
| OD-02 | Keep Minecraft-like discrete gameplay light, but make its propagation scheduled and budgeted. | The server owns 0–15 gameplay light for rules such as spawning. Block/light edits enqueue coalesced work; a piston/redstone spam cannot synchronously force an unbounded solve. Exact cadence and priority remain a benchmarked scheduler policy. |
| OD-03 | Keep ordinary blocks compact and make block entities sparse, activation-driven work. | A chest-heavy area must not cause every block entity to update merely because it is loaded. Block entities wake from explicit events, players/interest, due schedules, or bounded background policy. |
| OD-04 | First supported platforms are Windows x64 and Linux x64. | CI, packaging, native dependencies, and first performance fixtures target those platforms. Other targets are deferred. |
| OD-05 | Use the supervised child-server plus loopback gameplay transport as the desktop singleplayer default. | The same `ServerCore` remains host-agnostic; an embedded host can remain a test/fallback adapter, but it is not an equal product topology decision. Pausing is a lifecycle policy over the same server simulation. |
| OD-06 | Ship one public extension ecosystem rather than separate official native and sandboxed plugin ecosystems. | The public extension API is capability-based and portable. Unsupported power should lead to an API request or a project fork; native in-process code is not a supported public-plugin compatibility tier. |
| OD-07 | Resource packs are engine-agnostic ZIP-based `.vcpak` files, with unpacked directories for development. | Runtime assets must not depend on Godot import products. Minecraft conversion is best-effort, offline tooling with no promise of a usable or faithful result. |
| OD-08 | Resource-pack composition is an explicit low-to-high ordered stack with whole-asset replacement. | The last selected pack defining an asset wins. There is no resource-pack dependency DAG, no automatic dependency download, and no texture/data merge semantics. The entire ordered stack is locked for multiplayer compatibility. |
| OD-13 | The first implementation target is a real multiplayer vertical slice. | Starting/loading, connecting, authoritative movement, terrain streaming, block edits, durable save, crash recovery, and reconnect must work end to end before broad feature work can count as progress. |
| OD-14 | Use one fixed 60 TPS authoritative `WorldTick`. | Movement and world commits share a 60 Hz timeline. Rendering and packet/snapshot cadence remain independent, and slower systems run from deadlines/divisors on that timeline. There is no user-selectable tick-rate profile. Sustaining 60 TPS on the declared fixture is a release gate. |
| OD-15 | V1 dimensions use a configurable build range approximately 10,000 blocks tall. | The dimension descriptor stores explicit minimum and exclusive maximum build Y values separated by 10,000 blocks for the initial policy. Sparse signed section addressing remains height-agnostic so changing a policy range does not rewrite save keys. |
| OD-16 | A gameplay-modded world does not enter simulation when required mods/content are missing or incompatible. | Startup fails before section activation with an exact missing/mismatched-content report. Bounded opaque preservation remains useful to recovery/export tooling, but normal play does not substitute placeholders and continue. World-local block-state IDs remain `uint32`; typed namespaced allocation provides mod headroom without freezing a fragile global “mod ID band,” and instance-specific data stays in sparse block-entity/component records rather than dense voxel entries. |
| OD-17 | Accept the recommended deterministic non-recursive block-update/circuit substrate. | Whole-network dust calculation, explicit face-port devices, persisted scheduled transitions, bounded work, and no accidental Minecraft quasi-connectivity/update-order compatibility are the current direction. |
| OD-18 | Accept the server-authoritative hybrid and recommended movement reconciliation design. | Clients predict the shared controller and reversible presentation; the server simulates input and commits state; remote entities interpolate authoritative snapshots. |
| OD-19 | Use GameNetworkingSockets as the default transport implementation, behind the VibeCraft transport boundary. | Integrate the mature library rather than inventing UDP reliability/crypto/congestion. GNS must still pass Windows/Linux packaging, lifecycle, lane/backpressure, identity, and abuse tests; a measured failure may reopen the implementation without changing protocol semantics. |
| OD-20 | V1 block placement, breaking, and attacks use receive-time/current-state authority. | This is NET-04 option A. Actions remain idempotent and prediction is repaired explicitly. Historical/subtick evaluation may be added by a later capability/version without making v1 allocate world or pose rewind history. |
| OD-21 | The protocol must preserve a post-v1 path for native proxies and authenticated server transfer. | A future deployment may accept one public hostname and route a player to a region. Core connection/session state must tolerate a typed transfer offer, reconnect, handoff token, content re-agreement, and trusted-proxy identity without accepting client-forged forwarding metadata. |
| OD-22 | One authenticated player/session must not be able to crash or materially stall a healthy server through supported actions or packet flooding. | Per-session work/queue/rate budgets, bounded generation and simulation requests, overload isolation, and prompt disconnect are v1 requirements. This is testable application-layer resilience, not a DDoS-proof claim. Optional proof-of-work admission remains post-v1 research. |
| OD-23 | Accept the recommended required-content lock agreement. | Matching hashes are compatibility checks for cooperating clients, not attestation or anti-cheat. |
| OD-24 | Accept the recommended section-local CPU meshing architecture. | Start with hidden-face culling plus a greedy full-cube fast path and immutable non-cube templates; keep Godot outside worker meshing. Backend and throughput still require measurement. |
| OD-25 | Minimal fog-obscured far terrain is required for v1, though it need not block the first end-to-end playable. | V1 needs a bounded coarse distant silhouette beyond the full-detail radius. Fidelity, transitions, materials, and horizon are deliberately modest; the implementation is derived/cosmetic and may fall back to fog under load. |
| OD-26 | Keep gameplay light as two block-scale 0–15 values while allowing smooth client shading. | Server rules such as spawning use simple Minecraft-like light levels. The client may interpolate and shade per fragment; visual smoothness does not increase authoritative light-grid resolution. |

## Owner constraints and open format work

| ID | Direction | Current state |
| --- | --- | --- |
| OD-09 | Model/animation replacement should reuse first-party VibeCraft rig profiles, not Minecraft bone layouts. | A replacement model that declares a compatible `RigProfile` can reuse the profile's built-in clips. User-authored skeletal clips and the native voxel-model source format are deferred. GLB/glTF is not accepted as the native public-pack format; an engine-neutral voxel-model/rig format needs a focused format/tooling spike before selection. |
| OD-10 | No arbitrary custom shader source in resource packs. | A future UE-style material graph is desirable only as a bounded, engine-neutral graph that compiles to VibeCraft-owned material templates. It is deferred and may not become general shader code. |
| OD-11 | Godot was selected primarily for open-source licensing; the project currently has little Godot experience. | Keep Godot-specific renderer choices benchmark-gated and validate the developer workflow early. This is product context, not a decision to replace Godot. |
| OD-12 | Plugin persistence should be brokered by the host. | Prototype an operator-configured data-store service that isolates each plugin principal's data (for example separate schema/database/namespace) and never gives public plugins arbitrary database credentials or raw core-world access. Backend and provisioning policy remain open. |
| OD-27 | `NET-05` interest management needs a clearer product discussion before owner approval. | Keep the current layered prototype as a proposal, but do not treat its radii, ellipsoid, scope split, or priority behavior as owner-greenlit until the concrete player/operator behavior is reviewed. |
| OD-28 | `RENDER-02` needs a plain-language walkthrough and prototype evidence before owner approval. | Revisioned bounded mesh jobs remain the engineering recommendation. Worker count, snapshot strategy, Godot upload path, and frame budgets are implementation experiments rather than owner choices. |

## Reading rule

An **accepted decision** changes the relevant brief and baseline now. A **constraint or
open format direction** narrows future work but does not select an untested mechanism
or authorize implementation beyond its prototype gate.
