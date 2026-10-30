# Multiplayer recovery adversarial test report

Date: October 28, 2026. Checkout: v5, local `multiplayer` branch.

## October 30: ordinary loading and rejoin audit

The separate gameplay recovery loader is removed. Initial synchronization, shared loads, and
rejoining now use the existing persisted `GameRoot` contract and ordinary game-loading methods.
Both participants load the same saved world before their loaded-state fingerprints are compared.
There are no production `.Recovery.cs` files left.

### Findings and changes

| Inspected area | Evidence and resulting change |
| --- | --- |
| `GameSession.Recovery` | A controlled turn-1000 run using ordinary loading completed 20 days and 22 peer checks with identical gameplay outcomes to the baseline. Removing the alternative session loader, its state envelope, and the separate recovery snapshot codec. |
| `PlanetaryControlObserver.Recovery` | A normal XML save between two support-driven control losses in the same tick lost the guard: a neighboring planet received 22 support instead of 20. Moving the last handled tick onto `Faction.LastControlSupportShiftTick`; the regression now uses ordinary XML persistence. The existing once-per-faction-per-tick rule is unchanged. |
| Runtime generator settings | Saving the active RNG seed and ID-generation mode on `GameRoot` through normal persistence. The RNG position and ID counter were already saved and are not duplicated. Coverage checks the next wide random draw and next generated identity after an ordinary save/load. |
| Rejoin startup | Removing `StartRecoveredGame`, `ReplaceMultiplayerRecovery`, the separate tick connection, raw model-restoration helpers, and the scene's recovery-envelope branch. The rejoin scene uses `StartLoadedGame`; graph replacement uses `ReplaceMultiplayerGame`. |
| Duplicate world transfer | The reconnect offer sent a world, then the initial synchronization sent a world again. Reusing the initial-state receipt barrier for the first offer, including when the world supplier is the guest. The receiver checks initialization, revision, payload hash, and loaded-state fingerprint before opening gameplay. |
| Mutation during handoff | Local saving changes `SharedSaveToken`. Blocking ordinary persistence while the offered initial world awaits acknowledgment keeps the supplied state fixed; the existing operations gate reopens after admission. Tests cover rejection without changing the token for either participant. |
| Returning player names | Normal configuration could use an old lobby name while the returning client used its current authenticated name. Binding accepted greeting names to the reserved simulation roles and refreshing the retained lobby's presentation names keeps both worlds consistent without changing factions, readiness, or setup revisions. |
| Coordinator organization | The remaining network rejoin behavior lives in `MultiplayerCoordinator.Rejoin`. The custom simulation save/load path underneath it is gone. Removing the extra initial recovery envelope and the `runtimeRecovery` protocol flag. |

The coordinator still finds or recreates the reserved room, authenticates the two participants,
checks code/content compatibility, waits for an executing tick to finish, pauses the campaign,
selects a completed saved world, and exchanges the ordinary state receipt. Those operations are
network-session responsibilities, not a reason to keep a second simulation loader. The durable
store retains a small account/campaign/room header plus an ordinary multiplayer world payload.
Its save directory follows the active runtime's save manager.

Research timers were already included in normal faction serialization. The test that manually
forced a timer to zero did not establish a missing-save failure; its separate recovery-only
contract is removed. Ordinary loading retains its existing initialization behavior. Constructor
settings normalized by normal loading are processed on both peers through that same path.

The raw identifier read is consolidated into `PeekInstanceID`. Simulation fingerprints follow
persisted state and runtime rules; runtime parent-object caches are not an additional save contract.
Healing, ship repairs, and fighter replenishment in `RecoveryTickProcessor` are unaffected.

### Evidence and limits

The baseline and ordinary-load probe use seed 20261022, the same turn-1000 input, preserved
Alliance AI and Empire human controllers, 20 completed days, and 22 peer-state checks. Their
faction outcomes, battle responses, victory state, RNG position, and generated-ID count match.
The control-loss regression failed with expected support 20 versus actual 22 before the model
persistence change. The final automated checks and comparison are recorded in
`C:/Users/David/AppData/Local/Temp/rebellion-normal-load-proof-20261029`.

The fresh multiplayer measurements use TCP loopback. The changed coordinator handoff is covered
by inspected sender/receiver paths and receipt-admission regressions; no fresh native Epic
end-to-end reconnect run is claimed. These results do not establish a native FPS improvement.
The earlier UI-builder consolidation and native verification-cost finding remain separate work.

## October 29: implementation review (historical)

The decisions below describe the preceding revision and are superseded by the October 30 audit.


The recovery envelope was copying both missing gameplay history and data that normal persistence
already owns. Those are different problems. The changes below remove the demonstrated duplication;
the remaining decisions concern ownership of specific state, not a second general save system.

### Completed cleanup

- Keeping multiplayer UI generation in `MainMenuPrefabBuilder` and the existing prefab builders.
  Removing the proposal/preview windows, placement tool, preview shader, and separate placement
  settings asset. The accepted multiplayer button geometry is authored directly in the builder.
- Keeping profiling in the existing headless runners. Removing runtime phase tracing, its
  simulation/rendering hooks, and native timing commands. Headless per-day stopwatch measurements remain.
- Moving maintenance warning suppression and the next automatic scrap tick onto `Faction`.
  Normal saves now preserve both; the maintenance recovery class and DTO are removed.
- Removing the duplicate RNG position; `GameRoot.RandomIndex` already saves it.
- Removing the copied configuration; normal loading and recovery now share configuration
  reconstruction from the admitted catalog and saved custom settings. Verification still hashes
  the actual runtime rules, so a mismatch remains detectable.
- Removing the forced `Faction.Settings` serialization member and second faction-settings hash.
  The normal serializer contract already includes those settings.
- Removing the recovery list of every registered scene node. The lookup is rebuilt from the
  decoded hierarchy without assigning identities, changing ownership, or changing parent history.
- Removing the copied manufacturing queues and lane DTOs. Rebuilding uses saved producer IDs
  and queue sequence values. The shared builder now preserves those numeric values instead of
  renumbering them; queue priority is unchanged.
- Removing the per-fleet combat-flag DTOs and the combat-flag recovery setter. Checkpoints are
  captured only after turn processing and battle decisions finish.

- Saving a mission's starting roster as unit IDs and a baseline-present flag on `Mission` through
  ordinary persistence; removing its separate recovery DTO and capture/restore methods.
- Removing victory persistence and its recovery adapter. The live session still suppresses
  duplicate declarations and the campaign-ending flow disposes the session.
- Selecting smuggling and near-uprising notifications from observed support, membership, ownership,
  transit, deployment, and authored activation changes. The observer coalesces affected planets after gameplay reactions.
  Loading establishes a silent current-world baseline; neither notification-history adapter remains.
