# Multiplayer interface progress and verification

Current departure behavior and its verification are maintained in
[MultiplayerSessionExit.md](MultiplayerSessionExit.md). The interface evidence below includes
earlier revisions; current recovery retains active campaigns and uses Resume game after restart.

The multiplayer interface uses the shared native controls. Either connected player can request a
save; the host coordinates a settled checkpoint, and both participants keep a local copy. Speed
changes, including Pause, require matching selections. Combat waits for both human responses.

## Completed changes

- [x] Replacing the permanent speed box with a compact request and the existing accept and decline artwork.
- [x] Marking the local speed selection and battle response with the player's faction color.
- [x] Showing only the battle waiting message, without the additional local response line.
- [x] Keeping a save-name draft open through focus changes until explicit submission or cancellation.
- [x] Preventing a closed dropdown from taking keyboard selection during unrelated Options repaints; the regression reproduces the previous selection change.
- [x] Separating save request availability from the immediate snapshot boundary.
- [x] Displaying actual connection and recovery status in the strategy view, above modal windows.
- [x] Aligning the browser's empty message to the left and displaying its active search state.
- [x] Rejecting stale speed responses through the existing ordered command boundary.
- [x] Adding shared galaxy-size and victory-condition dropdowns to the lobby, editable by the host for new games.
- [x] Invalidating both ready approvals when the setup changes and rejecting stale approvals.
- [x] Preserving loaded saves' setup and validating that received lobby settings match the launch payload.
- [x] Allowing discovery controls to finish independently of general-chat admission; keeping chat failures in its transcript.

## Verification progress

- [x] Completing native setup replication and launch checks with the rebuilt player.
- [x] Completing the clean guest-departure and rejoin path, including connection cancellation and retry.
- [x] Completing the representative failure and window-lifecycle checks below; keeping native-only coverage limits explicit.
- [x] Running the complete checks against the final implementation and regression tests.

## Open findings

- Native process interruption during an individual disk write has not been exercised. Native
  interruption while a save awaited the peer's checkpoint receipt was exercised; controlled
  persistence tests separately cover delayed writes, write failure, queued requests, and cancellation.
- Deliberate state divergence is covered by independent-peer tests, coordinator failure-notice
  tests, and HUD projection tests. No native process-memory corruption was injected.
- A programmatic window-close run on the preceding UI build remains unresolved: the host paused correctly,
  but updating its recovery listing returned `Prepare game listing: NotFound`. The restarted
  guest found no game. The reason the service listing disappeared is not established. A later clean
  return-to-menu, cancellation, retry, and rejoin completed with the rebuilt player. Quitting to
  desktop and restarting also rejoined successfully. The rebuilt player's normal window X exited
  without a crash or listing-update error. This does not establish the cause of the earlier incident.
- One browser client on the preceding UI build crashed during programmatic window closure,
  with a Unity window-procedure stack. The cause is not established; there is no managed exception
  identifying an application path. The current player's Options Quit and normal window X checks
  did not reproduce it.

## Verified behavior

