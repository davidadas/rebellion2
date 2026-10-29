# Multiplayer session exit

An opponent's departure pauses an active campaign and blocks new orders and shared loads. An
already executing simulation operation first reaches a writable boundary; recovery then captures
the complete runtime, including pending battle responses, into an atomic account-specific file.
The survivor stays in the galaxy view with a notice naming the missing player.

After the pause is established, either surviving participant can make named saves, overwrite their
own multiplayer copies, and quicksave locally without an acknowledgment from the absent player.
The ordinary settled-state save guard still applies; an unfinished battle is retained by the
separate full recovery checkpoint. Local saves keep the original campaign, account, and faction
bindings and receive a distinct checkpoint token. They never overwrite the other role's history.
Completing a local save also requests a fresh recovery checkpoint while the same session remains
waiting. Loading and advancing simulation still require both participants.

After rejoining, a guest-selected load uploads that saved world with its integrity hash. The
host verifies its content and authenticated campaign bindings before both participants prepare
the replacement. A matching host file is optional and restores only that participant's saved
presentation. Guest-only saves therefore remain usable without a duplicate host copy.

Failed manual saves or loads retain the detailed exception in the log and display a concise
acknowledgment through the shared dialog. The application owns the pending notice; scene teardown
releases its controls and shortcut suppression without consuming the acknowledgment.

EOS lobby ownership may move to the survivor. Simulation authority and faction ownership remain
bound to the original authenticated accounts. A restarted player opens Multiplayer and selects
**Resume game**. The survivor supplies its paused continuation; build, gameplay content, campaign,
participant, faction, and simulation-state checks must pass before ordered play opens again.
Room ownership determines who initiates the recovery offer; either simulation role may receive it.
Snapshot revisions and ready acknowledgments belong to the negotiated initialization, including
when recovery replaces the world inside an existing session object.
Both speed selections reset to Pause. The clock uses the slower selection, so both players must
select a running speed before time advances. Either player can pause independently.

Recovery checkpoints are written after initial synchronization, after completed shared saves or
loads, and after disconnection. They contain one complete world payload and do not replace manual
save slots. If no room survives, the original host can reopen its last durable checkpoint; the
guest then finds the replacement room through Resume game. Simultaneous process loss can only
recover progress already written to disk.

Departure during an unfinished scene transition retains the blocking acknowledgment path and
returns to the main menu. Reopening a checkpoint uses the normal native loading screen.

## Completed implementation

- [x] Retaining the pending notice through connection teardown and scene replacement.
- [x] Reusing the common confirmation artwork with one acknowledgment action.
- [x] Keeping the acknowledgment above existing and subsequently opened modal windows.
- [x] Consuming Escape without dismissing the acknowledgment.
- [x] Suppressing menu and gameplay shortcuts through an independent input lease.
- [x] Releasing only the input ownership acquired by the notice.
- [x] Preventing duplicate menu transitions and restoring the notice when navigation fails.
- [x] Showing the pending acknowledgment in the main menu after interrupted scene loading.
- [x] Canceling superseded loading coroutines and invalidating their scene callbacks together.

Connection-loss detection belongs to the EOS transport. Recovery does not add a second heartbeat
or timeout policy. The acknowledgment behavior below applies to interrupted scene transitions.

## Recovery verification

On October 28, 2026, the recovery implementation passed all 7,361 EditMode tests,
`dotnet csharpier check Assets/`, and `bash ./build.sh lint`, including the static-analysis
and architecture checks. The standalone build succeeded, and its gameplay assembly was newer
than every production C# source before the final native check.

The native check force-terminated both authenticated clients after their turn-zero checkpoints
were durable. The host reopened its saved campaign through Resume game, creating a replacement
room. The guest used its previous room locator to find that replacement and rejoined through
Resume game. Both clients then published checkpoints with the same campaign and replacement
room, the same original faction assignments, turn zero, and both speed selections set to Pause.
The waiting notices cleared after synchronization. No recovery or state-divergence exception
was reported in either player's log.

Individual host and guest crashes were also exercised during recovery development. The survivor
retained the room through lobby ownership migration; the restarted participant rejoined its
original faction and simulation role. Those checks preceded the final directory guards for
deleted and ownerless service entries. The final full-restart check used those guards.

Automated coverage includes account and campaign admission, content/build identity, atomic
checkpoint replacement, corruption and truncation, directory disappearance and replacement,
cancellation, stale connection messages, restoration after the first battle response, and
twenty further simulation days after recovery from either participant's role. Native checks
use two local authenticated processes; they do not establish long-session network reliability
or durability when a process is killed during the disk write itself.

## Earlier acknowledgment verification

The implementation adds focused tests for notice lifetime, connection teardown retaining the
notice, shared acknowledgment controls, shortcut ownership, modal stacking, repeated submission,
navigation rejection/failure, callback ownership, and scene teardown.

- [x] Completing formatting, lint, static analysis, architecture checks, and the full EditMode suite.
- [x] Rebuilding the native player and checking departure with Options open.
- [x] Checking Escape, close-all, and Options shortcuts while the notice is visible.
- [x] Checking acknowledgment returns to the main menu without showing the notice again.
- [x] Checking both participant roles and departure during a pending battle.

On October 28, 2026, `dotnet csharpier check Assets/`, `bash ./build.sh lint`, and
`bash ./build.sh test` passed against the final C# sources. The complete suite passed all
7,318 tests. The loading integration fixture separately passed all seven cases, including
interruption during initial reconstruction, committed Ready, dismissed Continue, and the
Continue fade. These cases verify callback ownership and discarded gameplay activation.

The standalone player build succeeded, and its gameplay assembly was newer than every
production C# source before native verification. Closing the guest displayed the host's
departure notice. Force-terminating the host during a pending battle displayed the guest's
notice above both the battle window and the open Save/Load page. Escape, Ctrl+W,
Shift+Escape, and F9 left the guest notice visible. Clicking its check returned to the main
menu without reopening the notice. A saved multiplayer campaign was hosted again between
these checks. Loading interruption coverage is automated; native departure checks cover
the strategy presentation.

Native verification uses the same rebuilt player and content on both authenticated clients.
Earlier interface and recovery evidence is retained in [MultiplayerVerification.md](MultiplayerVerification.md).
