# ARCH-04 Singleplayer server lifecycle and local transport

Status: Proposed

Owner: Architecture research sprint  
Date researched: 2026-08-09  
Related spec: [`../../design_doc.md`](../../design_doc.md)

## Decision

Recommended choice: Keep one host-agnostic `ServerCore` and ship desktop
singleplayer through a supervised child process using loopback gameplay transport. An
embedded host remains a conformance/fallback adapter using the same typed protocol
semantics, not a competing product topology. The child still must pass packaging,
startup, pause/save, orphan cleanup, native transport, and target-platform tests.

One-sentence rationale: The semantic boundary provides dedicated-server parity; the selected child host must demonstrate that its fault-isolation benefit justifies its packaging, startup, memory, and native-library costs on the actual target platforms.

The local process is **private singleplayer**, not an implicit listen/LAN server. Hosting other players is a later, explicit session mode with remote-server pause and authentication rules.

### Owner decision — 2026-08-10

The owner selected option **C** for Windows x64 and Linux x64. Singleplayer uses the
same authoritative server path as multiplayer; its meaningful product difference is
that the local supervisor may request a pause at an authoritative tick boundary.

## Context and constraints

- The client is Godot with C# bindings; the server is standalone C# and must also run headless.
- The current spec requires singleplayer to use a server under the hood and asks for crash-resistant saving and server plugins.
- Local and remote gameplay must not drift into two code paths.
- The client needs useful progress and cancellation while worlds, registries, plugins, and spawn chunks initialize.
- Closing a menu, closing the game, a client crash, an OS shutdown, and a server fault are distinct events.
- “Save complete” needs a durability contract from `WORLD-04`; merely queuing writes or observing process exit is insufficient.
- Pause must stop authoritative game time at a tick boundary while keeping lifecycle control responsive.
- Target platforms are an owner decision. The first topology spike should cover Windows x64 and Linux x64; macOS, mobile, web, and consoles are not shipping commitments until helper-process and native-library packaging is demonstrated for each.

## Options considered

| Option | Strengths | Costs and failure modes | Fit for VibeCraft |
| --- | --- | --- | --- |
| A. Special singleplayer simulation with no server | Fast startup, simplest debugger | Guaranteed rules/mod/save drift; contradicts the architecture goal | Rejected |
| B. Server library in the Godot process, separate thread, in-memory or loopback transport | Low memory and startup cost; easy sharing/debugging; used successfully by voxel engines | A server fault or unsafe plugin can kill/corrupt the client process; shutdown deadlocks share one runtime; in-memory transport can hide real-network bugs | **Prototype as fallback/counterfactual** |
| **C. Supervised child server + loopback gameplay transport** | Process isolation, real transport behavior, independent logs/exit status, explicit save barrier | Extra runtime memory; packaging and child supervision; localhost socket/security details | **Selected Windows/Linux desktop direction; acceptance-gated** |
| D. Ask player to launch a dedicated server manually | Maximum operational separation | Bad singleplayer UX; lifecycle and auth burden exposed to player | Rejected for singleplayer; retained as dedicated mode |
| E. Child server + bespoke local gameplay IPC | Process isolation without UDP overhead/firewall behavior | Two transport implementations and different packet behavior; local-only bugs | Rejected; a control pipe is allowed, gameplay IPC is not |

## Evidence

### Minecraft

