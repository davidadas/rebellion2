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

## Headless EOS verification

`Assets/Tests/Runtime/Multiplayer/Epic` owns the opt-in real-service integration runner. The runtime
assembly is included in a player only when `REBELLION_EOS_TACTICAL_TEST` is explicitly supplied by the
integration builder. Regular player builds exclude it. The runner uses the production coordinator,
store authentication, EOS discovery and peer, ordered campaign, and tactical driver.

Build one Windows x64 integration player from the current checkout with Unity's
`-batchmode -nographics -quit -buildTarget StandaloneWindows64 -executeMethod EosTacticalIntegrationPlayerBuild.Build` arguments and
`-eosTacticalBuildDirectory <absolute-directory-under-the-system-temp-directory>`. Supply the usual
`-projectPath` and `-logFile` paths. The builder requires installed Windows native math and local EOS
configuration under the existing ignored `Assets/StreamingAssets/EOS` path. Its build manifest records
source and artifact fingerprints without player tokens or client configuration values.

Start that same executable twice with `-batchmode -nographics`. Each process needs:

- `-eosTacticalRole host` or `guest`.
- The same `-eosTacticalRunID <32-character-GUID>` and
  `-eosTacticalReportDirectory <absolute-report-root>`.
- Separate absolute `-userDataPath`, `-saveDirectory`, `-playerSessionFile`, temporary directories,
  and `-logFile` paths. The two session files must authenticate distinct real EOS product users.
- `-contentPath <verified-external-content-root>`.

Use `-eosTacticalCapitalsPerSide 1 -eosTacticalSquadronsPerSide 2` for the small carrier case, then
`-eosTacticalCapitalsPerSide 24 -eosTacticalSquadronsPerSide 8` for the fleet case. Both participants
must request the same roster and relay policy. Authored unit statistics and carrier capacities are
retained. `-eosTacticalDeadlineSeconds` bounds wall-clock duration between 30 and 1800 seconds.

Both fleet cases first exercise the same shared control check. Each faction must visibly retain
accepted enemy targets for capital ships and deployed fighters, together with its distinct formation.
The guest then requests the regular shared tactical pause. While paused, both players inspect an
opponent and verify that inspection grants no command ownership. They independently fingerprint
public unit and group state at the same frozen system time, excluding local selection, audio, and
interpolated presentation. Both reports must publish matching validated evidence before the host
requests resume. Each player must observe resumed combat before completing the case. Campaign speed
controls are not used to alter tactical simulation.
The role-specific `host-paused-state.json` and `guest-paused-state.json` sidecars retain the exact
named comparison input once, including floating-point bit patterns, so a mismatched field can be
diagnosed without relaxing the fingerprint assertion.

With no withdrawal option, the battle must finish through normal combat. To exercise a deliberate
human retreat in the large fleet case, pass `-eosTacticalGuestWithdrawalAtSystemTime 300` to both
processes. Only the guest confirms withdrawal, once authoritative state has reached that system
time and real capital/fighter firing and hull damage have occurred. The runner uses the regular
withdrawal preparation and confirmation controls and fails if their eligibility check rejects it;
it does not create a safe destination. Both reports must observe the guest's actual withdrawal,
and the guest must submit exactly one authenticated withdrawal command. This case verifies retreat
completion rather than an uninterrupted battle to destruction. Final result, effect, day-barrier,
campaign fingerprint, and cleanup requirements remain the same. Omitting the option disables it.

Run the relay case in fresh processes with a fresh run identity and
`-eosTacticalForceRelay true` on both sides. Each authenticated platform applies and reads back the
policy before negotiating its peer connection. A successful setting is insufficient: each report
must contain an actual SDK route observation for the admitted counterpart and lobby socket, and
every such observation must be `RelayedConnection`. A direct route fails the forced-relay run.

Each role writes an incremental JSON report under `<report-root>/<run-id>`. Passing requires real
authentication and launch, both participants' authenticated orders, capital and fighter discharges,
impacts and hull damage, one agreed battle result and campaign-day resumption, matching terminal
campaign fingerprints and effects, and completed connection cleanup. This verifies live service and
transport behavior without opening game windows; it does not measure rendered frame rate.
Reliable shot, impact, destruction, withdrawal, and fighter-recovery callbacks must match in count
and in a streaming SHA256 digest of their ordered identities and lossless transported payloads.
Matching counts alone do not satisfy the event comparison.
