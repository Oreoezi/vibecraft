# G1 semantic section-read contract

Status: **Provisional.** This document records the G1/R1 candidate handoff introduced
by issue #29. It is not frozen, persisted for users, or a public wire promise. Only a
future written G1 `greenlight` may freeze the handoff.

## Consumer seam

`ISectionBlockStateSnapshot` is the sole public section-state read seam in G1. It
exposes exactly:

- the snapshot's validated `SectionGeometry`;
- the exact `SectionRevision` captured with its values; and
- `GetBlockState(LocalIndex)`, returning the captured world-local `BlockStateId`.

`LocalIndex` values from zero through `Geometry.Volume - 1` are valid. An index at
or beyond the geometry volume is rejected with `ArgumentOutOfRangeException`; it is
never interpreted as air or missing content.

## Immutability guarantee

For the lifetime of a published snapshot, its geometry, revision, and semantic block
states do not change. Later mutable-section edits cannot affect the snapshot. Input
arrays and spans used to construct a snapshot are copied or transformed into storage
owned by the snapshot and are never retained by reference. Concurrent semantic reads
require no caller lock.

The revision and semantic values form one capture. A publisher must not combine a
revision from one instant with values from another.

## Intended downstream use

- G2 may read the semantics to build its separately specified deterministic
  persistence projection.
- G3 may read the semantics for collision and simulation-time views.
- G4B may read the semantics to build immutable render inputs.

`BlockStateId` is world-local. A future publisher must pair snapshots with the
appropriate immutable content-definition mapping; that mapping is not part of this
section interface.

## Representation exclusions

The public seam exposes no mutable section, concrete snapshot type, array, span,
memory owner, enumerable, palette order, dictionary, packed word, bits-per-entry
value, storage kind, storage metric, or implementation hash. Uniform, paletted, and
direct containers remain internal candidate implementations and may change without
changing semantic consumers.

Neither the CLR interface nor any backing container is a persistence or wire format.
G2 owns explicit persistence fields and codecs; G4A owns explicit transport fields
and framing. No caller may serialize interface reflection metadata, CLR layout,
backing objects, or raw memory.

## Publication boundary

G1 exposes a consumer contract only. Concrete snapshot construction and mutable
section ownership remain internal until a later world owner defines publication and
lifetime coordination. No Godot, SQLite, GameNetworkingSockets, renderer, generator,
plugin, survival, or persistent-user-world dependency enters this handoff.
