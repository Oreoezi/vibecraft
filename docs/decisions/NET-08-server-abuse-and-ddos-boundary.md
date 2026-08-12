# NET-08 Server abuse and DDoS boundary

Status: Proposed

## Decision

Recommended choice: Replace “DDoS safe” with a layered, measurable abuse-resistance contract: validated addresses before allocation, anti-amplification, authenticated sessions, bounded work/queues, per-source and global budgets, graceful degradation, and an explicit dependency on upstream mitigation for volumetric attacks.

One-sentence rationale: Application code can prevent cheap amplification and resource-exhaustion bugs, but it cannot keep a consumer Internet link usable when attack traffic saturates the link.

### Owner direction — 2026-08-13

For v1, one authenticated player/session must not be able to crash or materially stall
a healthy server through supported actions, malformed packets, packet floods, chunk
requests, or ordinary “lag machine” construction within configured limits. This is a
testable resilience target, not an absolute claim against unknown vulnerabilities or
link-saturating traffic. A session that persistently exceeds packet/work budgets is
throttled and disconnected promptly.

The safety property comes from bounded admission, generation, queues, simulation work,
and plugin calls—not from assuming “threaded chunks” makes expensive gameplay free.
Optional proof-of-work admission may be researched post-v1 for connection floods, but
it is not a substitute for authentication, rate limits, or upstream mitigation and
must account for weak/mobile clients and botnet parallelism.

## Context and constraints

- The public server accepts unauthenticated UDP-originating traffic.
- Attackers can spoof source addresses, replay packets, send malformed payloads, create connection churn, request expensive chunks, or exploit plugins.
- Player-hosted servers may lack a reverse proxy or provider-level scrubbing.
- Resource limits must protect legitimate players without creating unbounded per-IP state behind shared NATs.

## Options considered

| Option | Strengths | Costs/risks | Fit |
| --- | --- | --- | --- |
| Best-effort validation only | Easy | Cheap CPU/memory/amplification attacks remain | Reject |
| Custom UDP cookies, crypto, rate control | Tailored and low overhead | High security/protocol engineering risk | Defer unless mature libraries fail requirements |
| Mature encrypted transport plus application budgets | Reuses address validation, crypto, loss/congestion work | Library/runtime constraints; still needs app controls | Recommended |
| Require a hosted gateway for all servers | Strong centralized controls | Self-hosting dependency and cost | Optional deployment tier |

## Evidence