- Computing diversion rates once per producing planet per resource phase, independently of
  notification history. Removing the all-planet smuggling tick scan. Uprising gameplay timers and
  deficit resolution continue to run in their existing simulation phase.
- Completing queued movement/manufacturing reactions at command completion and tick handoff points,
  including the pause for human combat input. Saves and recovery capture reject pending reactions.
  Saving does not execute gameplay. The two private result-queue recovery adapters are removed.
- Removing saved ownership-observer associations and the fog-of-war recovery adapter. Ownership
  operations must publish their facts before completing; an O(1) pending-fact counter rejects
  incomplete operations and checkpoints. The live deduplication tables remain operation-local.
- Removing the saved per-faction ownership lists and their capture/restore methods. Recovery rebuilds
  faction membership from the existing hierarchy. Automatic scrapping orders eligible units by
  ordinal ID before its random draw, so both peers choose consistently without acquisition history.
- Removing the tick and combat recovery adapters, scheduler-phase DTO, saved battle responses,
  and serialized deferred messages/blockade results. The live scheduler still holds these facts
  while a battle is unresolved; saving and checkpoint capture reject that state.
- Retaining the last completed checkpoint across a disconnect. An executing tick finishes first;
  if it stops awaiting battle input, the survivor restores the retained completed checkpoint before
  rejoining. Both participants reopen the same world and make fresh responses when that battle occurs.
  Progress since that checkpoint is lost. Checkpoints retain their launch/save/load/disconnect cadence;
  this does not add a full-world snapshot per tick.
- Requiring a completed turn before initial network ordering begins. A requested checkpoint is
  captured before another day starts. Coordinated loads defer encounter detection to an agreed turn.

The generic registry-order/slot persistence removed in the preceding revision remains removed.
Acquisition order is not preserved. The random scrap target may differ from earlier revisions;
eligibility, draw count, shortfall timing, and the number of units scrapped are unchanged.

### Remaining state to discuss

| Item | Demonstrated reason it remains | Decision still open |
| --- | --- | --- |
| In-turn support-shift history | The existing rule suppresses repeated control-loss support shifts by a displaced faction within the same tick. | The current tick and handled faction IDs remain; changing this rule to a per-cascade scope is a separate decision. Transient observer associations are not saved. |
| RNG seed and generated-ID mode | Normal campaigns use `Summary.Seed`, but injected streams can have a different seed. A runtime flag also chooses counter-based versus random IDs. Both counters are already saved normally. | Keep the actual seed and mode until each has one explicit owner; neither counter is duplicated. |

The earlier shared-control builder consolidation remains unfinished: preview/proposal tools were
removed, but `MenuControlPrefabBuilder` still adds a shared authoring layer alongside
`MenuPrefabBuilder`. This recovery change does not claim to complete that UI cleanup.

### Serialization contract issues

The reference-aware JSON codec now transfers only completed worlds and runtime settings. Its
result-type registrations and battle-decision registration are removed. Deferred gameplay facts
are not serialized. Model parent/history reference handling remains in the codec; this change does
not establish that a separate serialization policy is the right permanent design.

Three recovery accessors still bypass model setter side effects: assigning the decoded galaxy,
reading/writing already assigned IDs, and restoring the faction processing rate. Parent and previous
parent object references are also included for live/historical result references. These are not
additional faction settings or duplicate worlds. The normal galaxy setter rebuilds hierarchy and
ownership; the identity getter can mint an ID; the processing-rate setter clamps to at least one
although a new settings instance initially contains zero. The zero-versus-one invariant needs an
explicit decision before removing that workaround. This review does not invent a new default.

Normal saves already include world objects, faction settings, RNG position, movement state,
production progress and priority, and created faction messages. The remaining adapters do not show
that these were generally missing from saves.

### Evidence and limits

Inspected producers, consumers, and restoration paths include `GameSession.Recovery`,
`GameTickProcessor`, `GameRoot`, `Faction`, `ManufacturingObserver`, `MaintenanceCommands`,
`MovementCommands`, `ManufacturingCommands`, `SmugglingCommands`, `UprisingResolver`,
`PlanetaryControlObserver`, `FogOfWarObserver`, `Mission`, `VictoryCommands`,
`SpaceCombatCommands`, `MultiplayerRecoverySnapshotCodec`, and `MultiplayerStateFingerprint`.
Regression coverage includes ordinary save/load of an active maintenance timer, recovery followed
by real automatic scrapping after an ownership transfer through both ordinary load and recovery,
covering the first and last random indices without saving ownership order; production priority with sequence gaps,
and rollback from an interrupted battle to a completed checkpoint with both responses reset.
Additional coverage verifies normal
mission-roster serialization contains only IDs for its baseline, creation callbacks finish before
saving and do not replay after recovery, callback failures prevent later checkpoints, support changes
need no tick poll, unrelated planets are not scanned, deletion/relocation is observed, and suppression
ship moves update other affected garrisons. Activation/deactivation cases verify stationary force
eligibility and subscriptions when an inactive sector becomes active again.

The earlier timing captures below are historical measurements of earlier revisions. They do not
establish a native FPS improvement from this cleanup. The native verification cost in P1 remains
open. The final check outputs and identical-save late-game comparison are kept in the local evidence
directory `C:/Users/David/AppData/Local/Temp/rebellion-multiplayer-review-20261029`.

| ID | Severity | Finding | Evidence |
| --- | --- | --- | --- |
| R1 | High, repaired locally | Snapshot revision ordering is scoped to the negotiated initialization. | Regression tests and native recovery followed by host termination, restart, and a successful shared save. |
| R2 | High, repaired locally | Recovery initiation follows room ownership; either role accepts the offer, with readiness scoped to the initialization and session. | Native live-host suspension after guest ownership migration now rejoins and saves successfully. |
| R3 | Medium, repaired locally | A selected recovering row must match the local recovery campaign and its exact room is used. | Three policy regressions; a second native campaign was not created. |
| R4 | Medium, repaired locally | Either paused survivor can save locally while the peer is disconnected. | Both native roles publish named saves and quicksaves; 10 focused regressions and the complete suite pass. |
| R5 | High, repaired locally | Guest-selected loading supplies the hash-bound saved world instead of requiring a host counterpart. | Protocol regressions and native guest-only load, shared save, and speed agreement. |
| R6 | Low, repaired locally | Synchronization failures use concise status text and compact notices ellipsize overflow. | Updated status regression and inspected rendering bounds; earlier failure screenshots remain historical evidence. |
| R7 | Medium, repaired locally | Manual persistence failures display the shared acknowledgment dialog. | Native locked quicksave preserves the old hash, displays the warning, releases shortcut suppression on acknowledgment, and retries successfully. |
| P1 | Performance work remains | Native daily receipt verification blocks the main thread for approximately 163 to 177 ms at the measured medians. | Twenty completed days per native peer at turn 1000; no native FPS diagnosis or performance fix claimed. |
| U1 | Concrete identity fault repaired locally | Persistence is created after the final state handshake rather than using a provisional recovery identity. | Both native processes restarted from disk, rejoined at turn 3, and completed a guest-requested shared save. Historical injected-shortcut delivery remains unproven. |
| V1 | Medium, verification tooling; repaired locally | The verifier previously timed out on a legitimate human combat decision and labeled waiting state as divergence. | Explicit ordered Fight responses, two-human mode, and a negative run that reports required input without divergence artifacts. |

