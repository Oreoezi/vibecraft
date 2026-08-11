# Owner decisions

This is the compact record of decisions made after the first research pass. It turns
owner review into explicit project direction without pretending that an unrun
prototype has passed. Individual briefs remain the detailed source for interfaces,
risks, and tests.

Source: [`../questions.md`](../questions.md), reviewed 2026-08-10.

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

## Owner constraints and open format work

| ID | Direction | Current state |
| --- | --- | --- |
| OD-09 | Model/animation replacement should reuse first-party VibeCraft rig profiles, not Minecraft bone layouts. | A replacement model that declares a compatible `RigProfile` can reuse the profile's built-in clips. User-authored skeletal clips and the native voxel-model source format are deferred. GLB/glTF is not accepted as the native public-pack format; an engine-neutral voxel-model/rig format needs a focused format/tooling spike before selection. |
| OD-10 | No arbitrary custom shader source in resource packs. | A future UE-style material graph is desirable only as a bounded, engine-neutral graph that compiles to VibeCraft-owned material templates. It is deferred and may not become general shader code. |
| OD-11 | Godot was selected primarily for open-source licensing; the project currently has little Godot experience. | Keep Godot-specific renderer choices benchmark-gated and validate the developer workflow early. This is product context, not a decision to replace Godot. |
| OD-12 | Plugin persistence should be brokered by the host. | Prototype an operator-configured data-store service that isolates each plugin principal's data (for example separate schema/database/namespace) and never gives public plugins arbitrary database credentials or raw core-world access. Backend and provisioning policy remain open. |

## Reading rule

An **accepted decision** changes the relevant brief and baseline now. A **constraint or
open format direction** narrows future work but does not select an untested mechanism
or authorize implementation beyond its prototype gate.
