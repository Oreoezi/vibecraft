# ARCH-05 Server plugin execution boundary

Status: Proposed

Owner: VibeCraft architecture research
Date researched: 2026-08-09
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Make data packs and one metered sandboxed public component API the
server extension boundary, with immutable event/read models, host-owned logical
scheduling, and validated command buffers committed by the authoritative simulation
owner. Select the sandbox runtime only after its prototype; do not maintain an
in-process native .NET plugin API as a parallel public ecosystem.

One-sentence rationale: This preserves deterministic single-writer world ownership and
gives extensions one portable, reviewable compatibility surface rather than splitting
the ecosystem between a sandbox and unrestricted engine internals.

### Owner decision — 2026-08-10

There is one public extension ecosystem. Missing power should result in a deliberate
API request or a project fork; it does not create a supported native-plugin escape
hatch. Lua and Wasm remain runtime candidates until the sandbox/tooling gate decides
which can enforce the required limits. Plugin persistence is host-brokered: the server
may use one operator-configured database service and isolate each plugin principal's
data, but public plugins never receive arbitrary database credentials or core-world
storage access.

Server “plugin support out of the box” therefore means:

- first-class declarative content and sandboxed server modules using the same namespaced packages/capability ABI as `MOD-01`/`MOD-02`;
- lifecycle, scheduling, events, commands, persistence, budgets, diagnostics, and failure semantics designed before a public API is stabilized;
- no supported mixins, reflection patches, raw engine collections, direct database access, or asynchronous mutation of live world state.

## Context and constraints

- `ARCH-01` makes the server authoritative. A server plugin is inside that authority boundary and can affect durable worlds, inventories, gameplay, and connected users.
- `WORLD-08` selects one deterministic 20 Hz writer per world for v1 and permits pure/background work only through immutable inputs and ordered publication.
- `ARCH-02` keeps blocks compact, block entities sparse, and dynamic entities in replaceable stores. A plugin API must not expose those layouts.
- `WORLD-09` requires namespaced versioned persistence, bounded opaque preservation, and explicit migrations.
- A malicious or buggy sandbox module may attempt escape, denial of service, invalid mutations, stale handles, persistence abuse, event recursion, information disclosure, or nondeterministic ordering.
- A private native fork may do all of those plus P/Invoke, reflection, arbitrary I/O, thread creation, process termination, and memory corruption through native code. The server cannot securely contain it in-process, so it has no public-plugin compatibility contract.
- Plugin callbacks cannot be allowed to turn a 50 ms world tick into an unbounded deadline. Count/fuel limits preserve deterministic work decisions; wall-time watchdogs detect runaway execution.
- Future regionized simulation is possible but not greenlit. The API should express owner context without claiming that v1 is parallel.

## Options considered

| Option | Strengths | Costs and failure modes | Fit for VibeCraft |
| --- | --- | --- | --- |
| Bukkit/Paper-style native plugins with broad server objects | Familiar, powerful, easy ecosystem growth | Full trust, main-thread stalls, mutable internals and synchronous assumptions become compatibility debt | Private-fork pattern only |
| In-process native .NET plus permissions/`AssemblyLoadContext` | C# ergonomics and dependency separation | No security containment; cooperative unload; arbitrary threads/I/O | Private-fork pattern only |
| Sandboxed Lua with curated globals | Excellent iteration and proven game modding pattern | Host allowlist is a long-lived escape surface; separate VM/ABI; quota implementation required | Viable guest later |
| Wasm components plus capability command/event API | Actual code boundary; typed portable ABI; metering and explicit imports | Tooling/runtime maintenance; host APIs require careful design | **Recommended public executable tier** |
| Out-of-process native plugins over RPC | Killable and OS-isolatable; native language freedom | Serialization latency, consistency/recovery, OS sandbox variance | Future high-risk/enterprise tier |
| No executable plugins, data only | Safest and simplest | Cannot express novel mechanics | Insufficient long term; valid first milestone |

## Evidence

### Paper and Folia

