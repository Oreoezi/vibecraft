# E1-r1 evidence area

This directory is reserved for versioned `VC-G1-E1-PROTOCOL-1.0.0` evidence.

- `diagnostic/` may contain exploratory or remediation runs. Every report must say
  `evidenceClassification: diagnostic`, `disposition: defer`, and
  `performanceDecisionEligible: false`.
- A decision-eligible issue-31 run is forbidden until the owner explicitly accepts
  the applicable G0 fixture and captured host/runtime conditions. It must use a
  separate path and must not overwrite diagnostics.

The report runner atomically publishes a new evidence-set directory and refuses to
overwrite any existing path. Fixture and report IDs are evidence identities only; no
artifact here is a world, persistence, network, or wire format.

The immutable failed baseline remains under `artifacts/g1/e1/full-observational/`.
See `docs/implementation/G1/E1-r1-protocol.md` for exact hashes and protocol rules.
