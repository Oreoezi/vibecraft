# MOD-01 Client mod runtime

Status: Proposed

Owner: VibeCraft architecture research
Date researched: 2026-08-09
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Ship resource packs first, then one sandboxed public component API
only after the hostile runtime/toolchain prototype. The sandbox runtime is not selected
yet; WebAssembly/WIT remains a researched candidate alongside a carefully constrained
Lua-family host. Do not maintain opt-in native .NET plugins as a parallel public API.

One-sentence rationale: The public API must have one capability-enforced boundary;
native .NET assemblies retain the process's filesystem, network, native-code,
reflection, memory, and termination authority and therefore cannot be that boundary.

### Owner decision — 2026-08-10

There is one public extension ecosystem. Native code belongs in private forks, not a
second supported mod tier. WebAssembly and a sandboxed Lua-family host are candidates;
the selected runtime must enforce the same capability, quota, interruption, storage,
and update requirements before any public ABI is promised.

The phrase “native client-side mod support with scoped permissions” in `design_doc.md` must therefore be split:

- **Sandboxed client mods** receive enforceable scoped capabilities through the one
  selected public runtime.
- **Private native modifications** are unrestricted code chosen outside the VibeCraft
  extension contract; they receive no compatibility or security promise.
- **Data/resource packs** contain no executable code and remain the preferred route for content that does not require logic.

The sandbox protects the player’s machine and the client process from downloaded mod code. It does **not** prove to a server that the client is honest, prevent a modified client binary, or conceal information already replicated to the client. `NET-09` package hashes establish byte agreement only.

## Context and constraints

- The client is Godot with C#, but untrusted code must not receive Godot objects, CLR objects, raw pointers, native library loading, reflection, process APIs, arbitrary files, or sockets.
- Client mods need useful presentation APIs—owned UI, particles, audio, visible-world read models, input actions, and namespaced local storage—without direct authority over durable gameplay.
- Server-required client components must use the ordinary input/action-intent protocol from `ARCH-01`; they cannot write authoritative transforms, inventories, chunks, combat results, or animation claims.
- A malicious module may attempt an escape, CPU or memory denial of service, host-call amplification, stale/forged handles, filesystem or network access, data exfiltration, event recursion, log flooding, persistence bombs, or exploitation of the Wasm runtime itself.
- A benign but broken module may trap, spin forever, allocate until failure, emit malformed values, retain stale handles, depend on event order, or fail after the server has made it required.
- The sandbox runtime and compiler toolchain are security dependencies that require prompt updates and a platform test matrix.

## Options considered

| Option | Strengths | Costs and failure modes | Fit for VibeCraft |
| --- | --- | --- | --- |
| In-process native .NET with `AssemblyLoadContext` | Best C# ergonomics and performance; broad ecosystem access | Full process authority; cooperative unload; reflection/PInvoke/threads can bypass every friendly API | Trusted tier only |
| Sandboxed Lua/Luau VM | Fast iteration and small scripts | Security depends on a perfect allowlist and VM embedding; one accidentally exposed library can become ambient authority; separate ABI/tooling | Public-runtime candidate; must pass the same capability/quotas prototype |
| Wasm core modules with a handwritten pointer/length ABI | Supported by current Wasmtime .NET APIs; small host surface | Parser/memory-validation burden; custom string/list/resource conventions become permanent | Prototype fallback only |
| Wasm Component Model with WIT interfaces | Typed, versioned, language-neutral contracts and explicit imports/resources | .NET host bindings and guest toolchains are moving; component overhead and supported RIDs need measurement | Strong research candidate; not selected |
| Out-of-process native mod host over IPC | OS process can be killed; stronger defense in depth | Cross-platform OS sandboxing is not uniform; high call latency; complicated UI/render integration | Deferred for high-risk capabilities |
| Browser-style JavaScript runtime | Familiar and naturally event-driven | Adds another large runtime/JIT and host API; sandbox quality depends on embedding; no advantage over Wasm for this project | Reject for v1 |

## Evidence

### .NET is not a sandbox

