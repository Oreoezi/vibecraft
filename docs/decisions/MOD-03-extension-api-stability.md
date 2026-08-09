# MOD-03 Extension API lifecycle and stability

Status: Proposed

## Decision

Recommended choice: Expose a small versioned capability API built from registration phases, immutable read views, validated commands, typed events, and host-owned schedulers; keep engine internals inaccessible and make compatibility promises only after each surface survives use by first-party content.

One-sentence rationale: Stable semantic boundaries preserve future concurrency, networking, and persistence changes, whereas unrestricted object access, mixins, and synchronous hot-path hooks turn implementation details into permanent public contracts.

## Context and constraints

- VibeCraft wants server plugins, sandboxed client mods, data-driven content, and permission scopes.
- The server may later change storage, entity representation, tick rates, or simulation ownership.
- Extension code can stall ticks, retain stale references, generate excessive state, or introduce nondeterminism even when it cannot escape a sandbox.
- Client and server modules may need related but different capabilities.

## Options considered

| Option | Strengths | Costs/risks | Fit |
| --- | --- | --- | --- |
| Public engine classes plus reflection/patching | Maximum power and fast ecosystem growth | No security or compatibility boundary; internals freeze | Trusted developer mode only |
| Universal event bus with mutable objects | Familiar and flexible | Ordering/cancellation debt; hot hooks; shared ownership | Reject as the core model |
| Data packs plus narrow commands/events/capabilities | Stable, testable, sandbox-friendly | Some features require new host APIs | Recommended |
| Separate ad hoc API per subsystem | Local optimization | Inconsistent lifecycle, errors, security, and versioning | Reject |

## Evidence

Paper's plugin documentation separates load, enable, and disable phases, warns that APIs are available only in appropriate lifecycle phases, and cautions that hot events can cause lag ([Paper plugin lifecycle](https://docs.papermc.io/paper/dev/how-do-plugins-work/)). Its newer lifecycle API exists because early registry/bootstrap events do not fit the older runtime event system and notes that some registrations prevent safe reload ([Paper lifecycle API](https://docs.papermc.io/paper/dev/lifecycle/)). This is direct evidence that lifecycle and mutability phase become public compatibility contracts.