The table above describes the current repair pass. The findings and dated verification sections
below retain their historical state; the final repair section supersedes their open-status summaries.

The initial audit left production behavior unchanged. The subsequent R4 repair adds local saving
for either survivor while preserving ordinary settled-state guards and automatic recovery.
Findings below distinguish native reproduction, executable regression coverage, and code-only
risks. Passing the existing suite does not establish that these lifecycle paths work.

## Reproduced bugs

### R1 — Host restart hangs after a previous successful recovery

Severity: High. An authenticated player cannot finish rejoining an otherwise retained campaign.

Reproduction:

1. Start a two-player round and reach synchronized gameplay.
2. Suspend the guest process for 45 seconds, then resume it. Let the automatic reconnect finish.
3. Verify that both clients have written new recovery checkpoints and cleared their notices.
4. Terminate the host process, leaving the guest alive. Let room ownership migrate.
5. Restart the host, select Multiplayer → Resume game, and dismiss Continue.

Observed: The host remains on `Synchronizing the game`. The guest remains on
`The returning player is loading the paused game`, even though the host has entered the galaxy.
Neither client publishes the completed-rejoin checkpoint. Both windows remain responsive, but
ordered gameplay never reopens.

Cause traced in the implementation:

- `MultiplayerCoordinator.Gameplay.TrySendInitialSnapshot` increments the sending host's
  `_revision` on each synchronization.
- `MultiplayerCoordinator.Disconnect` resets `_revision` to zero in the restarted host.
- `MultiplayerCoordinator.Recovery.EndForPeerDeparture` and `HandleRecoveryHello` retain the
  surviving guest's revision while issuing a fresh initialization identity.
- `MultiplayerCoordinator.HandleSnapshotCommit` rejects `metadata.revision < _revision`, even
  when the packet belongs to that new initialization. A returning host's revision 1 is therefore
  rejected after the surviving guest has already reached revision 2.

Evidence: `repeat-recovery-host.png`, `repeat-recovery-guest.png`,
`repeat-recovery-host.log`, and `repeat-recovery-guest.log` in the external evidence directory
listed below. Native testing used the unchanged gameplay assembly from the preceding build.

### R2 — Automatic reconnect deadlocks when the simulation host no longer owns the room

Severity: High. A temporary interruption cannot recover even though both processes and the
network connection return.

Isolated reproduction, starting from a healthy round:

1. Restart both clients and resume a checkpoint. Finish synchronization.
2. Terminate only the host, letting the guest retain ownership of the room.
3. Restart the host, resume, dismiss Continue, and verify that both notices clear and both
   clients publish fresh matching checkpoints. This first host restart succeeds.
4. Suspend that live host process for 45 seconds, then resume it.
5. Wait for the provider to close and re-establish the P2P connection.

Observed: Both logs report `Connection established` at 12:03:25 local time. The host remains on
`Player connected; negotiating game`. The guest remains on
`Player connected; verifying the returning account`. Neither starts state transfer or restores
ordered gameplay. This reproduces independently of R1: the round was fully synchronized before
the interruption.

Cause traced in `MultiplayerCoordinator.HandleConnected`:

- The guest now owns the room, so `_waitingForRejoin && _backend.IsLobbyOwner` returns while
  waiting for the counterpart's Hello.
- The live simulation host does not own the room. It passes that first condition, then
  `_role == MultiplayerRole.Host && !_returningToRecovery` also returns without sending Hello.
- `EndForPeerDeparture` explicitly sets `_returningToRecovery` to false. Both sides therefore
  wait, although the physical transport is connected. The initial handshake decision still
  mixes simulation role with room ownership.

Evidence: `clean-pair-before-migration.json`, `isolated-before-host-stall.json`,
`isolated-before-host-stall-host.png`, `isolated-before-host-stall-guest.png`,
`isolated-nonowner-stall-host-later.png`, `isolated-nonowner-stall-guest-later.png`, and
`isolated-nonowner-stall-host.log` / `isolated-nonowner-stall-guest.log`.

## Additional findings and save-permission repair

### R3 — Join game ignores the selected recovering campaign

Severity: Medium. The browser can launch the wrong resume operation or report an error about a
different campaign than the row the player selected.

Reproduction preconditions: The account has a recovery record for campaign A. A different
recovering campaign B appears in the browser. Select B and click Join game.

`MultiplayerMenuOverlay.JoinSelected` checks only
`_selectedGame.state == "rejoining" && _coordinator.HasRecovery`, then calls `ResumeGame()`
without passing the selection. `MultiplayerCoordinator.ResumeAsync` reads `_recoveryRecord`
and searches its `lobbyID` / `campaignID`, so the request follows A. No comparison with B's
campaign or room occurs on this branch. The selection is discarded before admission can
explain whether this player belongs to B.

This is a code-confirmed routing error, not a claimed native two-campaign reproduction.

### R4 — Neither survivor can manually save while waiting for the disconnected peer

Severity: Medium. The host cannot explicitly create a named save before closing the waiting
campaign. The automatic recovery checkpoint is a separate feature and does not create a normal
manual save slot.

Expected behavior clarified after the audit: the surviving host can manually save the settled,
paused campaign without requiring the disconnected guest to acknowledge that local write.

Initial implementation: `MultiplayerCoordinator.Recovery.EndForPeerDeparture` clears
`_synchronizationReady`, calls `_runtime.BlockGameOperations()`, and releases the coordinated
persistence channel for either role. `GameRuntime.CanRequestSave` then falls back to `CanSave`,
which remains false while `_operationsBlocked` is true. `SaveGameLocally` rejects that same
state. There is no host exception or separate local-save operation for the waiting campaign.

`ProcessRejoinRecovery` can still capture a complete continuation and publish it through
`MultiplayerRecoveryStore`; that automatic file bypasses the ordinary manual-save permission
path. This initially established the permission gap through a code trace.

Repair: departure now binds `MultiplayerLocalPersistence`, and the recovery loop reopens
persistence after establishing the paused writable boundary. Manual saves and quicksaves use
the survivor's original Host/Guest namespace and retain account/faction ownership. Each local
publication receives a distinct save token. Loading remains unavailable until reconnecting;
orders and time remain under the multiplayer owner. Completed local saves request another
recovery checkpoint only while the same campaign is still waiting.