Microsoft states directly that [`AssemblyLoadContext` provides no security features and loaded code has the full permissions of the process](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext). Microsoft also documents that dependency contexts have no binary isolation and exist to resolve/version assemblies, not contain them ([`AssemblyLoadContext` concepts](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)). Its unload is cooperative: live threads, static references, callbacks, or handles can keep a collectible context alive after `Unload` is requested ([assembly unloadability](https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability)).

Code Access Security is not a replacement. Microsoft says CAS and partially trusted code are not supported security boundaries and advises against executing unknown-origin code without alternative measures ([secure coding guidelines](https://learn.microsoft.com/en-us/dotnet/standard/security/secure-coding-guidelines), [`SYSLIB0003`](https://learn.microsoft.com/en-us/dotnet/fundamentals/syslib-diagnostics/syslib0003)).

Direct conclusion: a native `.dll` can call `System.IO`, open sockets, P/Invoke native libraries, inspect the process, spawn threads, retain references, or terminate the game. A VibeCraft permissions JSON cannot revoke CLR authority.

### WebAssembly, WASI, and Wasmtime

WASI describes a capability model in which a module/component starts without ambient authority and can act only through interfaces the host grants ([WASI introduction](https://wasi.dev/)). WIT defines typed interfaces, imports/exports, versioned packages, and host-owned resource handles rather than exposing language-specific objects ([WIT reference](https://component-model.bytecodealliance.org/design/wit.html)). These properties match the required stable extension boundary; they do not remove the need to validate every host function.

Wasmtime documents that Wasm memory accesses are bounds checked, control flow is type checked, and all outside interaction occurs through explicit imports ([Wasmtime security](https://docs.wasmtime.dev/security.html)). It provides deterministic fuel and lower-overhead wall-clock-oriented epoch interruption; an interruption traps or yields rather than safely resuming arbitrary partially completed host work ([interrupting execution](https://docs.wasmtime.dev/examples-interrupting-wasm.html)). The official .NET binding exposes fuel charging, epoch deadlines, and host-call fuel accounting primitives ([`Store`](https://bytecodealliance.github.io/wasmtime-dotnet/api/Wasmtime.Store.html), [`Caller`](https://bytecodealliance.github.io/wasmtime-dotnet/api/Wasmtime.Caller.html), [`Config`](https://bytecodealliance.github.io/wasmtime-dotnet/api/Wasmtime.Config.html)).

The sandbox is still a maintained dependency, not a theorem. Wasmtime publishes security advisories, including 2026 issues involving WASI permission checks, component string transcoding, tables, host-data leakage, and preemption/trap state ([Wasmtime security advisories](https://github.com/bytecodealliance/wasmtime/security)). VibeCraft must pin an audited build, subscribe to announcements, and ship runtime fixes quickly. General WASI filesystem/socket/CLI APIs are unnecessary attack surface for ordinary mods and will not be linked in v1.

### Tooling maturity

The Bytecode Alliance's C# component tooling can build WIT-based WASI 0.2 components with .NET 10 and NativeAOT, but its own documentation says the underlying technologies are under heavy development and describes version/import friction ([`componentize-dotnet`](https://github.com/bytecodealliance/componentize-dotnet)). The official Wasmtime .NET package documents the core `Module`/`Linker` API, fuel, and epoch interruption, but its public API documentation does not yet establish every Component Model and resource-limiter facility available in Rust/C ([Wasmtime .NET repository](https://github.com/bytecodealliance/wasmtime-dotnet), [NuGet package](https://www.nuget.org/packages/Wasmtime/)).

Engineering inference: define the desired API in WIT now, but make a .NET-host integration and supported-platform spike a greenlight gate. If the binding cannot instantiate the component, cap all guest/host allocations, and interrupt it reliably, do not silently downgrade the product claim to “native C# with a permission dialog.” Evaluate a small Wasmtime C-API bridge or a long-lived out-of-process component host and amend this decision.

### Existing games and engines

Luanti's client mods run in a shared Lua environment, are locally enabled, and can be restricted by server flags; the API is explicitly unstable and server-to-client mod transfer remains unimplemented ([Luanti client API](https://github.com/luanti-org/luanti/blob/master/doc/client_lua_api.md)). Luanti's advisory history contains repeated sandbox and HTTP/insecure-environment access-control failures, including 2026 advisories ([Luanti security](https://github.com/luanti-org/luanti/security)). This is direct evidence that adding a scripting language and hiding a few functions is an ongoing security program, not a one-off feature.

Veloren uses Wasm plugins and an event-driven game interface, and describes portability, sandboxing, and eventual server-to-client distribution as goals ([Veloren plugin guide](https://book.veloren.net/contributors/modders/writing-a-plugin.html)). Its current dependency manifest includes Wasmtime/WASI while its plugin API has evolved substantially over time ([Veloren dependency dashboard](https://gitlab.com/veloren/veloren/-/work_items/2192)). This supports the runtime direction but also warns against declaring an ABI stable before real first-party use.

Fabric's mixin model can transform game classes and is valuable for modifying a closed game, but it is intentionally not a constrained security interface ([Fabric Loader documentation](https://docs.fabricmc.net/develop/loader/)). Native VibeCraft plugins belong in the same trust category.

## Proposed design

### Runtime and artifact classes

```text
data_pack
  declarative registries/configuration; no executable payload

sandbox_component
  one selected sandbox-runtime artifact after its hostile-host gate
  imports only granted VibeCraft capability interfaces
  isolated instance, memory, handle table, queues, and storage namespace

private native modification
  outside public package discovery, multiplayer requirements, and compatibility policy
```

The distinct `.vcmod`/`mod.json` manifest declares `artifact_kind: sandbox_component`, side, logical digest, supported `mod_abi` range, requested capabilities, and quota class. A content lock selects an exact artifact/ABI for cooperating clients. The user grants client capabilities; a server may require a sandbox component and minimum grant set, but it cannot force the user to grant locally sensitive authority. In that case joining fails with an actionable error.

Private native modifications are never downloaded, selected, or required by the public
extension resolver. They are ordinary local code changes/forks, not a package kind that
normal users or servers must support.

### ABI and imports

- Use an exact WIT package such as `package vibecraft:client-host@0.1.0;` containing `world client-mod` (and a distinct server world/package). The manifest's `mod_abi` may be a resolver range, but the lock records one selected exact package version; WIT syntax itself never uses `0.1.x`.
- Link only the exact `vibecraft:*` interfaces granted to that module. Reject undeclared imports before instantiation.
- Link **no general WASI filesystem, socket, HTTP, process, environment, terminal, wall-clock, or entropy interfaces** in v1. Supply game-specific logging, deterministic random, logical time, and namespaced storage interfaces where needed.
- Treat automatically introduced `wasi:*` imports from a guest toolchain as a build/validation failure unless each import is explicitly reviewed and implemented with the same policy. Stubbing or virtualizing an import to “denied” is acceptable only when the failure behavior is deterministic and tested.
- No CLR/Godot object, delegate, pointer, span, stream, exception type, or assembly-qualified name crosses the ABI. Values are bounded WIT records/lists/variants or opaque generational resources.
- One module instance receives an unforgeable host-created `PrincipalId` in its `Store`; host calls derive package name, approved update lineage, side/component, and grants from that principal. `PackageId` is a dependency name, not storage/security identity, and the guest never supplies either identity and asks the host to trust it.

### Compilation and cache boundary

- `.vcmod` accepts only standard Wasm/component bytes plus bounded metadata. Reject WAT, Wasmtime serialized native modules/components, native libraries, and resource-pack payloads.
- Bound validation/adaptation/compilation as hostile work, not only execution. Prefer a killable helper process with OS CPU/memory/deadline limits unless the prototype demonstrates equivalent containment in-process.
- Never call Wasmtime native deserialization on downloaded/package bytes. A private compiled cache is accepted only when produced locally by the exact runtime/configuration, keyed by source logical digest and feature policy, protected from package replacement, and invalidated on runtime/security upgrades.
- Cap types, functions, tables, memories, imports/exports, custom sections, binding/transcoding allocations, and adapter/resource tables independently of file and linear-memory limits. Audit the complete transitive import set after C# component adaptation.
- A resource handle is valid only in the creating module instance, includes a generation, has an owner/lifetime, and is checked on every call. Handles cannot be serialized as durable identity.

### Client data flow and scheduling

```text
network/render/game state
        -> bounded immutable event snapshot
        -> per-module mod executor callback
        -> bounded presentation/action command buffer
        -> host validation and capability check
        -> Godot main-thread publication or normal network intent
```

- Wasm callbacks do not run on the Godot render/main thread. A dedicated bounded executor invokes modules one at a time per instance; a Wasmtime `Store` is never used concurrently.
- Event input is a copied/bounded read model. Mods cannot retain a view into mutable chunk, entity, inventory, network, renderer, or input buffers.
- Output is staged. UI, particles, sounds, and local settings publish at a main-thread budget boundary. Gameplay requests become the same sequenced action intents available to the base client and remain server-validated under `ARCH-01`.
- A callback cannot synchronously trigger another callback in the same module. New events are queued for a later dispatch boundary with recursion and queue caps.
- Client mods cannot register arbitrary per-frame polling. The host sends coalesced/batched events and optional fixed-rate callbacks with an explicit maximum rate.
- v1 exposes no guest-created threads, shared Wasm memory, blocking host calls, or async continuation across a callback. Long computations must be split into host-scheduled future callbacks with module-owned serialized state.

### Provisional runtime limits

These are safe starting limits to validate, not immutable ABI promises:

| Resource | Default per module | Aggregate/default behavior |
| --- | ---: | --- |
| Package Wasm bytes | 16 MiB | Reject larger executable payload before compilation |
| Linear memory | 64 MiB maximum, declared up front | 256 MiB across sandboxed client modules |
| Wasm stack | 1 MiB | Runtime-enforced |
| Live host handles | 4,096 | Generation checked; release all on disable |
| Event input/output | 256 KiB each per callback | Reject/truncate by typed policy; never allocate from guest length first |
| Host calls | 1,000 per callback | Each call additionally charges fuel proportional to validated bytes/work |
| Output commands | 256 per callback, 2,048 queued | Coalesce presentation updates where semantics allow |
| Schedules/subscriptions | 1,024 each | All tied to module lifetime |
| Client storage | 8 MiB per profile/server namespace | Explicit larger quota requires user approval |
| Logs | 64 KiB/minute with burst 16 KiB | Sanitize control characters; aggregate suppression notices |

Fuel is the deterministic primary CPU limit. During the prototype, calibrate a fuel quantum on every supported architecture and choose the default callback allowance that corresponds to at most **1 ms p95** on the minimum supported machine. The per-module aggregate target is **2 ms of CPU per rendered 16.7 ms frame**, and all sandboxed modules together may consume **4 ms p95** before lower-priority callbacks are deferred. Fuel charged by Wasm instructions is supplemented by explicit host-call fuel based on input bytes and operation class.

Epoch interruption is the independent runaway watchdog. A single callback gets a **5 ms wall-clock deadline** in normal operation and traps when exceeded; it is not a scheduling guarantee and cannot make a blocking host function safe. Every host function must be non-blocking, bounded, and cancellation-aware independently. Exact fuel numbers are machine/runtime-specific configuration and are intentionally not part of the public ABI.

Reject modules that request threads/shared memory, memory64, multiple memories, unbounded memory/table growth, unsupported proposals, or imports outside the resolved world. If the chosen .NET embedding cannot enforce a whole-store allocation limit, require declared maxima and static validation and count all host allocations separately; inability to prove the cap blocks greenlight.

### Failure and disable policy

- Validation, import, capability, ABI, or instantiation failure: do not run the module; provide a stable diagnostic. A required module prevents joining.
- Trap, fuel exhaustion, deadline, malformed result, or host validation failure: discard the callback's entire output buffer and storage transaction. No partial client/gameplay action is published.
- First ordinary fault: record attribution and notify the user without a modal loop. Three faults in 60 seconds disable the module for the session. An escape attempt, forbidden import, cross-module handle, or repeated quota attack disables it immediately.
- Required gameplay client module failure after joining: stop invoking it, disconnect with a specific reason, and preserve diagnostics. Required behavior must not silently disappear while the session continues.
- Optional cosmetic module failure: disable it and continue.
- Disable cancels schedules/subscriptions, invalidates all handles, drains/discards queued outputs, rolls back the open storage transaction, disposes the instance/store, and verifies that no module-owned work remains.
- Native .NET exceptions may be caught for diagnostics, but native plugins can corrupt state, retain threads/references, or terminate the process; no containment or reliable disable guarantee is made.

### Persistence

- Client storage is host-owned and scoped by `(local profile, authenticated server-key identity or local-world identity, PrincipalId, component_id)` with bounded key/value/count/total sizes. Unsigned updates inherit a principal only after explicit local approval; merely reclaiming a `PackageId` grants no prior data.
- Writes are buffered per callback and commit only after the callback succeeds. Storage schema version belongs to the package, not a CLR type name.
- A migration callback sees only its own prior namespace, receives tighter fuel/time/output quotas, and commits atomically. Failure preserves old bytes and prevents the required module from starting.
- Removing a module does not silently delete its storage. Cleanup/export is an explicit user action, consistent with `WORLD-09`'s unknown-data policy.

## Threat model and non-goals

Protected assets:

- user files, credentials, clipboard, network identity, processes, and devices;
- client process integrity and availability;
- data belonging to other modules and profiles;
- server/session tokens not explicitly exposed to a module;
- bounded CPU, memory, GPU publication, logs, disk, and event queues.

Adversaries include a deliberately malicious downloaded module, a compromised publisher/package, a malicious server requiring a module, and malformed persistent module data. The host runtime, VibeCraft host functions, package parser, Wasmtime, generated bindings, and OS remain in the trusted computing base.

Out of scope:

- Protecting a public server from a modified client; server authority and validation do that.
- Preventing analysis of chunk/entity information already sent to the client.
- Containing native .NET plugins.
- Guaranteeing safety after a Wasmtime/JIT sandbox escape; prompt patching and optional future process isolation provide defense in depth.
- Granting arbitrary Internet, filesystem, process, or native-library access to sandboxed modules in v1.

## Greenlight criteria

- Product and UI language consistently distinguish data packs, sandboxed Wasm, and trusted native .NET.
- The selected host path instantiates the WIT component from C#, links only approved imports, enforces linear-memory/table/stack/host-allocation limits, charges fuel including host work, and interrupts a runaway callback on Windows, Linux, and macOS x64/arm64 targets that VibeCraft supports.
- A C# guest-tooling experiment either produces an artifact with exactly the approved import set and acceptable startup/size, or C# is explicitly excluded from the first sandboxed SDK while trusted native C# remains available.
- No Godot/CLR object or mutable engine buffer crosses the ABI; all mutations and presentation effects pass through validated commands.
- Required-module failure disconnects cleanly; optional-module failure leaves the base client usable; disable leaves no live handles, callbacks, schedules, or queued output.
- The escape/DoS suite below passes against release builds, sanitizers where available, and every supported architecture.
- A security-update policy identifies the embedded Wasmtime version, checks its advisories in CI/release review, and defines a maximum response time for critical/high sandbox issues.

## Prototype and security test plan

Required: yes.

Build one client with two sandboxed modules: a legitimate HUD/particle module and an adversarial test module. Exercise both a Rust guest and a C# Component Model guest if the toolchain can produce one. Compare direct Wasmtime .NET Component Model support, a narrow Wasmtime C-API bridge, and a long-lived out-of-process host only where the first path lacks required controls.

Functional scenarios:

- subscribe to bounded player/visible-world events, create an owned HUD tree, play a sound/particle, store settings, and send an ordinary action intent;
- negotiate exact ABI/capabilities through `NET-09`, reject a missing grant, disable/re-enable across restart, and migrate one storage schema;
- run 32 representative cosmetic modules for 30 minutes while rendering/streaming chunks and record CPU, frame publication cost, memory, startup, and queue depth.

Escape and abuse corpus:

- import WASI filesystem, sockets, HTTP, environment, process exit, clocks, entropy, terminal, and an unknown `vibecraft:*` interface;
- use out-of-bounds pointers, integer overflow, negative/huge lengths, invalid UTF-8, NaN/infinite coordinates, oversized lists/strings, invalid enum tags, and malformed component metadata;
- grow memory/table beyond declared maxima, recurse until stack exhaustion, loop forever, burn fuel in host calls, allocate host handles without release, and create event/schedule/log/output storms;
- guess another module's handle, reuse a stale generation, pass a handle after disable, use a resource in the wrong profile/server, and race a revisioned visible-world read with unload;
- trigger callback reentrancy, trap after staging storage/UI/action commands, trap during migration, and disconnect while work is queued;
- compile with threads/shared memory, memory64, multi-memory, unexpected proposal features, automatic toolchain WASI imports, oversized compiler structures/custom sections, and package-supplied serialized native Wasmtime cache bytes;
- fuzz every ABI decoder and host-call validator from raw component bytes and valid instantiated guests;
- demonstrate separately that a native .NET test plugin can read a file/open a socket despite a fake denied permission, proving the UI classification remains honest.

Pass conditions:

- No denied OS or cross-module resource is observed; forbidden imports fail before execution.
- Every infinite/expansive case traps or is rejected within the declared fuel, wall-time, memory, queue, disk, and handle limits without killing or permanently stalling the client.
- A failed callback publishes zero staged effects and zero storage writes.
- At target load, sandbox work stays within the 4 ms p95 aggregate budget, main-thread publication stays below 1 ms p95, and no queue grows without bound on the declared minimum machine.
- Repeated seeded runs produce identical logical-time/random results and failure classifications; wall-time interruption may occur at a different instruction but cannot change committed output.

## Risks and unresolved questions

- The Wasmtime .NET binding may lag Rust/C runtime features or security releases, especially Component Model, store limiting, async, and platform artifacts. This is the largest implementation/tooling risk.
- Component Model/WASI and C# NativeAOT tooling continue to evolve. Generated imports, artifact size, trimming, exception behavior, debugging, and source maps must be tested rather than assumed.
- Wasmtime's JIT requires executable-memory support on common platforms; mobile/consoles and hardened Apple environments may require AOT/interpreter strategies. Wasmtime documents weaker testing outside Windows/macOS/Linux and no Cranelift 32-bit support ([platform support](https://docs.wasmtime.dev/stability-platform-support.html)).
- Fuel cost changes across runtime upgrades can change throughput. Recalibrate policy per runtime build while keeping semantic count/output limits stable.
- In-process Wasm is a strong language/runtime boundary but not OS process isolation. High-risk future capabilities such as arbitrary HTTP or user-selected files may justify a broker process.
- Sandboxed C# ergonomics may disappoint developers expecting the full CLR/BCL. The SDK must publish its supported subset clearly.
- Client mods can still automate allowed input, inspect allowed visible data, or alter presentation. Capability safety for the user's machine is separate from competitive fairness.

## Dependencies

- Requires: `FOUNDATION-00`, `ARCH-01`, `ARCH-02`, `NET-07`.
- Coordinates with: `NET-09`, `MOD-02`, `MOD-03`, `ARCH-05`, asset/package manifests, Godot client boundary, UI/input design, release security response.
- Blocks: enforceable `MOD-02` capabilities, the sandboxed path in `ARCH-05`, sandboxed client SDK, safe required-client-module policy, and the public “scoped permissions” claim.

## Rejected or deferred alternatives

- Native .NET plus declarative permission checks as a sandbox: rejected; the CLR does not enforce them.
- `AssemblyLoadContext` as security isolation: rejected; retain it only for native dependency separation and best-effort unload.
- General WASI inheritance (`filesystem`, `sockets`, environment, stdio) for every module: rejected.
- Automatically downloading/running native code required by a server: rejected.
- Remote attestation as proof that required mods are running: rejected for v1 and outside this threat model.
- Executable hot reload: deferred; disable/restart is the first supported lifecycle.
- Arbitrary network/filesystem capabilities: deferred until a brokered design and separate UX/security review.