Folia requires global, region, async, and entity schedulers because plugins acting on world state need the correct ownership context ([Paper/Folia support](https://docs.papermc.io/paper/dev/folia-support/)). VibeCraft can avoid retrofitting this debt by never promising arbitrary-thread world mutation.

Fabric Loader exposes entrypoints and metadata but also supports mixins that transform engine and other mod classes ([Fabric Loader documentation](https://docs.fabricmc.net/develop/loader/)). Mixins are powerful for modifying an existing closed game; they are incompatible with VibeCraft's stated sandbox and stable-boundary goals.

Veloren's experimental WASM plugins communicate through events because the host ABI must be explicit; its documentation acknowledges the API is still being used to build consensus ([Veloren plugin guide](https://veloren.gitlab.io/book/contributors/modders/writing-a-plugin.html)). Terasology's components/systems/events show how extensions can compose behavior without every component owning logic ([Terasology entity system](https://metaterasology.github.io/docs/concepts/entitySystem.html)). Luanti advises stable games/mods to preserve old names or aliases to keep worlds compatible ([Luanti world-compatibility guidance](https://docs.luanti.org/for-creators/keeping-world-compatibility/)).

## Proposed design

### Three extension tiers

1. `data_pack`: future declarative blocks, items, recipes, loot, tags, biomes, structures, and configured behaviors under its own parser. Preferred whenever sufficient. Visual resources remain separate `.vcpak` artifacts.
2. `sandbox_component`: `.vcmod` capability-limited executable components for server/client logic. Portable ABI, metered execution, no ambient filesystem/network/process access after the sandbox gate passes.
3. `native_plugin`: in-process .NET server/client plugins with full process authority and a distinct trusted installation path. Explicitly unsupported as a security boundary and allowed only by operator/user choice.

The tiers may share namespaced identity/resolver vocabulary, but never an artifact parser, extension, or trust prompt. Tier does not imply gameplay authority: the server validates every client-originated command.

### Lifecycle

```text
discover manifests
  -> resolve dependency/content lock
  -> instantiate sandbox/trusted hosts
  -> bootstrap (declare capabilities and migrations)
  -> register (content, codecs, commands, handlers)
  -> freeze registries
  -> open world / validate required saved namespaces
  -> start runtime events and scheduled work
  -> quiesce (stop new work)
  -> save barrier
  -> shutdown/dispose
```

Runtime reload is not promised for executable code in v1. Resource/data-pack reload may be supported only where registries and live state define an explicit rebinding/migration contract. “Disable” must cancel host-owned schedules/subscriptions before teardown.

### API shape

Extensions receive opaque handles and immutable snapshots/read models:

```text
PlayerView, EntityView, SectionView, InventoryView, RegistryView
```

Mutations are validated commands submitted to an owner/scheduler:

```text
TrySetBlock(command)
TrySpawnEntity(command)
TryMoveItem(transaction)
Schedule(owner, due, command)
SendPresentationEvent(command)
```

Every result is explicit: accepted, rejected with stable reason, deferred, throttled, stale revision, missing capability, or invalid owner. Extensions never retain references to mutable section/component arrays or Godot nodes.

### Events

Use a small typed catalog with explicit semantics:

- `Before*` policy events exist only where cancellation/modification is intentionally supported.
- `After*` facts are immutable and cannot change committed state.
- Observation handlers cannot block authoritative completion; where possible they receive queued snapshots after commit.
- Handler order is deterministic: dependency order, then manifest priority, then namespaced ID. Do not expose arbitrary integer priorities with undocumented tie behavior.
- Recursive event depth and events per tick are bounded.
- Hot events such as every movement substep, voxel query, light sample, or mesh face are not public hooks.

Use bulk events—section activated, transaction committed, entity batch tick opportunity—instead of per-block polling. A plugin needing a missing hook requests a deliberate API addition rather than patching internals.

### Scheduling and ownership

Extensions never create threads that touch world state. The host exposes:

- simulation-owner scheduling by dimension/region/entity;
- background pure jobs with serializable/immutable inputs;
- wall-clock tasks that may perform permitted I/O but return commands to the simulation owner;
- cancellation tokens tied to plugin, world, and section lifetime.

Each callback/job receives fuel/time/allocation/output budgets. Trusted plugins cannot be securely contained, but the host still measures and attributes their time to support diagnosis and policy.

### Persistence

- Each extension owns namespaced, size-limited world/player/entity storage.
- Persisted records declare extension schema versions.
- Migrations are registered during bootstrap and run only within that namespace.
- Missing extension data is preserved/quarantined according to `WORLD-09`; it is not silently deleted.
- Extensions cannot serialize arbitrary runtime object graphs or assembly-qualified types into core saves.

### Compatibility

The host publishes `mod_abi_major` plus named capability versions. Within one stable major:

- add optional capabilities and fields;
- preserve existing semantic behavior or document a corrected behavior behind a new capability version;
- never reuse identifiers or change a command/event from `After` fact to cancelable `Before` policy in place;
- maintain conformance fixtures for supported capabilities.

Before public ABI 1.0, all extension APIs are experimental and packages pin an exact supported range. First-party gameplay must use the same public data/command interfaces for at least one milestone before stability is declared.

### Failure policy

- Manifest/registration failure: reject package before world open.
- Runtime trap/exception: abort that callback, record attribution, and disable the extension after a configurable repeated-failure threshold.
- Timeout/fuel exhaustion: same as trap; never extend a simulation deadline indefinitely.
- Failed required server extension: stop or enter an explicit safe/read-only mode; do not continue while dropping authoritative mechanics.
- Failed cosmetic client extension: disable it and continue when policy permits.

## Greenlight criteria

- A plugin cannot obtain mutable engine collections, Godot objects, sockets, or file handles without a named capability.
- All world mutations execute on an owning simulation context through validated commands.
- Plugin disable cancels schedules/subscriptions and leaves no callable stale handle.
- Missing plugin data survives load/save according to `WORLD-09`.
- A deliberately slow or recursive sandbox extension cannot make queues or memory unbounded.
- At least two first-party features are implemented through public data/command/event surfaces before ABI stability is promised.

## Prototype or benchmark

Required: yes.

Implement one data pack and one sandbox module that add a block, item, recipe, command, scheduled behavior, namespaced persisted value, and a client presentation event. Test dependency failure, stale handles, denied capabilities, recursive events, fuel exhaustion, unload cancellation, schema migration, missing package on reload, and host ABI mismatch.

Pass condition: deterministic results across repeated runs; bounded callback time/output; no direct core references cross the ABI; removal/re-addition preserves owned data; a host internal storage refactor changes no extension fixture.

## Risks and open questions

- A capability API can still expose information useful to cheating; client capabilities need a separate confidentiality/fairness review.
- WASM component-model tooling and C# guest support must be benchmarked before choosing a runtime.
- Deterministic ordering can create dependencies between plugins even when documented; diagnostics should expose handler order.
- Trusted native plugins remain able to bypass every cooperative API rule.

## Dependencies

- Requires: `ARCH-05`, `MOD-01`, `MOD-02`, `GAME-01`, `WORLD-09`, `NET-07`.
- Blocks: public mod SDK, plugin marketplace/distribution, stable release policy.

## Rejected or deferred alternatives

- Mixins/runtime patching as supported API: rejected.
- Arbitrary mutable-engine-object event bus: rejected.
- Hot reload of executable plugins in v1: rejected.
- Stable ABI declaration before first-party dogfooding: rejected.