Ten focused cases pass, covering both roles, names, quicksaves, namespace isolation, blocked
and executing states, rejected local loading, session replacement, and disk failure. Full and
native follow-up validation is recorded below.

### R5 — Loading a guest-only save stops synchronization

Severity: High. The guest can retain a valid local copy while the host is absent, but the shared
load path cannot use that copy after reconnection when the host lacks the same slot.

`MultiplayerPersistenceSession.RequestAsync` forwards the guest's selected identifier to the
host. `MultiplayerCoordinator.PrepareHostCheckpointAsync` calls `ResolveHostLoadFileName`,
which maps a multiplayer identifier to its host namespace unconditionally. The load then reads
only the host's local store; no guest payload is uploaded or missing-copy fallback negotiated.
An absent file throws during host preparation. `MultiplayerPersistenceSession.Fail` retains
the operation reservation and notifies `HandleGameplayFailure`; `StopGameplay` blocks runtime
operations and pauses both clients instead of rejecting only the unavailable load.

Native reproduction on the rebuilt player:

1. Start a synchronized paused turn-zero campaign and terminate the host process.
2. Use the guest's quicksave shortcut and create a named save, `Guest disconnected proof`.
   Both publish successfully while the waiting notice remains visible.
3. Restart the host, Resume, and Continue. Both clients publish synchronized paused recovery
   checkpoints. Create `Guest connected proof` from the guest; both ordinary copies publish.
4. Verify that the disconnected named save exists only in the guest directory. Select it in
   the guest's Save/Load page and click Load.

Observed: the host throws `FileNotFoundException` for the mapped host filename. Both clients
display `Game synchronization stopped` and stop admitting gameplay. They do not return to the
previous usable connected state. The local guest file remains present.

Evidence in the local-saving evidence directory below: `guest-only-load-selected.png`,
`guest-only-load-host-failure.png`, `guest-only-load-guest-failure.png`, and
`guest-only-load-host.log` / `guest-only-load-guest.log`. It is separate from the durable
local-write permission repaired in R4. An outdated existing host counterpart was not tested.

### R6 — Long synchronization errors overlap in the galaxy notice

Severity: Low. The synchronization failure in R5 produces overlapping multi-line text and
includes the host's absolute save-directory path in both players' galaxy notices.

`StopGameplay` concatenates the exception's full message into the status. The HUD controller
passes that status to `UIRequestNoticeView.Render` through the speed-request notice. Its bounds
remain `StrategyHudView.SessionNoticeWidth` by `SessionNoticeHeight` (400 by 28 source pixels)
while the full storage error is assigned to the label. The failure screenshots above show the
rendering defect. The precise font/line-spacing contribution has not been established; no
layout repair is included in the save-permission change.

### R7 — A survivor's failed quicksave gives no in-game warning

Severity: Medium. A player can believe their latest paused campaign was saved when only the
previous file remains. Atomic publication protects that file, but it does not report failure
to the player.

Native reproduction: after terminating the guest, quicksave from the host, then hold a Windows
read handle without delete sharing on that newly written `.sav` file. Press F4 again while the
handle is open. The host logs an `IOException` for the locked file; the on-screen notice still
only says that the opponent is reconnecting. No save-failure notice or dialog appears.

`MultiplayerLocalPersistence.SaveGameAsync` and `GameRuntime.QuickSaveAsync` propagate the
publication exception. `AppInputController.OnQuickSave` catches only cancellation and has no
storage-failure notification path. The native screenshot establishes the missing feedback for
this case; other storage failures were not injected.

The old save retained its exact hash. After releasing the handle, another F4 successfully
published a new save, with no leftover temporary file. Evidence: `host-local-save-locked.png`,
`host-local-save-lock-retry.png`, `host-local-lock.log`, and `host-local-lock-result.json` in
the local-saving evidence directory. No feedback repair is included in this change.

## Verification-tool defect and repair

### V1 — The long-run verifier cannot handle human combat decisions

Severity: Medium for verification coverage. This does not establish a gameplay desync.

During the initial audit, `MultiplayerNetworkSimulationRunner` was run against an isolated copy of
`network-baseline-turn1000.sav`, preserving its configured controllers, requesting 100 days.
Seed: 20261022. It completed seven days and eight shared-state checks, then timed out at tick
1008 after 146.164 seconds total wall time. The input save's hash was unchanged. Both captured
tick-1008 XML files had the same SHA-256:

`302D5970604BF193EEF25B707CC5899E534494B1E75CF07AA2DBB7BEEAF6FF14`.

A separate direct simulation of the same isolated input reached settled ticks 1001–1007,
then reported `settled=false`, `busy=true`, and `pendingCombat=true` at tick 1008. Its speed
remained Fast. The probe queried these existing flags once after each of eight days; it added
no per-entity or per-tick production instrumentation. Its temporary Editor source was removed
from the checkout after capturing the result.

The initial `OrderedPeerHarness.AdvanceDayAsync` advanced both enumerators and waited for
`Host.CanBeginHostTick` without submitting combat responses or handling `CombatDecisionRequired`.
`GameTickProcessor.BeginPendingCombatDecision` deliberately leaves the tick awaiting that
input. The verifier consequently exhausts its two-minute barrier timeout on valid paused
decision state and writes artifacts labeled as divergence, even when those graphs agree.

Evidence: `late-game.json`, `late-game.log`, `late-game-tick-1008-host.xml`,
`late-game-tick-1008-guest.xml`, `late-decision-proof.json`, and the external diagnostic source
`RecoveryAuditProbe.cs`. The requested 100-day late-game run did not complete and must not be
reported as a pass. This audit has not established a late-game simulation desync.

The verification tool now subscribes to decision events. Its optional
`-networkSimFightHumanBattles` flag submits a Fight response only for the participant whose
human faction is involved and has not responded, using `ResolveCombatRequest` through
`MultiplayerGameSession.ExecuteAsync`. Every submission and accepted/rejected result is
recorded. The presentation query runs once per decision event outside simulation steps;
no scene scan was added to an AI or simulation hot path. The change is confined to the
Editor verifier; ordinary gameplay and battle defaults are unchanged.

`-networkSimHumanParticipants` explicitly assigns both ordered participants human controllers
for that test scenario. It is mutually exclusive with `-networkSimPreserveControllers`.
Reports include the exact initial controller assignments. If a human decision occurs without
the Fight flag, the verifier stops with `waitingForCombatResponse=true` and an explanation
instead of spinning for two minutes or publishing divergence-labeled artifacts.

The new positive and negative end-to-end runs are detailed in the continued stress section
below. The historical failed run above remains a failed run.

## Checks completed without a failure

- Guest suspension for 45 seconds, transport timeout, automatic reconnection, and publication of
  matching paused checkpoints, before the host restart in R1.
- The campaign identifier, faction assignments, current day, and Pause selections remained
  consistent across that first automatic reconnect.
