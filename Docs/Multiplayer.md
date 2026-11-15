# Multiplayer combat replication

## Authority and timing

The campaign uses host-ordered commands and verifies settled simulation state on both peers.
Tactical combat uses one authoritative host simulation and a passive guest replica. The guest never
advances combat systems or consumes the encounter's gameplay random stream. The host remains trusted;
this is a two-player listen-host design.

Simulation advancement does not wait for snapshot receipts. The host publishes at most one current
snapshot per local update, normally at 20 Hz. Initial state, movie transitions, and completion publish
immediately. Rendering advances independently and samples a bounded, timestamped presentation history.
Presentation sampling covers poses, visibility, lifecycle transitions, and departure animation; it never
changes authoritative positions, damage, targeting, command execution, or campaign outcomes.

## Delivery contracts

| Traffic | Delivery | Purpose |
| --- | --- | --- |
| Campaign actions, battle commands, effects, checkpoints, and receipts | Reliable ordered, channel 0 | Preserving indispensable operations and explicit agreement |
| Current tactical state | Replaceable unreliable unordered, channel 2 | Allowing newer state to supersede lost or delayed snapshots |
| Lobby chat | Existing channel 1 | Preserving its independent lobby transport |

Snapshots use a versioned, lossless binary representation with packet-local identity interning and
independent compression. Every snapshot can be decoded without an earlier snapshot. This avoids a
missing delta baseline preventing recovery; further delta encoding should be justified by measured
bandwidth requirements. Network representations do not quantize gameplay values.

The snapshot transport keeps a bounded window of eight partial assemblies within an 8 MiB budget and
one newest completed state. Incomplete assemblies expire independently; completing a newer snapshot
retires older work. Receiving one fragment from a newer snapshot does not discard an older assembly
that could still complete. Send backpressure discards replaceable work. Reliable commands do not wait
for the replaceable send path, and reliable sends have separate packet and byte retention bounds.
One XOR parity fragment per four data fragments can recover one missing data fragment in each group.
Parity storage counts toward the same assembly budget. Loss beyond that capability discards incomplete
state and relies on a newer complete snapshot or a reliable checkpoint.

Effects have a separate contiguous batch sequence. Each batch identifies the state and presentation
timestamp it needs. The guest releases it only after that state is available and its display cursor
has reached the event time. Duplicate batches do not replay effects. Movie announcements follow their
reliable event, and both presentations must acknowledge a movie before its shared pause ends.

Periodic reliable checkpoints recover from sustained snapshot loss. A resynchronization request reuses
the latest captured state without consuming newly accumulated simulation events. Terminal receipts
confirm both final state and the final effect watermark. Only then may the host submit the ordered
campaign result; its separate campaign receipt still governs final agreement.

## Failure and validation

Packet lengths, fragment geometry, retained events, pending commands, effect batches, and presentation
history are bounded. Preparation, unresponsive peers, unconfirmed commands, and terminal agreement have
explicit deadlines. A failure stops the shared encounter through campaign recovery. Recovery restores
the last completed campaign checkpoint; it does not migrate authority or resume an interrupted battle
midway through its simulation.

Regression coverage includes dropped, duplicated, and reordered snapshots; fragment expiry and
supersession; reliable event ordering; delayed playback; movie ownership; terminal fences; queue
overflow; and deadlines. Real-driver fixtures exercise delayed reliable delivery and complete snapshot
loss through final campaign agreement. Performance comparisons use identical authored fleets, seeds,
clock increments, and gameplay observations. Local simulated-link tests and CPU measurements do not
establish real EOS connectivity, rendered frame rate, or Internet behavior; those require a separate
two-account runtime validation.

## Source ownership

- `Assets/Scripts/Game/SpaceCombat` contains campaign-domain definitions and completed outcomes.
- `Assets/Scripts/Gameplay/SpaceCombat` contains encounter simulation and adjacent partial classes for
  its state projection. `Replication` contains the separate command, frame, effect, and state contracts.
- `Assets/Scripts/Multiplayer/Session/Combat` contains the connection-owned battle driver and protocol.
- `Assets/Scripts/Multiplayer/Protocol` contains the binary wire codec beside the existing packet and
  player-request codecs.
- `Assets/Scripts/Multiplayer/Transport` contains fragment assembly and bounded delivery scheduling.
- `Assets/Scripts/UI/SceneUI/TacticalCombat` contains Unity rendering and input presentation.

Tests mirror their subject's existing area under `Assets/Tests/EditMode/UnitTests`. Unity metadata moves
with an existing asset; generated scenes, prefabs, and copied media remain outside the source change.