Paper defines explicit load/enable/disable lifecycle phases and warns that APIs are not available in every phase ([Paper plugin lifecycle](https://docs.papermc.io/paper/dev/how-do-plugins-work/)). This is direct evidence that initialization order and registry availability become public contracts. Paper plugins are JVM code loaded into the server, so their API discipline is operational rather than a containment boundary.

Folia's primary documentation is stronger evidence about ownership. There is no universal main thread; regions own chunk/entity state, plugins must schedule against the region/entity owner, and cross-region access can corrupt data ([Folia repository](https://github.com/PaperMC/Folia)). The architecture exposes separate region, entity, global, and task queues, and warns against giving plugins region-local internals in sensitive callback paths ([Folia overview](https://docs.papermc.io/folia/reference/overview/)).

Engineering conclusion: VibeCraft should not implement region ticking in v1, but it should expose `OwnerContext` plus command/scheduler APIs now. A plugin written against “grab any chunk and mutate it from any callback/thread” would make later ownership impossible without Paper-to-Folia-scale breakage.

### Luanti

Luanti demonstrates that a voxel engine can sustain a broad namespaced server mod API and per-mod storage, but it also distinguishes ordinary sandboxed mods from operator-trusted mods allowed HTTP/insecure environment access ([Luanti HTTP API](https://docs.luanti.org/for-creators/api/http-api/), [server security guidance](https://docs.luanti.org/for-server-hosts/setup/)). Its security history includes multiple sandbox and access-control bypasses ([Luanti advisories](https://github.com/luanti-org/luanti/security)).

The useful lesson is two-sided: server-side scripting is a proven product model, and every host escape hatch becomes a permanent security maintenance burden. VibeCraft will not provide a general “insecure environment” capability to sandboxed modules; an operator needing unrestricted code must maintain a private fork rather than receive a supported plugin escape hatch.

### Veloren and Wasm

Veloren's plugin guide uses Wasm and an event-driven host interface and calls the API experimental ([Veloren plugin guide](https://book.veloren.net/contributors/modders/writing-a-plugin.html)). Its architecture/survey evidence shows that a shared headless server can expose a Wasm boundary without coupling extensions to rendering. This supports the mechanism, not copying its API or assuming its safety properties.

Wasmtime states that Wasm code has no outside I/O except linked imports and supplies fuel/epoch interruption for runaway code ([security](https://docs.wasmtime.dev/security.html), [interruption](https://docs.wasmtime.dev/examples-interrupting-wasm.html)). Its advisory record proves that the runtime itself remains a patchable security dependency ([advisories](https://github.com/bytecodealliance/wasmtime/security)). `MOD-01` owns the .NET host/tooling gate; this decision assumes that gate passes before sandboxed plugins ship.

### Private native forks

Microsoft explicitly says `AssemblyLoadContext` has no security features and loaded code receives full process permissions ([API documentation](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext)). Unload is cooperative and can be prevented by plugin threads, callbacks, statics, or retained references ([unloadability](https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability)).

Direct conclusion: native server code is equivalent to changing server software. A private fork may choose its own loader, but VibeCraft cannot promise a public native-plugin compatibility or containment contract.

## Proposed architecture

### Extension tiers and trust

```text
data_pack
  registration/configuration only; validated before world open

sandbox_component
  the single public executable API; capability-limited, metered, no ambient authority;
  one principal/runtime instance and isolated storage/queues per module

private fork/native patch
  outside the public package/API contract; no compatibility, containment, or
  multiplayer-distribution promise
```

Artifacts are class-separated: `.vcpak` is resource-only; a future `data_pack` has its
own declarative parser; the eventual public component artifact has bounded metadata
and one selected sandbox-runtime payload. A sandbox component that needs art/data uses
those separate artifacts. Client and server components use distinct host contracts,
host-created principals, and grant sets, and never share memory or an implicit private
channel. Communication uses bounded server-defined messages or ordinary authoritative
state.

The dedicated server never downloads or executes native code from clients. A private
fork may of course modify its own process, but that is not a VibeCraft extension
mechanism and cannot be required for joining a normal server.

### Lifecycle and registration

```text
discover/validate package bytes
  -> resolve dependencies, content lock, ABI, grants, quotas
  -> instantiate sandbox runtime instances
  -> bootstrap (declare migrations and registrations)
  -> register namespaced content, commands, handlers, schedules
  -> freeze registries and handler order
  -> validate saved required namespaces and run migrations
  -> open world
  -> runtime callbacks and logical schedules
  -> quiesce (reject new plugin work)
  -> finish/discard callbacks at boundary
  -> save barrier
  -> invalidate handles/cancel work/disable
  -> dispose runtime; native unload is best effort
```

Rules:

- Discovery parses manifests and validates Wasm imports/proposals/limits before any guest code runs.
- Bootstrap/registration receive no live world handles. Content declarations are data copied and validated by the host.
- Registry IDs are namespaced persistent identifiers. Registries freeze before world open; executable hot reload and runtime type replacement are not supported in v1.
- Handler order is dependency topological order, then declared phase, then stable namespaced package/handler ID. Worker completion, dictionary order, install path, and wall clock never choose gameplay order.
- A module cannot add handlers/schedules during disable or retain a host object across re-instantiation.
- Required package or migration failure aborts world open before any writable simulation begins.

### Callback, event, and command contract

Each invocation is an isolated transaction-like unit:

```text
CallbackEnvelope {
  event_id
  world_tick
  owner_context
  event_sequence
  immutable payload
  allowed output classes
}

CallbackResult {
  policy_result?       // only for explicitly cancelable/transformable events
  commands[]           // typed staged mutations
  schedules[]          // host-owned future work
  messages[]           // bounded presentation/admin output
  storage_writes[]     // own namespace only
}
```

- Input contains immutable bounded values and opaque generational resources, never live component stores, chunks, database connections, network sessions, or server objects.
- `Before*` policy events exist only where cancellation/transformation is a product contract. Their output is a small typed decision, not a mutable event object.
- `After*` events are facts. They cannot rewrite the committed operation; their commands run at the next documented barrier and may fail preconditions.
- Hot inner loops—every voxel read, collision cell, mesh face, AI primitive, packet, light sample, or movement substep—are not extension events.
- Bulk events replace per-object polling: section activated, entity batch opportunity, inventory transaction proposed/committed, scheduled callback due.
- Callback-created effects are staged. Before live mutation, the owner validates the **entire** batch against one immutable revision, reserves every resource/quota, and guarantees the selected commit primitives cannot fail. A trap, stale precondition, conflict, or invalid command rejects the whole batch before mutation. No external I/O, nested plugin callback, packet send, or irreversible side effect occurs during apply; `After*` events and presentation messages are emitted only after commit.
- Built-in world mutation occurs only after validation on the world owner. Events caused by the commit enqueue for a later documented phase; callbacks do not recursively reenter themselves.
- Command results are explicit on a future event or return channel: accepted, denied, stale revision, wrong owner, target missing, capacity, invalid state, or module disabled.

### Ownership and scheduler

The public API exposes ownership even though one thread owns a v1 world:

```text
OwnerContext = WorldOwner(world_id)
             | LocationOwner(dimension_id, section_key, generation)
             | EntityOwner(entity_handle, generation)
             | GlobalOwner(server_id)
```

- A callback may synchronously read only the bounded immutable view supplied for its owner and explicitly granted nearby query pages.
- World/entity/inventory commands name their target owner and revision. Cross-owner commands enqueue to the target owner; they do not synchronously lock or mutate another owner.
- `Schedule(owner, due_world_tick, callback_id, payload)` creates host-owned durable/ephemeral scheduled work with package/world lifetime and quota accounting. Plugins never capture a CLR delegate or mutable object as scheduled state.
- Asynchronous network/I/O threads never invoke a plugin with live world authority. They enqueue validated immutable events to the owner.
- Sandboxed modules cannot create threads. Trusted native plugins are contractually forbidden from touching world state off-owner; analyzers/debug checks can detect some misuse, but malicious native code can bypass them.
- Background pure jobs are deferred for sandboxed plugins in v1. When added, they receive a bounded immutable snapshot and return a revisioned command proposal; timeout/completion order cannot determine authoritative sequence.

This shape follows the useful part of Folia—owner-aware scheduling—without implementing dynamic ticking regions before `WORLD-08`'s profile gate.

### Server capabilities

`MOD-02` defines the vocabulary. The initial server host links only granted subsets of:

- registration: own-namespace content and command declaration during bootstrap;
- immutable reads: event payload, bounded owner-local section/entity/player/inventory views;
- validated commands: block/entity/inventory/game-rule operations explicitly allowed by capability and server policy;
- events/schedules: named subscriptions and logical-time callbacks at bounded rates;
- messaging: attributed bounded chat/UI/presentation output, never raw packets;
- persistence: own namespaced world/player/entity storage and own migration functions;
- deterministic utilities: logical tick and named deterministic RNG streams.

No sandboxed server plugin receives raw filesystem, SQL, arbitrary sockets/HTTP,
process/environment, native code, wall clock, OS entropy, Godot/client objects, packet
construction, or an insecure-environment escape. Future external-service integration
must use a destination-limited broker with separate credentials, timeouts,
request/response caps, and nondeterminism semantics.

### Provisional server budgets

These defaults are policy/configuration starting points, not stable ABI constants:

| Resource | Per sandbox module | Aggregate/default behavior |
| --- | ---: | --- |
| Wasm executable bytes | 16 MiB | Reject before compile when larger |
| Linear memory | 64 MiB declared maximum | 512 MiB across modules per server process |
| Wasm stack | 1 MiB | Runtime-enforced |
| Input/output bytes | 512 KiB each per callback | Larger reads must page; checked before allocation |
| Live handles | 4,096 | Generation/lifetime checked; zero after disable |
| Host calls | 1,000 per callback | Additional fuel/work charge by bytes/entities/cells |
| Staged commands | 1,024 per callback; 8,192 queued per module | Reserve before mutation; no partial overflow |
| Subscriptions/schedules | 1,024 / 4,096 | Package/world lifetime and storage charged |
| Plugin world storage | 16 MiB base per world | Larger named quota explicitly approved by operator |
| Per-player plugin storage | 64 KiB/player/module, 64 MiB aggregate default | Sparse and paginated; configurable with operator approval |
| Logs | 64 KiB/minute, 16 KiB burst | Sanitized and suppression-attributed |

Fuel is deterministic and charged to Wasm instructions plus host work. Calibrate it on the declared baseline server so an ordinary callback budget is **0.5 ms p95** and an explicitly classified bulk callback budget is **2 ms p95**. A module may consume at most the calibrated equivalent of **2 ms per 20 Hz world tick**, and all sandboxed modules together target **5 ms p95**. Deferrable observer work is postponed when the aggregate budget is empty; authoritative policy work fails closed according to its event contract.

Each invocation also gets a **5 ms epoch wall-clock trap deadline**. The wall clock is a runaway backstop, not authoritative scheduling and not an excuse for blocking host calls. Host functions must complete in bounded memory/time without disk/network waits. If server tick p99 cannot stay below 50 ms with these budgets, reduce plugin quotas/event frequency or move optional work; do not make authoritative processing stopwatch-dependent.

Trusted native plugin time, allocations, events, and commands are measured and attributed but not securely capped. .NET has no safe general way to abort arbitrary managed code at a deadline. A native callback that hangs may require the process watchdog to terminate the server and recover from the last durable save.

### Persistence and migrations

- The host owns persistence. Modules use records scoped by host-created `PrincipalId`, component/side, and world/player/entity scope through `server.plugin-storage`; they never receive SQLite connections/paths or inherit data merely by reclaiming a package name.
- `IPluginDataStore` is an operator-configured host service. Its provider may place a
  principal in a separate database, schema, or logical namespace, but the plugin sees
  only bounded host operations and never a connection string, SQL dialect, or another
  principal's rows. The host may use one configured SQL service for many principals
  without letting one plugin's storage choice become a core-world dependency.
- Runtime writes stage with the callback and join the same prevalidated in-memory commit revision. `WORLD-04` later persists world/plugin records from that revision atomically; its database transaction is not assumed to roll back already-mutated live memory.
- Each record declares package namespace and schema version. Payload and record count/bytes are capped; iteration is paginated.
- Migrations register during bootstrap, run before world publication, access only the package namespace, and use stricter fuel/time/output quotas. They cannot perform network I/O or mutate unrelated world state.
- Missing modules preserve bounded opaque data under `WORLD-09`. If a missing module is required for authoritative records, world open fails with a useful diagnostic; VibeCraft never silently drops mechanics/data.
- Module removal/cleanup/export is an explicit operator tool and uses crash-safe transactions/backups for bulk changes.

### Failure and disable policy

Sandboxed modules:

- Manifest/import/ABI/grant/registration/migration failure: reject before world open. Required module blocks open; optional module is disabled with diagnostics.
- Trap, exception, fuel/deadline exhaustion, malformed output, stale/forged handle, or quota violation: discard the full invocation result and storage transaction.
- Ordinary optional module: disable after three faults in 60 seconds; security violations or deliberate quota storms disable immediately. Continue only if the module owns no required authoritative behavior.
- Required authoritative module: pause new world ticks after discarding the failed invocation, reject new joins/actions, execute a save barrier for the last fully committed state, then stop that world/server with an explicit reason. Do not continue with missing mechanics or commit half an event.
- Disable cancels subscriptions/schedules, invalidates all handles, rejects later queued completions, discards open outputs/storage, and disposes the instance/store.

Trusted native modules:

- Load only at startup from an operator-controlled directory/allowlist; no server/client auto-download.
- Use a collectible `AssemblyLoadContext` for dependency separation, but do not promise safe runtime unload or containment.
- Catch top-level callback exceptions for attribution where possible. A native plugin can already have mutated arbitrary state or escaped the API, so “disable and continue safely” is not guaranteed.
- A hung callback triggers watchdog diagnostics and then process termination according to operator policy. Recovery uses crash-safe persistence; thread abort is not attempted.
- Mark the server/session as running trusted native code in diagnostics. Publisher signatures are provenance, not safety.

## Threat model and non-goals

Protected from sandboxed plugins:

- authoritative world invariants and deterministic owner ordering;
- host filesystem/network/process/environment and server credentials;
- other module namespaces, handles, grants, queues, and persistence;
- raw client sessions/packets and data outside granted read models;
- bounded tick CPU, process memory, queues, logs, disk, and host allocations.

The Wasmtime runtime, generated bindings, plugin host, core simulation validators, package parser, and persistence engine are trusted. A vulnerability in them can breach the boundary. Security updates and defense-in-depth remain mandatory.

Non-goals:

- containing an operator-approved in-process native plugin;
- making arbitrary blocking Internet/database integrations deterministic;
- exposing every engine hook or guaranteeing compatibility with Bukkit/Fabric/Forge mods;
- hot-reloading executable gameplay code;
- region-parallel ticking in v1;
- preserving a world after a required plugin disappears without an explicit compatibility/migration path.

## Public API shape

The exact WIT files remain an implementation artifact of the prototype, but the semantic surface is fixed by this decision:

```text
register-content(declarations) -> list<registration-result>
register-handler(event-id, callback-id, phase) -> result
register-command(command-definition, callback-id) -> result

read-section(section-resource, query-page) -> result<section-view-page, host-error>
read-entities(owner-resource, query-page) -> result<entity-view-page, host-error>

submit-world-command(owner-resource, world-command) -> result<staged-id, host-error>
submit-inventory-transaction(owner-resource, inventory-command) -> result<staged-id, host-error>
schedule(owner-resource, due-tick, callback-id, payload) -> result<schedule-id, host-error>

storage-get(key) -> result<option<bytes>, host-error>
storage-put(key, schema-version, bytes) -> result<staged-id, host-error>
```

Every list/bytes/query is bounded or paginated. Every handle is opaque/generational and callback- or instance-scoped. Every failure is a typed stable variant. ABI values contain stable namespaced IDs and explicit revisions, never runtime array order or CLR type names.

## Greenlight criteria

- One data pack and one sandboxed server component can add a block/item/recipe, command, scheduled behavior, world event reaction, and versioned saved value without receiving an engine object or direct mutable collection.
- All world/inventory/entity changes execute through validation on the owning simulation context; no network/I/O/worker callback mutates live world state.
- The selected sandbox runtime passes `MOD-01`/`MOD-02` import, interruption, memory, allocation, handle, and platform gates.
- Deterministic handler order and plugin RNG produce identical replay hashes across worker counts and randomized asynchronous completion.
- Trap/quota/invalid-command tests commit no partial command or storage result; a required-module failure saves/stops at the last committed boundary.
- Missing module data survives load/save byte-for-byte where `WORLD-09` permits, and migrations cannot access another namespace.
- Representative plugin load stays within the 5 ms p95 aggregate sandbox budget and the full `WORLD-08` target remains below 50 ms p99 on the declared baseline server.
- At least two first-party gameplay features use the public data/command/event/storage surface before ABI 1.0 is promised, as required by `MOD-03`.

## Prototype and test plan

Required: yes.

Build an engine-neutral C# server harness with the `WORLD-08` tick phases, a tiny `ARCH-02` world/entity/inventory model, in-memory transactional persistence, and the selected sandbox-host candidate. Implement one legitimate module plus an adversarial corpus.

Functional cases:

- register namespaced content before freeze and reject late/duplicate declarations;
- observe/cancel one explicit policy event, consume one immutable after-event, submit a cross-section command with revision, and receive its result;
- schedule durable logical-time work, save/reload it, migrate plugin state, disable/re-enable, and preserve data while absent;
- exercise one optional observer and one required authoritative module through graceful shutdown and crash recovery.

Security and correctness cases:

- attempt forbidden WASI/files/socket/HTTP/process/environment/clock/entropy/native imports;
- forge package/owner/entity/section handles, reuse stale generations, retain callback-borrowed resources, access another module's storage, and command an unloaded/wrong-revision owner;
- send invalid IDs/tags/UTF-8, NaN/infinity, coordinate/length integer overflow, oversized query/output/storage values, and malformed command graphs;
- loop/recursively overflow, grow memory/table, flood host calls/events/commands/schedules/logs/storage, create event recursion, and trap after staging outputs;
- disable during callback, complete old work after re-instantiation, fail required module mid-tick, kill during save/migration, and reopen with a missing/future-version module;
- randomize worker completion, network ingress, and hash/dictionary insertion order while replaying the same accepted external inputs;
- run native test plugins that open a socket/read a file, retain a thread preventing unload, throw, and hang, verifying that only diagnostics/watchdog—not sandboxing—is claimed.

Performance matrix:

- 0/8/32 sandboxed modules;
- 0/100/1,000 callbacks per world tick with realistic batched payloads;
- 1/4/8 separated players and the representative `WORLD-08` entity/chunk fixture;
- cold compile/instantiate, warm callbacks, save/reload/migration, and 30-minute soak;
- Windows/Linux/macOS x64 and arm64 for each supported dedicated-server target.

Pass conditions:

- Escape attempts observe no denied OS/cross-module resource and do not crash or permanently stall the server.
- CPU, memory, queue, handle, log, and disk storms remain within configured ceilings; global overload never silently drops authoritative work.
- Twenty seeded replays produce identical committed event/command/storage/state hashes.
- Optional failure leaves the server consistent; required failure exposes no half-commit and reopens from the last valid durable state.
- No mutable server object, database connection, socket, Godot type, CLR reflection object, or raw packet builder crosses the sandbox ABI.

## Risks and unresolved questions

- Wasmtime .NET Component Model/resource-limit support and security-release lag are unresolved until `MOD-01`'s spike. A missing enforceable limit blocks the sandbox claim.
- C# guest components may have larger startup/memory needs than Rust/AssemblyScript guests; the initial server SDK may need to support fewer languages.
- A synchronous required policy hook adds latency and coupling. Keep the cancelable event catalog tiny and prefer declarative rules or prevalidated command handlers.
- Aggregate deterministic fuel controls work but cannot guarantee a 50 ms tick on every CPU. Minimum hardware and reference calibration must be published.
- Host-call validators become security-critical and can contain confused-deputy or TOCTOU bugs even when Wasm isolation is perfect.
- Future HTTP/database integrations introduce nondeterminism, credentials, SSRF, and blocking. They need a broker/async result model not specified here.
- Future region simulation will require real cross-owner queues and ownership migration. The proposed API preserves the seam but does not prove that migration.
- A native plugin marketplace could imply safety the runtime cannot deliver. Native distribution/provenance needs separate product policy.

## Dependencies

- Requires: `FOUNDATION-00`, `ARCH-01`, `ARCH-02`, `WORLD-04`, `WORLD-08`, `WORLD-09`, `MOD-01`, `MOD-02`.
- Coordinates with: `MOD-03`, `NET-09`, `GAME-01` registries, `NET-07` protocol capabilities, `WORLD-05` lifetimes/leases, administration and observability.
- Blocks: final `MOD-03` API stabilization, public server SDK, stable plugin ABI, modded-world startup/recovery, and plugin marketplace/distribution policy.

## Rejected or deferred alternatives

- Native .NET as the sandboxed public tier: rejected.
- `AssemblyLoadContext`, CAS, analyzer rules, or permission manifests as containment: rejected.
- Direct mutable engine/world/entity/database/network/Godot object access: rejected.
- Arbitrary plugin-created threads touching world state: rejected.
- General insecure-environment/filesystem/network capability: rejected for sandboxed v1.
- Supported runtime patching/mixins: rejected.
- Executable hot reload: deferred.
- Sandboxed background pure jobs: deferred until the synchronous command/event baseline is profiled.
- Out-of-process native plugins: deferred as a possible high-risk integration tier.
- Folia-style parallel region ticking: deferred behind `WORLD-08`'s measured migration gate.