**Minecraft: Java Edition 1.20.4/1.21.9 — mapped implementation evidence.** `IntegratedServer` inherits `MinecraftServer`, carries client/pause/LAN fields, overrides `tick`, and exposes shutdown/synchronous chunk-write behavior; newer mappings expose `isPaused`. This is strong evidence that modern Java singleplayer hosts the server abstraction rather than a separate gameplay model ([Yarn 1.20.4 `IntegratedServer`](https://maven.fabricmc.net/docs/yarn-1.20.4%2Bbuild.3/net/minecraft/server/integrated/IntegratedServer.html), [Yarn 1.21.9 `IntegratedServer`](https://maven.fabricmc.net/docs/yarn-1.21.9%2Bbuild.1/net/minecraft/server/integrated/IntegratedServer.html)). Mojang's official player help confirms an existing world can be opened to LAN, showing that local and network-host modes are user-visible lifecycle states ([official LAN guide](https://help.minecraft.net/hc/en-us/articles/4410317081741-Play-Minecraft-Java-Edition-on-a-Local-Area-Network-LAN)).

The mapped `MinecraftServer.stop(waitForShutdown)` documentation warns that waiting from the server thread deadlocks ([Yarn 1.21.6 `MinecraftServer.stop`](https://maven.fabricmc.net/docs/yarn-1.21.6%2Bbuild.1/net/minecraft/server/MinecraftServer.html)). **Inference:** embedding is workable, but lifecycle calls must have explicit thread ownership; a separate process makes this boundary harder to violate accidentally.

What to retain from Minecraft:

- one server simulation for local and dedicated play;
- an explicit pause state owned by the server host;
- graceful stop that waits for server save work.

What not to copy blindly:

- direct client-object coupling inside the integrated server;
- treating “open to LAN” as a small toggle on a private session;
- synchronous shutdown calls whose legality depends on the calling thread.

### Luanti

**Luanti current engine documentation and master source inspected 2026-08-09 — primary project sources.** Luanti documents singleplayer as a client and server in the same process on different threads ([engine structure](https://docs.luanti.org/for-engine-devs/structure/)). Its client source creates the local server on a background thread, binds simple singleplayer to `127.0.0.1` specifically to avoid a Windows Defender warning, keeps rendering a creation/cancellation overlay, then connects as a client ([`Game::createServer`](https://github.com/luanti-org/luanti/blob/master/src/client/game.cpp)). Shutdown first stops the client, then destroys the server on another thread while updating a progress overlay ([`Game::shutdown`](https://github.com/luanti-org/luanti/blob/master/src/client/game.cpp#L3188-L3288)). Server destruction stops simulation and generation workers before plugin shutdown hooks, player saves, environment metadata, and map/database teardown ([`Server::~Server`](https://github.com/luanti-org/luanti/blob/master/src/server.cpp#L2772-L2939)).

Useful lessons:

- loopback-only binding is a security and UX feature, not just an address choice;
- startup and shutdown are asynchronous user-visible phases;
- generation workers must quiesce before final callbacks/save;
- the server owns persistence sequencing.

Luanti also contains a local optimization that copies server media directly while still filling the same cache remote connections use; its source comment explicitly rejects a shortcut that would make later remote joins slower ([same `game.cpp`, `copyServerClientCache`](https://github.com/luanti-org/luanti/blob/master/src/client/game.cpp#L3488-L3518)). That is a good standard: a local fast path is acceptable only when it preserves externally visible protocol/cache semantics.

### Veloren

**Veloren historical and current sources.** Voxygen builds its server crate behind the `singleplayer` feature, demonstrating code reuse between the graphical client package and server library ([current `voxygen/Cargo.toml`](https://gitlab.com/veloren/veloren/-/blob/master/voxygen/Cargo.toml)). A historical maintainer response describes the singleplayer server listening only on `127.0.0.1`, while LAN hosting uses the dedicated server path ([Veloren #574](https://gitlab.com/veloren/veloren/-/issues/574)). Version 0.8 had to make Voxygen wait until server initialization completed before connecting, fixing a startup race ([v0.8 release](https://gitlab.com/veloren/veloren/-/releases/v0.8.0)). A 2021 singleplayer failure shows an ancillary loopback metrics bind panic crashing the whole Voxygen process despite the game channel itself using in-process MPSC ([Veloren #1419](https://gitlab.com/veloren/veloren/-/issues/1419)).

Useful lesson: readiness must be an explicit event after **all required resources** are acquired, and an embedded server fault can take down the client even when gameplay transport is in memory. This is the strongest project-specific reason to prefer a child process for VibeCraft's plugin-heavy server.

### Other engine/library evidence

**Unity Netcode for Entities 1.4/1.5.** Unity keeps client and server in separate simulation worlds and supports an in-process host via IPC, but uses UDP when they are not in one process ([client/server worlds](https://docs.unity.cn/Packages/com.unity.netcode%401.5/manual/client-server-worlds.html), [`IPCAndSocketDriverConstructor`](https://docs.unity.cn/Packages/com.unity.netcode%401.4/api/Unity.NetCode.IPCAndSocketDriverConstructor.html)). This validates the separation of simulation roles but also demonstrates that an IPC fast path is a conscious second transport. VibeCraft should avoid that second gameplay transport until profiling proves loopback UDP material.

**Godot 4 stable.** Godot can create a process with redirected standard I/O, but its documented process helpers create an independent process that will not terminate merely because Godot exits ([Godot `OS.execute_with_pipe`](https://docs.godotengine.org/en/stable/classes/class_os.html)). Therefore process creation alone is insufficient: the child needs a parent-liveness contract and the client needs a save-aware shutdown state machine.

**.NET process APIs.** `ProcessStartInfo` supports redirected stdin/stdout/stderr; Microsoft warns that redirected pipes can deadlock when the parent does not drain output asynchronously, and that `Kill` is abnormal termination that may lose edited data ([redirected input](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.redirectstandardinput), [redirected output/deadlocks](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.redirectstandardoutput), [`Process.Kill`](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill)). These constraints are incorporated below.

### Sourced conclusions versus inference

Directly sourced:

- Minecraft, Luanti, Veloren, and Unity reuse a server role for local play rather than maintaining unrelated singleplayer rules.
- Luanti and Veloren bind private singleplayer to loopback and expose asynchronous initialization behavior.
- Veloren has had both readiness-race and embedded-server-fault failures.
- Godot-spawned processes are not automatically tied to Godot's lifetime; redirected streams require active draining.

VibeCraft engineering inference:

- A supervised child process is worth its memory/startup cost because VibeCraft explicitly wants server plugins, crash-safe saves, and dedicated parity.
- Standard streams should carry only parent/child lifecycle control; using the normal UDP gameplay connection gives more valuable parity than an in-memory fast path.
- A private local world and a multiplayer host have different pause, authentication, bind, and ownership semantics and should not be the same mutable mode in v1.

## Proposed design

The detailed state machine below is the selected child-process topology. The same
`ServerCore` must also be hostable by a minimal embedded adapter for conformance and
fallback testing, but that comparison does not reopen the desktop product decision.

### Components and ownership

```text
Godot client process
  LocalSessionSupervisor (C#)
    ├─ starts/monitors VibeCraft.Server
    ├─ writes framed control commands to child stdin
    ├─ asynchronously reads framed events from child stdout
    ├─ asynchronously drains/logs child stderr
    └─ creates the ordinary game client connection

VibeCraft.Server child process
  Bootstrap/control loop
    ├─ parent-liveness via control-stream EOF + heartbeat timeout
    ├─ lifecycle state machine
    └─ progress/fatal/save-complete events
  Game transport
    └─ ordinary UDP stack bound to 127.0.0.1:ephemeral
  Authoritative simulation and persistence
    └─ identical handlers used by dedicated mode
```

Use `System.Diagnostics.Process` from the Godot C# layer with `UseShellExecute=false` and all three standard streams redirected. Read stdout and stderr continuously and asynchronously from process start; never call a blocking `ReadToEnd`/`WaitForExit` pair on Godot's main thread.

Standard streams are not the gameplay protocol:

- stdin: length-delimited local control commands;
- stdout: length-delimited machine-readable lifecycle events only;
- stderr: human-readable structured logs, drained continuously and copied to the session log;
- UDP loopback: login, inputs, snapshots, chunks, actions, and all other gameplay.

If binary framing over `StreamReader` proves awkward, newline-delimited JSON is acceptable for the small control protocol. It must have a schema version and maximum line length. Do not parse ordinary log text as readiness.

### Session modes

```csharp
enum ServerHostMode
{
    LocalPrivate, // child, loopback only, one owner, pause permitted
    Dedicated     // externally supervised, configured interfaces, no client-owned pause
}
```

`LocalPrivate` invariants:

- bind the game socket explicitly to `127.0.0.1` (and optionally `::1` after dual-stack testing), never `0.0.0.0`;
- ask the OS for an ephemeral port rather than racing on a preselected fixed port;
- accept one local owner using a cryptographically random 256-bit one-time bootstrap token delivered over stdin, not command-line arguments or logs;
- acquire the world lock in the server process before reporting ready;
- do not advertise, forward ports, start public metrics, or enable remote administration;
- disable LAN promotion in v1. “Host friends” should start an explicit hosted/dedicated mode after its auth and pause policy is designed.

`Dedicated` invariants:

- lifecycle is owned by its launcher/service/admin, not by any connected game client;
- a normal client may disconnect but may not pause or stop the server;
- OS `SIGTERM`/Ctrl+C and an authenticated admin stop command enter the same drain/save state machine as local shutdown.

### Bootstrap contract

The client creates a fresh random `session_id` and token, starts the child, then sends a bootstrap request over stdin:

```json
{
  "control_version": 1,
  "kind": "bootstrap",
  "session_id": "random-128-bit-id",
  "auth_token": "random-256-bit-secret",
  "world_path": "canonical absolute path",
  "mode": "local_private",
  "game_bind": "127.0.0.1:0",
  "client_build": "content/protocol build id"
}
```

The world path is also validated against the application's worlds root; the server does not accept path traversal or a symlink escape merely because its parent supplied it.

Lifecycle events use typed messages:

```text
BootProgress(stage, completed?, detail_code)
Ready(session_id, pid, endpoint, protocol_version, server_build, world_id, capabilities)
PauseChanged(paused, effective_world_tick)
SaveProgress(stage, completed_units?, total_units?)
SaveComplete(checkpoint_id, durable_world_revision)
Fatal(code, safe_message, diagnostic_id)
Exited(reason, clean, last_durable_world_revision)
```

`Ready` is emitted only after:

1. arguments/control schema and build compatibility pass;
2. the exclusive world lock is held;
3. storage recovery/migration succeeds;
4. registries, required server mods/plugins, and world metadata load;
5. the loopback game socket is listening;
6. the simulation can admit its owner connection.

The client does not connect by polling a port. It waits for `Ready`, verifies `session_id`, build/protocol compatibility, PID, and loopback endpoint, then performs the ordinary network handshake with the one-time token.

Timeout policy:

- no short absolute timeout while valid `BootProgress`/heartbeats continue;
- declare startup stalled after 15 seconds with neither progress, heartbeat, nor process exit;
- offer **Keep waiting** and **Cancel startup**; cancellation requests graceful bootstrap abort and waits up to 10 seconds before offering force termination;
- world generation progress can be long, but an unresponsive child is never represented as “still generating.”

On any failure before `Ready`, the child releases the world lock, emits `Fatal` when possible, and exits nonzero. The client drains remaining output, records the exit code/diagnostic id, and returns to the world menu without partially entering a session.

### Runtime and parent liveness

- The supervisor sends a control heartbeat every two seconds. Gameplay packets do not substitute for ownership heartbeats.
- EOF on child stdin proves the parent/control owner disappeared. The child immediately enters orphan shutdown: stop admission/mutations at the next tick boundary, save through the normal barrier, and exit.
- Five missed heartbeats (10 seconds) trigger the same orphan shutdown even if an inherited handle remains open unexpectedly.
- A parent may reconnect to gameplay after transient UDP loss, but it cannot reattach as process supervisor in v1.
- The child writes its PID and `session_id` into the world lock metadata. On the next launch, stale-lock recovery verifies process identity/start time before recovery; it never blindly deletes a lock held by a live server.
- The server process monitors its own fatal simulation task. An unhandled fault emits `Fatal` where possible and exits nonzero. The client offers the log and a restart/recovery attempt; it does not silently restart and risk a crash loop.

### Pause semantics

Only the authenticated owner of `LocalPrivate` may request pause.

```text
Running --PauseRequest--> Pausing --tick boundary/quiesce--> Paused
Paused  --ResumeRequest--> Resuming --next fixed tick------> Running
```

At the pause barrier:

- finish the current authoritative tick and record `effective_world_tick`;
- stop advancement of world time, entities, AI, scheduled ticks, fluids, redstone, hunger, weather, and gameplay timers;
- stop committing worker results to world state; workers may finish immutable calculations and queue results;
- continue control/game network polling, keepalives, disconnect handling, diagnostics, and save I/O;
- preserve the accumulator without “catching up” elapsed wall time on resume;
- permit explicit save/checkpoint while paused.

The client shows the pause menu immediately as UI, but labels the world paused only after `PauseChanged(true, tick)`. Until the acknowledgement arrives it suppresses new gameplay input locally while the server finishes the barrier.

Default trigger: opening the explicit pause menu. Losing window focus does **not** pause by default; offer it later as an accessibility/user preference. A breakpoint or frozen Godot render thread must not implicitly pause the child.

If any non-owner connection is ever admitted, pause capability is revoked before that player joins. This is another reason not to implement “Open to LAN” by rebinding the private child.

### Graceful leave and save barrier

Local close-to-menu and application exit use the same server-owned sequence:

```text
Running/Paused
  -> StopRequested(reason)
  -> Draining at tick T
  -> QuiescingWorkers
  -> Saving
  -> SaveComplete(checkpoint, durable revision)
  -> ClosingTransport
  -> ProcessExited(0)
  -> World lock released
```

Detailed contract:

1. The server acknowledges `StopRequested` with the last tick that can accept mutations. Later gameplay actions are rejected as shutting down.
2. Network/world-generation/path jobs stop accepting new work. Jobs that can mutate only do so through the tick commit queue, which is closed at the barrier.
3. Server plugin shutdown hooks run with cancellation and time budgets from `ARCH-05`; a stuck plugin cannot block forever.
4. Player, entity, chunk, world metadata, registry/plugin storage, and pending transaction state are checkpointed through the persistence API.
5. `WORLD-04` performs its required flush/fsync/atomic publish and returns a durable checkpoint id/revision.
6. Only then does the server emit `SaveComplete`, close the game socket, release the world lock, and exit zero.

The client keeps a responsive save-progress screen and continues draining output. After 30 seconds without progress, it offers **Keep waiting** or **Force quit (recovery may be needed)**. It never force-kills automatically during an ordinary quit. OS shutdown may impose a shorter external deadline; in that case the journal/recovery guarantees of `WORLD-04` are the fallback, not a claim that saving completed.

`Process.Kill(entireProcessTree: true)` is last-resort user-authorized termination after the warning. It must never be called merely because the client window closed; first close stdin/send stop and wait for the barrier.

### Crash and disconnect behavior

| Event | Required behavior |
| --- | --- |
| Client UDP disconnect, process/control still alive | Server keeps local world paused for a 10-second reconnect window; supervisor reconnects with the same session, then resumes only after explicit owner input |
| Client process crash/control EOF | Child enters orphan shutdown and durable save; no reconnect window requiring a dead supervisor |
| Server process crash | Client stops prediction, returns to an error/recovery screen, preserves logs/exit code, never treats latest client replica as a save |
| Control protocol malformed/version mismatch | Fail before world admission; release lock and exit nonzero |
| World already locked | Report owning PID/session metadata when safe; do not open read-write |
| Save hook/storage failure | Emit fatal save status, keep process/world lock alive when possible, offer retry; never emit `SaveComplete` |
| Force termination/power loss | Next launch runs `WORLD-04` recovery before `Ready`; client reports recovery result |
| Dedicated client disconnect | Only that connection ends; server lifecycle is unchanged |

### Public interfaces

```csharp
public interface ILocalServerSupervisor : IAsyncDisposable
{
    LocalServerState State { get; }
    IAsyncEnumerable<ServerLifecycleEvent> Events(CancellationToken ct);
    Task<ReadyInfo> StartAsync(LocalWorldRequest request, CancellationToken ct);
    Task<PauseAck> SetPausedAsync(bool paused, CancellationToken ct);
    Task<SaveCheckpoint> SaveAsync(CancellationToken ct);
    Task<ShutdownResult> StopAsync(ShutdownReason reason, CancellationToken ct);
}

public enum LocalServerState
{
    Idle, Spawning, Bootstrapping, Ready, Connecting, Synchronizing,
    Running, Pausing, Paused, Draining, Saving, Stopping,
    Exited, Faulted
}
```

The authoritative server core exposes the same host lifecycle to both process frontends:

```csharp
public interface IServerHost
{
    Task<ReadyInfo> StartAsync(ServerStartOptions options, IProgress<BootProgress> progress, CancellationToken ct);
    Task<PauseAck> SetPausedAsync(PauseRequest request, CancellationToken ct);
    Task<SaveCheckpoint> SaveBarrierAsync(SaveReason reason, CancellationToken ct);
    Task<ShutdownResult> ShutdownAsync(ShutdownReason reason, CancellationToken ct);
}
```

The dedicated console/service maps signals and admin commands to `IServerHost`; the child bootstrap maps control messages to it. Neither frontend contains gameplay save logic.

## Prototype or benchmark

Required: yes

Smallest useful experiment: Host one tiny `ServerCore` through both (a) a Godot-launched child with stdio control and loopback transport and (b) an embedded adapter using the same typed protocol semantics. Advance a counter/world revision, pause, checkpoint, resume, and shut down with fault injection at every lifecycle state. Compare protocol traces, startup, memory, failure containment, and packaging. No chunk renderer or full game is required.

Test scenarios:

- 100 consecutive start/connect/pause/resume/save/stop cycles on each declared target platform (begin with Windows x64 and Linux x64); run both adapters where the platform permits;
- two worlds launched concurrently plus a second launch of the same world;
- spaces/non-ASCII in install and world paths;
- server startup delays, migration failure, plugin failure, port-bind failure, protocol/build mismatch, malformed control message;
- client kill, server kill, parent stdin closure, frozen control heartbeat, frozen game socket, OS termination signal;
- dirty world with writes injected immediately before pause and shutdown;
- stdout/stderr flood large enough to fill pipe buffers if not drained;
- save hook that hangs and storage layer that reports flush failure;
- loopback packet loss/jitter using the same network harness as remote sessions.

Success metrics:

- All 100 normal cycles exit server code 0, release the world lock, leave no child process after five seconds, and reopen at the exact acknowledged durable revision.
- `Ready` is never observed before the world lock, storage recovery, registries/plugins, and game socket are ready.
- Pause acknowledgement occurs at a tick boundary; authoritative tick/world time does not advance during a 60-second pause and does not catch up on resume.
- Client crash/control EOF causes graceful child exit and lock release within 15 seconds in the minimal test world.
- A deliberately crashed/killed server never emits `SaveComplete`; next startup either recovers to the last durable checkpoint or fails closed with a diagnostic.
- Concurrent opening of one world yields exactly one writer; the loser does not modify any world file.
- Stdout/stderr flood does not deadlock bootstrap or shutdown and does not block Godot's main thread.
- Local gameplay traverses the same serializer, authentication, input, snapshot, and disconnect handlers as a separately launched dedicated server, verified by handler-level coverage or a protocol trace comparison.
- No local game socket is reachable through a non-loopback interface.

## Greenlight criteria

- The cross-platform prototype meets every lifecycle and fault-injection metric above.
- `WORLD-04` defines what “durable checkpoint” means and demonstrates recovery after forced termination.
- `WORLD-05` defines dirty tracking/autosave and the set of state covered by a save barrier.
- `NET-03` validates the selected GNS loopback path and protocol conformance; any
  measured showstopper and replacement must not force different gameplay semantics.
- `NET-07` defines build/protocol compatibility before admission.
- `ARCH-05` gives server plugins bounded shutdown behavior.
- Packaging proves the selected host adapter and runtime are included and launchable in exported Godot builds on every declared target platform.
- Product accepts that LAN hosting is separate from private singleplayer and declares the first supported host platforms.
- The child and embedded adapters produce equivalent authority/protocol traces. The
  child must pass export, startup, memory, pause/save, crash, orphan, and native-
  library gates for supported desktop platforms; a failure blocks that platform or
  explicitly promotes the already-conformant fallback rather than creating different
  gameplay semantics.

## Risks and open questions

- A second .NET process adds startup time and memory; embedding reduces those costs but shares crash/plugin risk. Measure both before selecting the default.
- macOS sandbox/notarization and store packaging may restrict helper executable placement or launch. Godot's process documentation warns that sandboxed macOS apps can launch only embedded helper executables ([Godot `OS` process methods](https://docs.godotengine.org/en/stable/classes/class_os.html)).
- Linux/Windows process and pipe semantics differ. The prototype, not API availability alone, is the gate.
- The 10-second UDP reconnect window is a default, not a persistence promise. It may be shortened if it complicates player-entity ownership.
- A plugin can still corrupt the server process/world; child isolation protects the client, not authoritative data. Plugin capabilities and storage transactions remain necessary.
- Local users with sufficient OS privileges can inspect or manipulate their own process. The one-time token prevents accidental/cross-process joins, not a determined owner cheating in their own singleplayer world.
- Mobile, console, and web may forbid child processes. They need an embedded server host that preserves the same `IServerHost` and gameplay protocol, or they remain unsupported.
- “Open to LAN” is explicitly deferred. Designing it later may choose a new hosted process rather than promoting an already private process.

## Dependencies

- Prototype invariants: `ARCH-01` authority boundary plus typed stubs for transport, handshake, save barrier, and plugin-disabled shutdown.
- Production requires: greenlit `NET-03`/`NET-07`, `WORLD-04`/`WORLD-05` durability coverage, and `ARCH-05` plugin shutdown budgets.
- Blocks: exported application packaging, world-selection UX, local authentication, pause UI, recovery UI, and end-to-end singleplayer tests.
- Related: `NET-08` for any non-loopback hosted mode; `MOD-03` for lifecycle hooks.

## Rejected or deferred alternatives

- **Direct singleplayer world mutation by the client:** rejected; it defeats every parity and authority goal.
- **Changing the selected child desktop topology without its acceptance evidence:** rejected. The embedded host remains the required conformance/fallback counterfactual, not a competing product topology.
- **In-memory gameplay transport for local play:** rejected for v1. It hides packetization/loss/order behavior and doubles the transport surface. A test-only in-memory adapter is acceptable for unit tests.
- **Fixed local UDP port:** rejected; it creates collision and stale-process failure modes. Bind port zero and communicate the selected endpoint through `Ready`.
- **Parsing logs for “server started”:** rejected; readiness is a versioned machine event.
- **Automatic force-kill timeout on normal exit:** rejected; it turns slow saves into avoidable data loss.
- **Promoting private singleplayer to LAN by rebinding:** deferred. Multiplayer host admission, pause revocation, auth, firewall, DDoS, and ownership need an explicit design.
- **Client writes a last-minute save from its replica:** rejected. The client does not possess canonical world state.

## Source quality note

Minecraft Java details use Fabric mappings plus Mojang's official LAN documentation because the integrated-server source is not openly published by Mojang. Luanti and Veloren claims link to first-party source, documentation, releases, or issue trackers. Unity, Godot, and .NET claims link to vendor documentation. The child-process candidate and exact state machine are VibeCraft-specific engineering conclusions derived from those implementations rather than claims that another game uses the same design.
