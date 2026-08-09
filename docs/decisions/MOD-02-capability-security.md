# MOD-02 Capability and extension security model

Status: Proposed

Owner: VibeCraft architecture research
Date researched: 2026-08-09
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Use deny-by-default, versioned, parameterized capabilities enforced at every host call and paired with independent resource quotas; grant them only to sandboxed Wasm principals, while classifying native .NET extensions as unrestricted trusted code outside this model.

One-sentence rationale: A capability is useful only when possession is unforgeable, scope is narrow, every use is validated, and authority cannot be recovered through another broad API.

Capability names are not user-facing promises by themselves. The security boundary consists of all of the following together:

1. a Wasm runtime with no ambient imports;
2. a host-created module principal and immutable grant set;
3. narrow typed host functions;
4. semantic validation and ownership checks at use time;
5. CPU, memory, call, output, queue, disk, and handle quotas;
6. transactional publication and failure/disable policy;
7. security testing and runtime patching.

## Context and trust boundaries

VibeCraft has three different security relationships:

| Relationship | What is protected | What is not claimed |
| --- | --- | --- |
| Local user vs sandboxed client mod | User machine, client availability, other mods/profile data, tokens not granted | Honest play, secrecy of replicated world data, protection from native plugins |
| Server operator vs sandboxed server plugin | Server process, world integrity, other plugin namespaces, availability within quotas | Protection after a runtime/host escape; safety of trusted native plugins |
| Public server vs any client | Authoritative world/inventory/combat state through protocol validation | Client binary integrity, truthful mod reporting, prevention of bots/x-ray from delivered data |

The adversary may control a package and all of its Wasm state, arguments, persistent bytes, timing, and requested capabilities. It may collaborate with another module or a malicious server/client. The package parser, Wasmtime, generated ABI bindings, VibeCraft host functions, capability resolver, persistence layer, and OS are trusted and therefore must be kept small and tested.

Native .NET code is a fourth relationship: the installer/operator has chosen to trust it with the entire process identity. A native plugin may voluntarily use the same high-level API and may declare capabilities for compatibility/documentation, but enforcement cannot be claimed.

## Options considered

| Option | Strengths | Costs and failure modes | Fit for VibeCraft |
| --- | --- | --- | --- |
| One boolean `trusted` flag | Simple UI/configuration | No least privilege; one useful feature grants everything | Reject |
| Flat permission strings checked at load only | Easy manifests | TOCTOU and confused-deputy bugs; no resource/owner scope; host APIs can bypass checks | Reject |
| Java/.NET-style in-process permission sandbox | Familiar native languages | Modern .NET does not enforce CAS/partial trust; native calls escape | Reject |
| Parameterized object capabilities plus quotas | Least authority; maps to WIT resources; testable | More host design and policy UX | **Recommend** |
| One OS process/container per extension | Strong kill/isolation boundary | Platform-specific policy, IPC overhead, deployment complexity | Defense-in-depth option, not v1 baseline |

## Evidence

WASI's stated model is no ambient authority: a module/component can act only through capabilities explicitly provided by its host ([WASI introduction](https://wasi.dev/)). WIT resources model non-copyable host entities as owned/borrowed handles, which is a useful representation for scoped authority but does not prescribe application validation ([WIT resources](https://component-model.bytecodealliance.org/design/wit.html#resources)). Wasmtime confirms that Wasm can interact with the outside world only through linked imports; its filesystem support is capability-oriented ([Wasmtime security](https://docs.wasmtime.dev/security.html)).

Wasmtime's resource limiter documentation warns that limiting Wasm linear memory is not the same as limiting all memory: runtime metadata and embedder allocations remain outside that counter. CPU needs separate fuel/epoch controls ([`ResourceLimiter`](https://docs.rs/wasmtime/latest/wasmtime/trait.ResourceLimiter.html), [interrupting execution](https://docs.wasmtime.dev/examples-interrupting-wasm.html)). Engineering conclusion: every VibeCraft host call and queue must have its own byte/count/work budget; a memory-page limit alone is insufficient.