- Both-process restart and replacement-room discovery restored a synchronized paused campaign
  before isolating R2.
- One host crash and authenticated rejoin succeeded with the guest retaining the same room,
  before the second interruption in R2.
- Two deterministic stress runs, seed 314159, each completed 30 recoveries with alternating
  survivors and 150 simulation days without divergence. These use small synthetic campaigns,
  not late-game performance workloads.
- Recovery from either survivor with an undelivered operation commit rejected that old
  connection's traffic and restored identical independent peer graphs.
- With the room owner suspended, Escape canceled a connecting Resume request and retained the
  browser. A subsequent Resume and Continue completed, cleared both notices, and published
  matching checkpoints. Evidence: `cancel-while-owner-paused.png`, `cancel-retry-loading.png`,
  and `cancel-retry-completed-host.png` / `cancel-retry-completed-guest.png`.
- Holding a Windows read handle without delete sharing on the guest's checkpoint, then
  terminating the host, made the guest's atomic replacement fail with an `IOException` in
  `File.Replace`. The guest visibly warned that its recovery checkpoint could not be saved.
  The preceding checkpoint retained its exact hash and modification time, and no `.tmp`
  file remained after cleanup. The test handle was released afterward. Evidence:
  `locked-checkpoint-actual-guest.png`, `locked-checkpoint-actual-guest.log`, and
  `locked-checkpoint-actual-result.json`. This verifies safe failure and reporting; it does
  not establish an automatic retry policy after a filesystem lock disappears.

## Evidence and scope

### Follow-up: guest manual saving after the final reconnect

When guest saving was reported as unavailable, the host had deliberately been terminated for
the filesystem-lock test. `MultiplayerCoordinator.CanRequestSave` requires
`_synchronizationReady`, which is cleared on departure. The temporary file handle was released,
and both clients subsequently completed a rejoin and wrote matching paused turn-zero recovery
checkpoints. Evidence: `final-restored-pair.json`.

The initial attempt to save after that rejoin was inconclusive: keyboard/UI automation did not
produce a completed save, and that attempt was not counted as a pass. The later rebuilt-player
test described below explicitly verifies a guest-created named save after reconnecting: both
ordinary participant copies were published. This supersedes the initial verification gap
without treating the earlier automation failure as a diagnosed game bug.

### Save-permission repair validation

Evidence directory:
`C:/Users/David/AppData/Local/Temp/rebellion-local-saving-20261028`.

The complete required validation passed against the repaired C# sources: formatting, lint,
static analysis, architecture checks, and all 7,375 Unity EditMode tests, including the 10 new
`MultiplayerLocalPersistenceTests` cases. The complete Unity run took 96.863 seconds. Raw
results: `format.log`, `lint.log`, `full-tests.xml`, and `full-tests-unity.log`.

The standalone player was rebuilt afterward. Before launch, its `GameAssembly.dll` timestamp
was verified newer than every production C# source; its SHA-256 is retained in
`validation-manifest.json`. Native clients started a new admitted room from an ordinary save,
so old recovery files were not accepted by bypassing build compatibility.

Native results:

- Terminating the host left the guest paused at turn zero. F4 published a local quicksave,
  and the Save/Load page published `Guest disconnected proof`, with no peer process running.
  Evidence: `guest-local-quicksave.png`, `guest-local-name-edit.png`, and
  `guest-local-named-save.png`.
- Restarting the host and finishing Continue restored both peers. A subsequent guest-created
  named save, `Guest connected proof`, published both host and guest ordinary copies.
  Evidence: `guest-survivor-rejoined-host.png`, `guest-survivor-rejoined-guest.png`, and
  `guest-connected-named-save.png`.
- Loading the guest-only save reproduced R5 and R6; the clients were then closed before the
  next independent room was started. The valid local guest file was retained.
- In the second room, gameplay advanced before fault injection. Terminating the guest
  settled and paused the survivor at turn 111. The host published both a quicksave and
  `Host disconnected proof` locally. Evidence: `host-local-quicksave.png` and
  `host-local-named-save.png`.
- Locking that host quicksave reproduced R7 while preserving the previous file exactly.
  Releasing the handle allowed a successful retry, with no temporary file left behind.
- Restarting the guest restored both peers at paused turn 111, including paused speed
  selections on both factions. Loading `Host disconnected proof` then completed a shared
  scene transfer, both Continue actions, synchronization, and new recovery publications.
  Evidence: `final-rejoined-pair.json`, `host-local-load-selected.png`,
  `host-local-load-completed-host.png`, `host-local-load-completed-guest.png`, and
  `host-local-load-completed.json`. Both native processes remain reconnected and paused.

At the end of the save-permission pass, R1, R2, R3, R5, R6, R7, and V1 remained unresolved.
The save-permission repair does not claim
to repair those separate recovery, loading, presentation, feedback, or verification defects.

### Continued stress testing

Evidence directory:
`C:/Users/David/AppData/Local/Temp/rebellion-hardcore-recovery-20261028`.

This pass changes only the Editor verification tool and test fixtures. No gameplay or transport
repair is included. R1, R2, R3, R5, R6, and R7 remain open.

| Scenario | Verified interval | Result |
| --- | --- | --- |
| Preserved saved controllers; explicit Fight responses | Seed 20261022, turns 1000–1100 | 100 days, 102 state checks, four accepted Empire responses, unchanged input; 212.122 seconds total verification time. |
| Both ordered factions explicitly human; explicit Fight responses | Same input and seed, turns 1000–1200 | 200 days, 202 state checks, four accepted responses across both factions, unchanged input; 364.795 seconds total verification time. |
| Preserved controllers; no scripted response policy | Same input, reaching the turn-1008 human decision | Expected nonzero exit with `waitingForCombatResponse=true`, no submitted choice, and no divergence paths; 27.278 seconds total. |
| Recovery after the first battle response | Either survivor, either first responder, every Fight/Retreat pair | All 16 combinations pass; the recorded first choice survives, the second remains outstanding, and both continuations settle identically after the second response. |
| Delayed reliable delivery during incremental ticks | Scheduler seeds 11, 42, 314159, and 20261028 | 1,344 speed requests, 144 days, and 16 recoveries; final participant selections and independent graphs agree. |
| Corrupt recovery storage and concurrent publication | Invalid declared lengths, trailing bytes, mismatched campaign/account/faction/hash headers, and parallel account/manual writes | All 12 cases pass: malformed checkpoints are rejected before restoration, and concurrent ordinary/recovery files remain independently readable. |

The two-human report records both `FNALL1` and `FNEMP1` as Human. It reports exactly one
initial snapshot transfer, no later snapshot transfer, and stable game references throughout.
The preserved-controller negative report records Alliance AI and Empire Human, which also
establishes the configuration of the identical input used by the first run. The detailed JSON
reports retain faction planets and ownership changes, assaults, fleets and units, manufacturing
categories, personnel captures/deaths, and victory state.