IETF UDP best practice requires congestion control for substantial UDP use, recommends existing standard security mechanisms rather than inventing them, calls out duplication/reordering handling, path-MTU limits, and amplification risk ([RFC 8085](https://www.rfc-editor.org/rfc/rfc8085.html)). QUIC includes address validation and an anti-amplification rule specifically to prevent an endpoint being used as a traffic amplifier ([RFC 9000](https://www.rfc-editor.org/rfc/rfc9000.html)); its loss and congestion behavior is separately standardized ([RFC 9002](https://www.rfc-editor.org/rfc/rfc9002.html)).

OWASP's DoS guidance recommends bandwidth/load limits, rate limiting, and upstream filtering/provider preparation for larger attacks ([OWASP DoS cheat sheet](https://cheatsheetseries.owasp.org/cheatsheets/Denial_of_Service_Cheat_Sheet.html)). This supports a responsibility boundary rather than a promise of immunity.

## Proposed design

### Responsibility boundary

VibeCraft provides protocol and application-layer abuse resistance. Server operators/providers remain responsible for attacks that saturate the host's access link or exceed host packet-processing capacity. Documentation must state this plainly.

### Connection admission

Before address validation:

- perform only fixed-cost header checks;
- allocate no player, chunk, compression, plugin, or large reassembly state;
- send no more than three bytes for each byte received, and preferably only a minimal challenge;
- use a stateless authenticated token bound to source address, time bucket, and server secret, or inherit equivalent behavior from the selected transport;
- reject unsupported versions in a bounded response;
- cap initial datagrams to a conservative non-fragmenting size (prototype at 1200 bytes).

After address validation but before authentication:

- bound handshake lifetime, retransmissions, message sizes, decompression ratio, and concurrent handshakes;
- partition token buckets by source prefix plus a global bucket;
- avoid permanent bans based solely on IP because NATs and spoofing make them blunt.

After authentication:

- apply account/session budgets in addition to network-prefix budgets;
- issue unpredictable session identifiers and reject replay/out-of-window sequence numbers;
- cap requested view distance, chunk request rate, chat/commands, block actions, inventory operations, and expensive queries;
- make duplicate mutating requests idempotent using action IDs or sequence windows.

### Resource isolation

- Network parsing writes validated bounded commands into fixed-capacity queues.
- Simulation consumes a per-player/per-tick command budget and records drops/throttles.
- Chunk generation, compression, save, and plugin queues have explicit capacity and cancellation.
- Plugins never execute on socket receive threads and cannot bypass budgets.
- Compression refuses attacker-controlled extreme expansion before allocating the claimed output.
- Overload degrades in order: optional telemetry/cosmetics, far chunks, low-priority entity snapshots, new admissions. Existing authoritative actions keep bounded service where possible.
- Each authenticated session has an attributed maximum amount of admitted parse,
  command, generation, circuit/block-update, plugin, and outbound work per interval.
  No single session can borrow an unbounded global queue. Repeated exhaustion yields a
  stable overload result and disconnect rather than tick-wide work multiplication.

### Observability

Expose counters/histograms for invalid packets by reason, bytes before/after validation, handshake outcomes, limiter drops, queue depth, compression ratios, chunk-request rates, retransmission/loss, CPU time by subsystem, and disconnect cause. Avoid high-cardinality raw-IP labels in metrics.

## Greenlight criteria

- The transport threat model identifies spoofing, amplification, replay, parsing, decompression, connection churn, and expensive authenticated actions.
- Every network-controlled length/count has a tested upper bound before allocation.
- No unauthenticated request can trigger plugin execution, world load, chunk generation, or a response exceeding the anti-amplification budget.
- Operators receive documented upstream mitigation expectations and overload controls.
- In the declared single-attacker corpus, one authenticated session cannot crash the
  process, exceed hard memory/queue caps, or make the authoritative 60 TPS p99 deadline
  fail for healthy existing sessions beyond the explicitly bounded recovery window.

## Prototype or benchmark

Required: yes.

Create a packet-flood and authenticated-abuse harness covering random bytes,
valid-header garbage, spoofable initial requests, handshake churn, replay, duplicate
actions, compressed bombs, chunk-request floods, view-distance churn, block/circuit
update attempts, plugin-command floods, and slow readers. Measure allocations, CPU
and admitted work per principal, queue bounds, response amplification, healthy-player
latency/tick health, disconnect time, and recovery after the flood stops.

Initial pass targets:

- process malformed pre-validation datagrams without heap allocation in the common path where practical;
- never exceed the pre-validation amplification cap;
- memory remains bounded under sustained input above service capacity;
- simulation tick p99 recovers within five seconds after an application-layer flood ceases;
- no crash, deadlock, or unbounded log growth.
- one authenticated attacker is throttled/disconnected without causing a sustained
  60 TPS deadline failure or disconnecting healthy sessions in the declared fixture;
  freeze exact recovery/latency thresholds with target hardware and player load.

Targets must be revised against actual hardware and player-count goals.

## Risks and open questions

- QUIC can simplify crypto/congestion/address validation but may add runtime and datagram API constraints.
- Residential hosting may remain vulnerable to simple bandwidth saturation regardless of application behavior.
- Account authentication can itself become an external dependency and denial point.
- Proof of work can penalize legitimate low-power clients while a distributed attacker
  parallelizes it; evaluate it only as an optional admission layer with measurement.

## Dependencies

- Requires: `NET-03`, deployment/player-count targets.
- Blocks: public server exposure and plugin network APIs.

## Rejected or deferred alternatives

- Claiming the server is “DDoS-proof”: rejected as untestable and misleading.
- Implementing custom cryptography: rejected for v1.
- Unbounded queues to avoid dropping player data: rejected because they convert overload into latency and memory failure.
- Mandatory v1 proof of work: deferred; first prove GNS admission, authentication,
  per-session work budgets, and upstream responsibility.