Microsoft states that `AssemblyLoadContext` gives loaded assemblies full process permissions and that modern .NET does not support Code Access Security as a partially trusted boundary ([`AssemblyLoadContext`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext), [secure coding guidelines](https://learn.microsoft.com/en-us/dotnet/standard/security/secure-coding-guidelines)). This directly rules out enforcing the capability model over arbitrary in-process C# code.

Luanti allows selected mods to request HTTP or an insecure environment through operator allowlists and warns server operators never to disable mod security or trust unknown entries ([HTTP API](https://docs.luanti.org/for-creators/api/http-api/), [server setup](https://docs.luanti.org/for-server-hosts/setup/)). Its advisory history includes repeated insecure-environment, HTTP access-control, and sandbox-escape defects ([Luanti advisories](https://github.com/luanti-org/luanti/security)). The lesson is to avoid a broad “insecure environment” escape hatch in the sandbox tier and test every authority path continuously.

Folia makes spatial ownership observable to plugins because work must execute on the region/entity owner; accessing another region's state can corrupt data ([Folia repository](https://github.com/PaperMC/Folia), [architecture overview](https://docs.papermc.io/folia/reference/overview/)). VibeCraft is single-writer in v1 under `WORLD-08`, but capability scopes and commands should still name an owner so future regionization does not turn a previously broad plugin API into concurrency debt.

## Capability model

### Principal, request, grant, and use

```text
Package manifest request
        + local user/server policy
        + side and content-lock policy
        + host ABI support
        -> immutable resolved grants
        -> host-created ModulePrincipal in one Wasmtime Store
        -> linked interfaces/resources
        -> per-call grant + scope + ownership + quota + semantic validation
```

The manifest requests authority; it never self-grants. A request is resolved before instantiation:

```text
CapabilityRequest {
  id: namespaced capability ID
  min_version
  max_version
  scope_parameters
  reason
  required: bool
}

CapabilityGrant {
  id
  selected_version
  normalized_scope
  quota_class
  policy_revision
}
```

Rules:

- Capability IDs omit embedded versions (`core.log`, not `core.log@1`); requested/selected semantic versions are separate fields. Never reuse a name/version for broader authority.
- Unknown required capability rejects load/join. Do not model optional authority as unresolved WIT imports: the host always fulfills one small mandatory capability-broker/lifecycle interface, and `open-capability(id, requested-scope)` returns a typed resource/grant or stable denial from the already approved grant set. Alternatively publish a small finite set of complete WIT worlds.
- A grant is immutable for an instance. Changing grants disables/reinstantiates the module after a lifecycle barrier; host functions do not race mutable permission sets.
- The `ModulePrincipal` is a host-created `PrincipalId`/component/side/update-lineage record attached to the Wasmtime `Store`. Host calls obtain it from caller context, not from guest-supplied package names. `PackageId` alone never grants storage inheritance.
- Capability resources are non-transferable across instances unless an interface explicitly brokers a narrowed derived capability. Raw numeric handle copying grants nothing.
- Package signatures/provenance and content hashes are separate from authority. Signed malware remains malware; byte agreement remains only agreement.

### Capability vocabulary

The initial vocabulary is deliberately narrower than the long-term mod wish list.

Common sandbox capabilities:

| Capability | Scope | Semantics |
| --- | --- | --- |
| `core.log` | severity and byte rate | Structured, sanitized attributed logging |
| `core.schedule` | max timers, minimum delay, owner class | Future logical-time callbacks; no threads |
| `core.random` | named deterministic stream | Host RNG scoped by package/world/session; no OS entropy |
| `storage.kv` | own namespace and byte/count quota | Transactional namespaced key/value records |
| `events.subscribe` | explicit event IDs and rates | Bounded immutable event batches |

Client-only capabilities:

| Capability | Default scope | Important restriction |
| --- | --- | --- |
| `client.self.read` | local predicted/confirmed player views | No session secret or mutable object |
| `client.visible-world.read` | state already replicated and within client interest | Does not bypass fog/interest; cannot promise anti-x-ray |
| `client.ui.owned` | module-owned subtree, widget/type/count limits | Cannot inspect or mutate another module/base UI except named extension slots |
| `client.presentation.emit` | particle/audio/decal classes and rates | Host validates asset IDs, distance, lifetime, and GPU budgets |
| `client.input.intent` | named game actions | Produces ordinary local input/action intents; no synthetic authoritative result |
| `client.settings` | own declared settings | No arbitrary config file access |

Server-only capabilities:

| Capability | Default scope | Important restriction |
| --- | --- | --- |
| `server.registry.declare` | own namespace during registration | Frozen before world open; no runtime replacement |
| `server.world.read` | callback owner plus explicit bounded query area | Immutable snapshots only; unloaded chunks are not synchronously forced in |
| `server.world.command` | command kinds, dimension/owner policy | Staged validated commands with revisions; no direct mutation |
| `server.entity.read` | event entity/owner and bounded query | Opaque generational handles and immutable views |
| `server.entity.command` | permitted entity command kinds | Executes on owner and revalidates existence/permissions |
| `server.inventory.command` | explicit inventories/transaction kinds | Atomic transaction; no raw slot-array access |
| `server.command.register` | own namespaced commands | Host parses permissions, sizes, rate, and output |
| `server.message.emit` | recipients/channel/size/rate | No raw packet construction |
| `server.plugin-storage` | own world/player/entity namespace | `WORLD-09` schema and preservation rules |

Not available to sandboxed modules in v1:

- arbitrary filesystem paths or directory preopens;
- arbitrary outbound/listening sockets, DNS, HTTP, or server packet construction;
- environment variables, process spawn/exit, native libraries, devices, clipboard, terminal, or OS credentials;
- wall-clock time, nondeterministic OS entropy, guest threads, or shared memory;
- reflection into host types, Godot nodes/resources, raw database handles, mutable chunk/component arrays, or another module's storage;
- an “unsafe,” “all APIs,” or Luanti-style insecure-environment capability.

If a later feature genuinely requires HTTP or user-selected files, add a separate brokered interface with destination/path handles selected by the user/operator, request/response byte and time limits, redirect/DNS/private-address policy, and an independent threat review. Do not widen `core.*`.

### Authority derivation and attenuation

A callback receives only resources relevant to its event—for example a borrowed player handle for `player-joined`, not a global player lookup capability. From a broad grant, the host may derive a narrower resource such as `SectionView(section, revision, read-only, expires=end-of-callback)`. A guest cannot broaden it.

Every host operation validates:

1. calling `ModulePrincipal` and selected ABI/capability version;
2. resource belongs to that principal and its generation/lifetime is current;
3. operation lies within normalized scope and event/owner context;
4. argument shape, finite/range/length/UTF-8/enum/ID constraints;
5. target exists and revision/preconditions still hold;
6. simulation/game permissions permit the operation independently of mod capability;
7. operation, byte, allocation, and queue quotas can be reserved atomically;
8. result exposes only the minimum data defined by that interface.

Capabilities do not bypass gameplay rules. For example, `server.world.command` permits submitting `TrySetBlock`; it does not make every set legal. Client `input.intent` cannot generate elapsed simulation time or claim a hit. Administrative authority is a separate server role checked after module authority.

## Quotas and accounting

Quotas are attached to the principal and charged before work/allocation. Limits compose rather than substitute for one another:

- **Wasm execution:** fuel per callback and aggregate per tick/frame; epoch wall-time watchdog.
- **Wasm memory:** declared linear-memory/table/stack/instance limits and forbidden proposals.
- **Host allocation:** decoded input, returned lists/strings, temporary snapshots, UI objects, particles, command buffers, and persistence buffers.
- **Calls and effects:** host calls, events, commands, subscriptions, timers, handles, logs, network messages, and renderer publications.
- **Durable storage:** total bytes, keys/records, value size, migration output, player/entity cardinality.
- **Backlog:** event/output/schedule queue count and bytes with deterministic coalescing/drop/fail semantics.

Default numeric limits live in runtime policy rather than the ABI. `MOD-01` sets client starting values. `ARCH-05` sets server starting values. A package may request a named larger quota class; the user/operator approves it separately from semantic capabilities. Modules cannot observe exact host machine capacity, only their resolved limits and stable quota-exceeded results.

Host functions must never allocate based only on a guest-provided length. Validate against capability and remaining byte budget, use checked arithmetic, reserve quota, copy/parse, then perform bounded work. Charge expensive calls by a documented work unit (bytes, cells, entities, UI nodes, result rows), not one flat “host call.”

### Overload behavior

- Read/event result too large: return a stable `result-too-large` with pagination/continuation where the API defines it; never return a partial structure that looks complete.
- Command/output quota unavailable: reject before staging dependent work. A transaction is all-or-nothing.
- Event queue full: coalesce only event types whose contract explicitly allows it; otherwise disable an optional observer or fail a required module according to role. Never grow without bound.
- Fuel/deadline exhausted: trap the whole callback and discard staged outputs/storage.
- Persistent quota exceeded: return typed failure without deleting old values.
- Global plugin budget exhausted: defer only callbacks declared deferrable; authoritative pre-commit policy callbacks must fail the initiating operation or trigger required-plugin failure, never silently approve.

## Ownership, scheduler, and command rules

`WORLD-08` uses one deterministic world writer in v1. The public extension API still models ownership explicitly:

- A world/entity callback carries an `OwnerContext` and logical `WorldTick`.
- Read resources are valid only for their documented owner/revision and callback lifetime.
- Mutations are commands to the current or named target owner. The host applies them at a deterministic barrier after validation.
- Cross-owner operations are messages/commands with revision preconditions, even though one thread can optimize them internally in v1.
- Scheduled work names a world/entity/location owner and due logical tick; it does not capture a mutable object or thread.
- Background work, if added, receives immutable bounded data and can return only a versioned command proposal. Completion order never becomes authoritative gameplay order.
- Client callbacks execute on a mod executor and publish presentation commands at a main-thread boundary; they never call Godot directly.

This keeps the capability vocabulary compatible with a future region scheduler without promising Folia-style live parallel mutation now.

## Persistence security

- The host owns all database/filesystem access. A module sees only namespaced records through `storage.kv`/`server.plugin-storage`.
- Core keys include host-created `PrincipalId`, component side/id, and explicit scope (world/profile/player/entity); the guest cannot choose a raw SQL table, filesystem path, another principal, or storage lineage.
- Keys, values, record count, total bytes, query result size, and migration output are bounded. Iteration is paginated with opaque expiring cursors.
- Writes stage with callback commands. Before mutating live state, the owner validates the **entire** batch against one immutable revision, reserves all quotas/resources, and guarantees the selected commit primitives cannot fail; any stale precondition/conflict rejects the batch before mutation. No external I/O, nested callback, packet send, or irreversible effect occurs during apply. Post-commit events/messages are emitted afterward. `WORLD-04` then preserves world/plugin records from the same committed revision; its database transaction is not a rollback mechanism for already-mutated memory. Cosmetic client storage may use its own atomic transaction.
- Records contain a package schema version and opaque payload; never deserialize an assembly-qualified CLR object graph.
- A migration can read/write only its namespace, uses stricter fuel/time/output quotas, and has no network/world mutation capability.
- Missing modules and unknown versions preserve bounded opaque data under `WORLD-09`; explicit cleanup is required.
- Corrupt or malicious records fail closed with attribution/quarantine. They are not replaced silently with default gameplay state.

## Failure, revocation, and audit

Grant revocation occurs by disabling the instance at a safe boundary, invalidating every resource generation, cancelling schedules/subscriptions, discarding queues and open transactions, and constructing a new instance if policy permits. V1 does not mutate grants in place or promise executable hot reload.

Audit records include package hash, ABI, resolved grants/scopes/quota class, callback/event ID, logical owner/tick, fuel/host-work/bytes, staged command outcomes, traps/quota failures, disable reason, and runtime build. Do not log secret values or full mod storage. Logs and metrics themselves are bounded.

Failure roles:

| Module role | Load/runtime failure |
| --- | --- |
| Optional cosmetic client | Disable and continue |
| Server-required client gameplay/presentation | Refuse join or disconnect |
| Optional server observer/tool | Disable; authoritative state continues |
| Required authoritative server module | Abort world open, or stop at safe tick/save barrier; never continue with mechanics missing |
| Trusted native plugin | Best-effort diagnostics only; process/world may already be compromised |

## Greenlight criteria

- A written capability-to-WIT-interface matrix exists; every host import has an owner, scope, semantic validator, work formula, quota, failure code, and test owner.
- No sandbox world links ambient WASI filesystem/socket/HTTP/process/environment/terminal/time/entropy interfaces.
- Module identity is host-derived and cross-module/stale/expired resources fail for every host call.
- Every guest-controlled byte/count/length has checked arithmetic and a pre-allocation cap; fuzzing reaches all decoders and host validators.
- Client capabilities cannot bypass `ARCH-01`; server commands cannot bypass simulation ownership or gameplay/admin permission checks.
- Quota exhaustion is bounded and transactional; no test can create an unbounded event, command, log, UI/GPU, persistence, or handle queue.
- Native .NET is labeled unrestricted in manifest schema, CLI, logs, and UI and is excluded from sandbox guarantees.
- Missing/disabled modules preserve namespaced state according to `WORLD-09`, and required-module failures stop safely.

## Prototype and test plan

Required: yes.

Implement a minimal host with `core.log`, `core.schedule`, `core.random`, `storage.kv`, one client UI/read/intent interface, and one server world-read/world-command interface. Generate bindings from one WIT package and run a legitimate and adversarial module on both client and server harnesses.

Security test families:

- **Ambient authority:** attempt files, path traversal/symlinks, environment, sockets/DNS/HTTP, process exit/spawn, terminal escapes, native imports, reflection, clocks, entropy, and clipboard.
- **Confused deputy:** lie about package ID, pass another module's resource, reuse stale generations, retain callback-borrowed handles, use a server/admin handle in client scope, and invoke a capability after revocation.
- **Validation:** fuzz invalid UTF-8/tags, huge/negative lengths, integer overflow, NaN/infinity, invalid coordinates/registry IDs, duplicate IDs, stale revisions, unloaded owners, and unauthorized inventory/entity targets.
- **Availability:** infinite loops, deep recursion, memory/table growth, host-call loops, giant reads/results, timers/events/commands/handles/logs/UI/particles/storage storms, and traps after staging work.
- **Scheduling:** recursive events, callbacks completing after owner unload, cross-owner commands, disable during dispatch, deterministic ordering under randomized worker completion, and timer floods across save/restart.
- **Persistence:** key/value/count quotas, migration trap/timeout, corrupt payload, missing module, future schema, interrupted commit, and attempted access to another namespace.
- **Runtime supply chain:** run the corpus against every Wasmtime update before release and regression fixtures for applicable published advisories.

Pass conditions:

- Denied operations have no external side effect and a stable failure/trap classification.
- Resource forgery/reuse and policy revocation produce zero data disclosure or mutation.
- Each availability test remains within the configured CPU, memory, disk, handle, queue, and wall-time ceilings; base client/server remains responsive.
- Any callback failure discards all of its staged commands and storage writes.
- Twenty deterministic server replays with randomized asynchronous completion produce identical committed command/event/persistence hashes.
- Static review can trace every host import to one capability grant and validator; there is no generic host-object escape or “unsafe” capability.

## Risks and unresolved questions

- Capability APIs can be individually narrow yet compose into broader authority. Security review must consider combinations, especially world reads plus messaging/storage.
- Information-flow control is not promised. A mod may exfiltrate any data through a later network capability, so arbitrary networking remains out of v1.
- UI accessibility APIs, clipboard, user-selected files, and URLs need platform-specific consent/broker behavior not designed here.
- Exact per-call work formulas and quota defaults require prototype measurements and adversarial fuzzing.
- Component Model resources and .NET host bindings may not expose all limiter/ownership facilities uniformly; `MOD-01` owns that tooling gate.
- Future parallel region simulation will require owner-aware commands to become actual queues. The current API must not expose synchronous cross-owner assumptions.
- Sandboxing is a release/security-maintenance obligation. A stale runtime invalidates the safety claim even if the capability design is sound.

## Dependencies

- Requires: `FOUNDATION-00`, `ARCH-01`, `ARCH-02`, `WORLD-08`, `WORLD-09`, `MOD-01`.
- Coordinates with: `ARCH-05`, `MOD-03`, `NET-09`, `WORLD-04`, `NET-07`, asset/package manifests, UI/input policy.
- Blocks: public mod permission schema/UI, WIT host API, server plugin grants, client required-mod policy.

## Rejected or deferred alternatives

- Native .NET permission sandbox: rejected.
- Broad `unsafe`/insecure-environment grant in the sandbox tier: rejected.
- Capability checks only during package load: rejected; every use is validated.
- Raw SQL/filesystem/network/packet/Godot/engine-object access: rejected.
- Dynamic in-place grant changes and executable hot reload: deferred.
- Arbitrary HTTP and user filesystem access: deferred to brokered, separately reviewed capabilities.
- Per-module OS process: deferred as defense in depth for high-risk capabilities or platforms where in-process Wasmtime cannot meet the greenlight gates.