Raw simulation evidence: `late-fight-100.json`, `late-two-human-200.json`, and
`late-no-combat-policy.json`, with corresponding logs. The two positive runs exercise real
TCP framing and ordered simulation, not EOS admission, relay behavior, or reconnection.
The delayed-delivery cases preserve each sender's reliable packet order; they do not emulate
arbitrary packet reordering, real-time network timeouts, or internet loss.

The negative run is an expected stopped verification, not a passing nine-day simulation. No
unrequested battle choice is used to make that run progress. Its empty divergence paths must
not be represented as proof that no mismatch could exist in an uncompleted barrier.

#### Native load interruption

Both clients entered a shared load and remained on Continue. Terminating the guest made the
host show the load-failure presentation, return to the menu, and display the departure
acknowledgment. Escape retained that acknowledgment. The host's previous durable checkpoint
retained its exact hash. Acknowledging, resuming from disk into a replacement room, and
restarting/resuming the guest produced new matching paused turn-111 recovery publications.

Evidence: `interrupt-load-before-host.png`, `interrupt-load-before-guest.png`,
`interrupt-load-after-guest-crash-host.png`, `interrupt-load-escape-retained.png`,
`interrupt-load-resumed-host.png`, `interrupt-load-resumed-guest.png`, and
`interrupt-load-resumed-pair.json`. Baseline checkpoints and logs were copied before fault
injection. The missing menu-model warning was present in baseline logs too; its presence
after interruption is not evidence of a newly caused recovery fault.

The separate attempt to interrupt after only the host had continued was not counted:
subsequent injected save/load clicks and shortcuts did not establish the required loading
state reliably. Cause not established. Neither input-delivery failures nor uncompleted UI
attempts are counted as game bugs or successful scenarios. Matching recovery publications
alone do not prove that every subsequent user control works.

A later audit build established the previously missing half-loaded precondition. Starting from
a synchronized paused turn-0 round, the host requested a shared load, continued into the
galaxy, and displayed `Game loaded; waiting for the other player`. The guest remained on
Continue. Both states were captured before terminating the guest. The host returned to the
menu with the departure acknowledgment and retained the exact prior checkpoint hash
`81F1159BBEC97E86DFF8FA68AFFEC14F232E70210CF6ACC9AB058540BE2AEFCF`.
Acknowledgment and disk resume, followed by guest restart/resume, returned both coordinators
to Connected with chat/save admission reopened and shortcut suppression cleared. The options
shortcut, save tab, and new-save editor subsequently responded to ordinary OS input.

Evidence for this follow-up is under `native-audit`: host captures `frame-1793221488739.png`
and `frame-1793221524689.png`, guest capture `frame-1793221489907.png`, the immutable command
responses, `half-load-checkpoint-preservation.json`, `half-load-baseline-*.checkpoint`, and
`after-half-load-*.checkpoint`. Capture files live in the corresponding role control directories.
Both actual game windows were used; the audit observer resolves UI targets and records public
state without invoking game handlers. Its separate assembly is omitted from a verified ordinary
player build. Failed observer queries and input attempts are not counted as runtime defects.

#### Timing observation

In the two-human 200-day run, host incremental tick CPU time had a median of 105.979 ms,
p90 of 150.219 ms, and p99 of 234.351 ms. Guest values were 72.744, 177.287, and 293.849 ms.
These include synchronous tick-completion subscribers. Barrier wall time had a median of
543.799 ms and p99 of 951.398 ms. The verifier's separate full-graph comparison added a
median of 1,134.792 ms per day and is not a native rendering cost.

These measurements warrant further profiling; they do not diagnose the reported native FPS
drop. Cause not established. No AI rules, tick cadence, candidate limits, or production
diagnostic polling were changed to produce these results.

#### P1 — Receipt verification timing

A separate control and instrumented run use the same turn-1000 input, seed 20261022,
two human participants, explicit Fight responses, and 20 days. Existing bounded phase timing
is enabled only for the instrumented run; no gameplay implementation or tick cadence changes.
Both finish at turn 1020 with identical world hashes, faction outcomes, battle responses, and
victory state. The input hash remains unchanged and both reports identify the same loaded source.
Control wall time is 49.480 seconds; instrumented time is 49.355 seconds, a difference of -0.252%.
One pair of runs is insufficient to establish a performance improvement.

| Receipt verification | Samples | Median | p90 | Maximum |
| --- | --- | --- | --- | --- |
| Host | 20 | 97.176 ms | 169.627 ms | 320.094 ms |
| Guest | 20 | 88.935 ms | 95.314 ms | 160.936 ms |

No timing samples are dropped. Receipt verification is the largest measured individual phase;
movement medians are 28.213 ms and 28.885 ms respectively. The implementation establishes its
frequency and ownership: `MultiplayerGameSession.HandleTickCompleted` sets a pending receipt,
and the subsequent `Pump`, called from `MultiplayerCoordinator.Update`, synchronously calls
`MultiplayerStateFingerprint.ComputeSimulation` once at the completed-day barrier. This cost
is outside the incremental tick iterator and therefore absent from the earlier tick CPU figures.

This identifies a measured main-thread cost, not the cause of the reported native FPS drop.
Cause not established. The sample uses Editor execution, only 20 days, and two peers sharing a
process and static reflection-plan caches. Separate native players and cold caches may differ.
The verifier's independent full-XML comparison is also excluded from production phase costs.

Evidence: `profiles/control-20.json`, `profiles/timed-20.json`, their logs, and
`profiles/comparison.json` in the continued-testing evidence directory.

#### U1 — Native save follow-up after disk resume

After restarting both audit clients and resuming the retained campaign, both report Connected
at paused turn 0. Public runtime observations report `CanSave=true`, `CanRequestSave=true`,
`Tick.IsSettled=true`, `Tick.IsBusy=false`, and `CanExecuteCommands=true` on both sides.
Guest F4 attempts and a new-save form attempt produced no new `.sav` in either isolated role
directory during the observation interval. The form had focused text entry and an enabled Save
button before submission; its editor closed afterward. The runtime observations remained writable.

Cause not established. These observations do not establish that the save callback ran, which
protocol stage was reached, or whether this occurs without the audit build. This is an unresolved
follow-up, not a diagnosed persistence deadlock or a reversal of the earlier verified survivor-save
results. F4 is reported in the active binding display, but a binding display alone does not prove
delivery of that key event. The speed-agreement follow-up was not completed and is not counted.

The audit automation itself required corrections: response-file replacement raced a reader,
a destroyed Unity selection needed Unity's null check, and focusing through a click inside the
viewport deselected text fields. Those are verification-tool defects. Corrected queries report
public runtime boundaries and input bindings; no private game handler is invoked. Windows focus
guard failures and commands attempted before a form became available are discarded.