The interface revision described below passed **7,279 EditMode tests**, formatting, lint,
static analysis, and architecture checks. Its rebuilt standalone player also passed the native
checks below. Current recovery validation, including the 7,361-test suite and full process
restart, is recorded in [MultiplayerSessionExit.md](MultiplayerSessionExit.md#recovery-verification).

The save-input focus fault was traced through `OptionsMenuView.Render`: hiding an already closed
`TMP_Dropdown` called `Selectable.Select` and deselected the input on each repaint. The view now
hides only an expanded dropdown. A regression reproduced the incorrect selection, and native
typing retained the complete draft while turns advanced. Temporary diagnostic callbacks were
removed before rebuilding the verified player.

| Area | Automated coverage | Native verification |
| --- | --- | --- |
| Speed agreement | Matching and different selections, every speed including Pause, repeated stepping, acceptance, decline, stale revisions, and both participant roles. | A remote request appeared; declining removed it without changing the agreed speed; accepting Pause paused both players. |
| Combat responses | All four response combinations, illegal withdrawal, recovery after the first response, and projection from either player's perspective. | A local response received its faction outline and waiting message. The guest closed before responding, rejoined the pending encounter, and completed resolution without losing the host's first response. |
| Save editing | Losing focus and repainting preserves the draft until explicit Save. Snapshot readiness remains separate from request admission. | Complete host text survived turns 484–488; complete guest text survived turns 513–518. Both fields retained the caret and accepted explicit Save. Earlier checks also covered remote speed requests with Options open. |
| Shared saves | Coordinated persistence, participant-specific storage, concurrent saves with either participant first, and cancellation of both queued requests. | Host- and guest-created named saves reached both local stores with matching checkpoint ticks and tokens at turns 508 and 519. |
| Connection notices | Current connection text takes priority over speed controls; the notice layer sits above modal windows. | Closing the guest during an active round paused the host and displayed the waiting-for-rejoin notice. |
| Browser messages | Existing browser and menu regressions remain in the full suite. | The active search and empty messages appeared on the left. |
| Host configuration | All six size/victory combinations survive serialization; changed settings clear approvals; unchanged settings preserve them; guest and invalid requests cannot change setup; loaded saves remain fixed. | Small/Headquarters reached the guest; changing to Medium cleared both approvals and preserved the chat draft. Launching Small/Headquarters produced matching settings and seed in both local saves. |
| Saved-round launch | Saved participant ownership and readiness checks. | Loading the shared turn-16 save in the lobby launched both clients into their saved factions and paused state. |
| Passwords and cancellation | Scoped proofs and form cancellation ownership. | Empty and incorrect passwords were rejected; the correct password admitted the guest. Escape cancelled a connecting rejoin, retained the browser, and allowed a subsequent retry. |
| Clean rejoin | Recovery state and stale initialization tests. | Guest returned to the menu; host paused and advertised the game. Retrying after cancellation restored the guest, showed the loading notice on the host, and cleared it after synchronization. Both clients subsequently advanced to turn 6 at the agreed Fast speed. |
| Desktop lifecycle | Existing cleanup and recovery tests. | Options Quit exited; the restarted client discovered and rejoined the paused game. Normal window X also exited without a crash or listing error. Leaving as host displayed connection loss and paused the guest. |
| Interrupted load | Replacement-scene synchronization and disposal tests cover requests from either role. | Closing the guest at the shared-load Continue screen cancelled the load and returned the host to the main menu. |
| Interrupted save | Delayed receipt/write, failure, concurrent request, and disposal tests. | Suspending the guest before a save left zero new save files before its receipt. Terminating that test guest cancelled the request and eventually displayed waiting for rejoin above the still-open Options window. The guest then rejoined; a new save wrote matching participant copies at turn 0. |
| Divergence notices | Independent graphs with deliberately changed unit state stop before another day. Coordinator failure publishes the stopped-session reason, and HUD projection prioritizes connection notices over speed requests. | Native divergence injection was not performed. |

The native battle check used an external verification save derived from an existing campaign,
with one fleet moved into an opposing fleet's system. It exercised actual admission, battle UI,
ordered responses, departure, and recovery. It is not evidence about naturally occurring battle frequency.
The derived input and its metadata were removed from the player's save list after verification;
the external evidence copy remains separate from the checkout.

After normal window X, the transport reported interruption six seconds after process shutdown and
closed the connection after eleven seconds in this run. The host then displayed waiting for rejoin;
the restarted guest discovered and rejoined it. This is one measured detection interval, not a
guaranteed timeout for every network condition. Forced process termination took longer than the
initial thirteen-second observation window to surface the interruption notice.

The native speed and shared-save checks used two authenticated clients over a direct EOS
connection. These checks establish those interactions; they do not establish long-session
performance or cover every network interruption.

Six ordered TCP-loopback runs completed 100 ticks each, using seed **314159**, every combination
of Small/Medium/Large and Conquest/Headquarters, and independent host/guest graphs. Each completed
102 state checks without divergence. These runs preserve configured faction controllers and
record the loaded source and assembly identities. They verify replay for these six cases;
they do not replace EOS lifecycle or long-session testing.

A separate Small/Headquarters run, seed **271828**, completed **500 ticks and 502 state checks**
without divergence in 263.9 seconds. Across these seven runs, 1,100 ticks and 1,114 state checks
passed. This is deterministic replay evidence, not a latency or FPS benchmark.
These runs preceded the final Options focus guard and its UI regressions; simulation code did
not change afterward. The native focus and interruption checks used the rebuilt player.

## Failure and lifecycle checks

Keep this checklist current as each case is exercised. A normal success path does not substitute
for the interrupted case.

- [x] Checking all supported galaxy-size and victory-condition combinations on both participants through serialization and independent replay runs.
- [x] Checking host-only setup ownership, unchanged-setting updates, readiness invalidation, and stale readiness through model tests and native changed-setting checks.
- [x] Checking that loaded saves retain their saved setup and participant ownership through model/admission tests and native saved-round launch.
- [x] Checking native search completion, text filtering, selected-column sorting, and selection retention after refresh. Form cancellation and scoped failures are covered separately by operation tests.
- [x] Checking that hosting and joining remain available while general chat is not connected in the native player; chat admission runs independently of the browser command.
- [x] Checking protected-game admission with empty, incorrect, and correct passwords and connection cancellation in the native player.
- [x] Checking named saves from both players during native ticking, with controlled concurrent-request tests covering either participant while another checkpoint is pending.
- [x] Checking that unrelated updates preserve open Options, typed save/chat drafts, and browser selection during cancellation and retry.
- [x] Checking both sides' departure notices, guest recovery after return-to-menu, Options Quit, and normal window X, and saved-campaign restart after host departure.
- [x] Checking native departure during loading, checkpoint reservation, and a pending combat response; checking pending disk writes separately through controlled persistence tests.
- [x] Checking stale initialization messages, reordered or duplicated commands, and incompatible participant admission through the protocol/admission tests.
- [x] Checking deliberate state divergence stops independent peers, failure publishes its notice, and HUD projection exposes connection notices. Native corruption injection remains outside the exercised cases.
- [x] Checking deterministic continuation over sustained rounds with different galaxy sizes and victory conditions through seven verified-build runs.

## Required validation

Run these sequentially in the v5 checkout after the final source changes:

```bash
dotnet csharpier check Assets/
./build.sh lint
./build.sh test
```

Rebuild the standalone player before native verification. Use the same compiled simulation and
content identity on both clients. Record the configuration, seed, tick interval, and result of
each sustained simulation separately from UI interaction checks.