Evidence: `native-audit/unresolved-save-followup.json`, immutable responses in
`guest-control-1793222665315` and `host-control-1793222665315`, guest capture
`frame-1793222941700.png`, and the corresponding player logs. The audit source and artifact hashes
are retained in `native-audit/artifact-manifest.json`.

#### Continued-pass validation

All 7,407 Unity EditMode cases pass, including 32 new cases in this pass: 16 battle-recovery
combinations, four seeded delayed-delivery runs, and 12 storage cases. Formatting, lint,
static analysis, and architecture checks also pass. The full Unity run took 103.478 seconds;
no C# source was modified after that run started. Evidence: `full-tests.xml`,
`full-tests-unity.log`, `format.log`, `lint.log`, and `validation-manifest.json` in this pass's
evidence directory. The manifest retains source hashes and the unchanged native assembly hash.

The earlier targeted storage run had six test assertion failures: the implementation correctly
threw `InvalidDataException`, while the new tests incorrectly expected `IOException`. Inspection
of the validation branches and the actual exception output established the mismatch; only
the test expectation was corrected. These were test defects, not six product failures. The
complete passing run includes the corrected assertions.

After the audit observer changes, the complete suite again passes all 7,407 cases in 103.456
seconds. Formatting, lint, static analysis, and architecture checks pass against that C# state.
No C# source changed after validation began. The tested observer source matches its native audit
artifact, the production assembly is newer than all production C# sources, and a normal player
build contains no `NativeAudit.dll`. Evidence: `audit-full-tests.xml`, `audit-full-tests-unity.log`,
`audit-format.log`, `audit-lint.log`, and `audit-validation-manifest.json`.

Still requiring stronger evidence: U1 and native speed commands after recovery, additional
large-world seeds, real EOS loss/relay behavior, and separate-process native performance profiling.
The half-loaded interruption now has the native follow-up described above. No FPS cause is
claimed from the aggregate timing measurements, and uncertain input attempts are not counted
as game defects or passing scenarios.

### Evidence location and validation

External evidence directory:
`C:/Users/David/AppData/Local/Temp/rebellion-recovery-adversarial-20261028`.

The starting standalone gameplay assembly was newer than every production C# source. Native
faults target only the two controlled standalone processes. Their recovery files were backed
up before fault injection; ordinary save slots are not used as destructive test fixtures.

After adding four adversarial cases, the complete suite passed all 7,365 tests. Formatting,
lint, static analysis, and architecture checks also passed. The targeted multiplayer suite
passed 318 cases before the final strengthened stale-commit assertion; the full run includes
that final assertion. Tests are in `MultiplayerGameSessionTests.cs`; raw results are in
`full-tests.xml` and `multiplayer-tests.xml` in the evidence directory.

The automated stress cases exercise the simulation protocol. They do not recreate the retained
coordinator state that reproduces R1 and R2. These results must not be represented as proof of
long-session or arbitrary internet reliability.

Not exercised: three distinct accounts, a native two-campaign browser selection, relayed
internet traffic, controlled packet loss/rate limiting, full-disk failure, power loss during
flush, and process termination during the final disk replacement. Native UI automation that
missed a control or failed to obtain window focus was discarded, not counted as a game bug
or a passing scenario. The interrupted 100-day late-game run is explicitly incomplete above.


## Recovery and persistence repair pass

This pass stays in v5 on the existing local `multiplayer` branch. It changes no save schema,
scene-registry format, strategic-AI rules, or day-verification cadence. Protocol admission is
version 11 because guest load requests now include the selected snapshot payload and hash.
Both participants must use the same compatible build.

### Repaired ownership boundaries

- `MultiplayerSnapshotAdmission` scopes revision comparisons to one initialization. A restarted
  host may begin at revision 1 without a previous initialization rejecting its snapshot.
- `MultiplayerCoordinator.HandleConnected` initiates Hello according to room ownership, and
  `HandleRecoveryWelcome` accepts the authenticated offer for either original simulation role.
  `NotifyPresentationReady` distinguishes initialization as well as session identity: replacing
  the world inside a retained session still requires the new readiness handshake.
- `BindRuntime` no longer creates shared persistence while recovery is using a provisional
  initialization. `StartOrderedGameplay` establishes it after the agreed state handshake.
  The earlier disk-resume trace does not establish why injected save shortcuts failed, but this
  inspected identity mismatch independently prevented matching persistence messages.
- Browser joining validates the selected campaign before beginning recovery and uses that row's
  room. It cannot silently resume a different local campaign. The dedicated Resume action still
  performs campaign discovery without a selected browser row.
- Guest loads send the selected saved world and its integrity hash. The host checks content and
  authenticated participant bindings before issuing the existing shared preparation/transfer flow.
  A matching host file is optional and contributes only that participant's saved presentation.
- Manual save/load storage exceptions are logged with their details and reported through an
  application-owned notice. The shared dialog owns acknowledgment, cancellation routing, and
  shortcut suppression. Synchronization failure status is concise; compact notices ellipsize.

### Final validation and native reproductions

`dotnet csharpier check Assets/`, `bash ./build.sh lint`, and `bash ./build.sh test` passed against
these final C# sources: all **7,431** EditMode tests passed. Lint includes static analysis, member
ordering, analyzer tests, and architecture checks. No production C# source changed after final
validation began. The native gameplay assembly was rebuilt after the last readiness correction
and was newer than the production and audit C# sources before execution.

Native checks used two isolated authenticated processes with the same rebuilt gameplay assembly:

- Loading a guest-only save with no host counterpart restored both clients at turn 1. Both could
  save afterward, agree on Fast, advance to turn 3, and agree on Pause.
- Suspending the guest for 45 seconds, reconnecting, and saving completed successfully. Terminating
  the host afterward, restarting it, reconnecting, and saving also completed successfully.
- With the guest owning the surviving room, suspending the live original host for 45 seconds
  recovered both clients and completed another guest-requested shared save at paused turn 3.
- Locking the surviving host's existing quicksave caused a reported failure without changing the
  previous file hash. The authored acknowledgment dialog was visible. Clicking its check cleared
  the notice and shortcut suppression; retrying quicksave succeeded.
- Terminating both processes, reopening the durable campaign, rejoining, and issuing a save from
  the guest completed successfully at turn 3, publishing ordinary saves for both participants.

Save, load, speed, and battle checks invoke public service contracts in the explicitly enabled
native audit build. They establish native lifecycle, transport, and disk behavior, not physical
shortcut delivery. The acknowledgment check uses actual pointer input. No private callbacks or
reflection-based state injection are used. The old native build's failed readiness attempt was
fixed and rerun; its intermediate results are not evidence for the final revision.

Evidence directory:
`C:/Users/David/AppData/Local/Temp/rebellion-multiplayer-repairs-20261028`.
Relevant files include `final-format.log`, `final-lint.log`, `final-tests.xml`,
`final-tests-command.log`, `repairs-native-build-2.log`, `repaired-native-artifact-2.json`,
`guest-only-load-repaired.json`, `repaired-speed.json`, `repaired-guest-stall.json`,
`repeated-host-recovery.json`, `nonowner-host-reconnected.json`, `storage-warning-repaired.json`,
`storage-warning-repaired.png`, and `final-disk-resume-save.json`.

### Native receipt-verification profile

The rebuilt native clients loaded the unchanged turn-1000 input with seed **20261022** and ran
through turn **1020** with both factions controlled by humans. Both explicit Fight responses at
turn 1008 passed through the ordered command boundary. Both clients remained connected, ended at
paused turn 1020, and published a shared save. Saved entity counts and planet ownership agree
between the two participants. Input SHA-256:
`80EF17EEC5CEACBB575EE4E191E03F3B5A32239D9F024597C7854247DAA37FFE`.

| Native receipt verification | Samples | Median | p90 | Maximum | Total |
| --- | --- | --- | --- | --- | --- |
| Host | 20 | 177.416 ms | 182.779 ms | 182.853 ms | 3,480.528 ms |
| Guest | 20 | 162.900 ms | 171.525 ms | 193.348 ms | 3,207.502 ms |

No timing samples were dropped. The measured implementation runs once per completed day per peer,
in `MultiplayerGameSession.Pump`, outside the incremental tick iterator. The existing
`StateVerification` timing scope encloses `MultiplayerStateFingerprint.ComputeSimulation`, which
captures continuation facts and traverses the persisted simulation graph. This is a measured
main-thread stall and is the largest aggregate captured phase in this sample.

Performance work remains. This run does not establish the cause of the previously reported native
FPS drop: cause not established. It contains only 20 days, one seed, two local processes on the
same machine, and explicit audit requests. The 40.309-second wall interval includes polling,
battle input, and speed agreement; it is not simulation CPU time. This is not a native before/after
optimization comparison. No hashing protection was removed and no performance improvement is claimed.

Evidence: `native-profile-host.json`, `native-profile-guest.json`, `native-profile-run.json`,
`native-profile-analysis.json`, and `native-profile-worlds.json` in the repair evidence directory.

The repository-wide `git diff --check` additionally reports whitespace in dirty Unity rendering
and font assets. Those assets were not cleaned or staged by this repair. The required source
format, lint, architecture, and test checks above passed; no push or PR was created.

## Acknowledgment artwork correction

The shared acknowledgment mode now uses a dedicated background with one button recess and its
matching confirm-button position. Normal yes/no prompts retain their two-action background and
restore their button placement when the same view changes modes. The texture is included in
the media repository and the local installed content.

All 501 selected component, main-menu, and options-menu UI tests passed, together with formatting
and lint checks. The native player was rebuilt and its loaded assembly was newer than the changed
sources. Interrupting a launch by terminating the guest displayed the single-action disconnect
notice in the main menu; actual pointer input on its checkmark dismissed it. An initial capture
and click occurred during the loading transition and were repeated after the notice became
visible. These UI checks follow the earlier complete-suite pass; that earlier result describes
the preceding repair revision.

Evidence: `C:/Users/David/AppData/Local/Temp/rebellion-acknowledgment-art-20261028`, including
`ui-tests.xml`, `format.log`, `lint.log`, `native-build.log`, `native-result.json`, and
`acknowledgment-native.png`.

## FPS demo and speed-menu participant markers

The FPS demo had V-sync enabled with the frame limiter already set to Unlimited.
`GraphicsManager.Apply` mapped that preference to `QualitySettings.vSyncCount = 1`.
Turning V-sync off through the existing Video controls in both native clients removed the
ceiling; a subsequent guest capture showed 422 FPS. This setting change does not repair P1's
measured daily verification cost.

The speed menu previously received only the local acknowledged selection. HUD projection now
includes each human participant's configured faction color. Shared context-menu rows reuse the
selection-frame component for every selecting owner, retaining both colors on an agreed row.
An owner-managed selection state updates open menu markers without replacing commands or
discarding hover. Ordinary selected commands retain their existing single-owner presentation.

All 413 selected UI component, HUD, and context-menu tests passed, along with formatting and lint.
Native verification saved and restored the user's active round at turn 1403, displayed both
colors for an agreed Pause, then changed only the guest to Fast while the host menu remained
open. The host menu moved the guest's marker to Fast while retaining the host's Pause marker;
the guest's menu displayed the same acknowledged choices. Both native clients remained connected
and paused at the saved turn. The updated windows were left open for interaction.

Evidence: `C:/Users/David/AppData/Local/Temp/rebellion-speed-menu-selections-20261029`, including
`live-ui-tests.xml`, `format.log`, `lint-2.log`, `native-build.log`, `saved-progress.json`,
`native-result.json`, `host-menu-agreed.png`, `host-menu-peer-changed.png`, and
`guest-menu-different.png`. The uncapped capture is
`C:/Users/David/AppData/Local/Temp/rebellion-multiplayer-fps-demo-20261028/guest-uncapped.png`.

## Slowest participant speed policy

The current policy applies the slowest human preference in the order Pause, Very Slow, Slow,
Medium, Fast. Either participant can pause without approval. Choosing a faster preference cannot
override a slower counterpart; raising the slower preference raises the effective clock without
requiring identical selections. This supersedes the earlier exact-match policy, including its
requirement for both players to select Pause.

`GameManager.ApplyPlayerGameSpeed` resolves the effective speed at the existing ordered action
boundary using an explicit speed order, since enum numeric values do not represent clock order.
Per-player preferences and revisions remain independent. Faster-request responses retain their
revision checks, and closing Options restores only the local preference.

Formatting, lint, static-analysis, architecture checks, and the complete suite passed: all
7,490 tests. The clock regressions cover all 25 preference pairs in both arrival orders, immediate
Pause from either role, faster proposal acceptance/decline, stale responses, and Options restoring
the slower counterpart's limit. Historical exact-match assertions were updated to the new policy.

The rebuilt native clients passed eight real-transport clock cases: different active speeds,
raising the slower selection, either role becoming the slower participant, unilateral Pause from
each role, resuming under the remaining lower limit, and both selecting Fast. The user's current
round was saved before the update and restored afterward at turn 2880. Both updated windows were
left connected and paused, with the host at Pause and the guest at Fast.

Evidence: `C:/Users/David/AppData/Local/Temp/rebellion-slowest-speed-20261029`, including
`full-tests-2.xml`, `full-tests-2.log`, `format.log`, `lint.log`, `native-build.log`,
`native-clock-cases.json`, `saved-progress.json`, `native-result.json`, `host-restored.json`,
and `guest-restored.json`. Earlier failed runs retain obsolete-policy test expectations and are
not represented as passing results.
