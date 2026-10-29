# Development

## Requirements

- Unity 6000.4.12f1
- .NET SDK
- A Bash-compatible shell
- Ownership of *Star Wars: Rebellion* or *Star Wars: Empire at War: Gold Pack* through GOG or Steam
- Access to the separate `rebellion2-media` repository

Access to development content does not grant redistribution rights. **NEVER share, upload, commit,
or otherwise redistribute `rebellion2-media`, `Assets/Content`, or any copyrighted game asset.**

## Setup

1. Clone `rebellion2` and `rebellion2-media` beside one another.
2. Copy `rebellion2-media/Content/` into `rebellion2/Assets/Content/`.
3. Copy `rebellion2-media/Models/MainMenu/` into
   `rebellion2/Assets/Art/Models/MainMenu/`.
4. Overlay `rebellion2-media/UnityAssets/` onto `rebellion2/Assets/`, preserving `.meta` files.
   This installs native Unity packages, including the main-menu planet, at their logical art
   paths.
5. Open `rebellion2` in Unity.
6. Run **Rebellion > Build > Build All UI**.

The copied content, models, Unity assets, all generated prefabs, and all generated scenes are local
development artifacts and are ignored by Git. This includes the boot scene and cutscene playback
assets. Manage the media checkout and refresh these local copies outside Unity.

CI installs clean media mirrors automatically with `rsync --delete` before Unity builds and tests.

## Commands

```bash
./build.sh format     # Check C# formatting
./build.sh xmlformat  # Format XML data
./build.sh lint       # Run static analysis
./build.sh test       # Run Unity EditMode tests
./build.sh coverage   # Run tests with coverage thresholds
./build.sh build      # Build the standalone player
./build.sh clean      # Remove build artifacts
./build.sh all        # Run the complete local verification suite
```

Running `./build.sh` without a command is equivalent to `./build.sh all`.

Set `UNITY` if Unity is installed somewhere other than the detected platform default. Standalone
builds use the host platform unless `BUILD_TARGET` and `BUILD_PLAYER_PATH` are set:

```bash
BUILD_TARGET=StandaloneWindows64 BUILD_PLAYER_PATH=build/rebellion2.exe ./build.sh build
```

Player builds generate the UI through `StandalonePlayerBuild.Build` and verify that development
content under `Assets/Content/` was not embedded in the player.

For native UI audits, pass `-nativeAuditBuild` to `StandalonePlayerBuild.Build` and use a
separate player output directory. This adds the `REBELLION_NATIVE_AUDIT` compilation symbol;
`Assets/Tests/NativeAudit` requires that symbol in standalone players. Launch each audit player
with its own absolute `-nativeAuditControl` directory. Without that launch argument, the observer
does not start. Ordinary builds omit the symbol.

Write one JSON command to a temporary file, then rename it to `request.json` in that directory.
Commands have a unique numeric `id` and an `operation`: `state`, `point`, `capture`, or `wait`.
For example, `{"id":1,"operation":"point","target":"MultiplayerButton"}` resolves the
center of a unique active control, its event-system raycasts, and public UI state. Exact hierarchy
paths disambiguate repeated names. Coordinates use Unity's viewport origin at the bottom left.
The completed response is published once as `response-1.json`; captures also create `frame-1.png`.
Use a fresh control directory for each process and never reuse command IDs within it.

For native service audits, explicit `browse`, `host`, `join`, `ready`, `launch`, `resume`, `save`,
`quicksave`, `load`, `speed`, and `fight` commands call the existing public service contracts.
`key` queues a virtual Input System keyboard press and release. These commands do not invoke
private callbacks. Poll `operationPending`, then check `operationSucceeded`, coordinator status,
and the resulting scene and files: completion of a service call alone does not establish admission.
`fight` submits one explicit local Fight response; it does not automate future battles.
Simulation profiling stays in the editor/headless runners. The network simulation runner records
per-day tick, step, barrier, and verification durations with `Stopwatch`. Ordinary gameplay has
no phase-trace collector or native timing commands.

Service audits do not establish physical input delivery. Drive input through the operating system
when testing actual shortcuts and controls, and verify the resulting UI, checkpoint, or save
independently. A resolved pointer target does not prove a click was delivered. State responses include active control paths,
interactivity, enabled action names, shortcut suppression, and selected-control focus; they omit
input-field contents and credentials. Scene queries run only for explicit commands, with request
polling at five hertz. Keep both audit clients on the same compiled build and use isolated saves.

## Multiplayer synchronization

`GameRuntime` selects the session owner before constructing simulation services. Multiplayer
campaigns require an `IGameSessionOwner`; presentation reports readiness for that specific session
and never replaces its execution owner. Local and network player actions use the session's writable
command boundary. Commands wait while a tick executes, and network commands also wait for the
completed day's verification barrier.

Admission compares a source identity embedded in the compiled game assembly and a SHA-256 digest
of the composed gameplay catalog. The source identity covers production scripts, assembly settings,
package dependencies and Unity version; it is independent of checkout paths and line endings.
Content checks apply to joining, content changes, shared loads and reconnects. Pack/version labels
alone do not admit a peer. After changing source inputs, allow Unity's identity regeneration and
script compilation to finish before testing or building.

Each launch, shared load and reconnect receives an initialization identity. Readiness, recovery
snapshots and gameplay packets from an outgoing initialization cannot enter its replacement.
Snapshot revisions are ordered within that initialization; a restarted host begins its own sequence.
Recovery transport initiation follows room ownership, while simulation authority retains the
campaign's authenticated host role. The shared persistence channel is created after the agreed
state handshake, rather than binding to a provisional recovery identity.
Recovery checks both payload integrity and the restored simulation continuation before opening
gameplay. Completed days compare model and runtime continuation fingerprints, including active RNG
seed/position, identifiers, faction rules and pending simulation facts. Participant UI state is
excluded. A mismatch or execution failure stops replay without retrying a potentially applied action.

Simulation verification performs one graph traversal per completed multiplayer day per peer, after
the tick iterator releases its execution boundary. The headless network runner measures its cost;
verification is not a frame-loop poll.

Scene-node lookup uses a standard dictionary rebuilt from the decoded hierarchy. Save files and
network snapshots do not preserve a second registry membership list, dictionary storage positions,
or vacant slots. Manufacturing queues are rebuilt from saved producer IDs and sequence values.

Human participants select their own game speed. The effective clock uses the slowest human
selection in the order Pause, Very Slow, Slow, Medium, Fast. Either player can pause; time advances
only when neither remains paused. A faster preference takes effect as the slower participant raises
their selection, without requiring identical choices. Keyboard stepping follows the local selection.
The strategy HUD shows the effective clock, both acknowledged choices, and faster-speed requests.

Each human side in a pending fleet encounter must respond before resolution. A single human facing
an AI side resolves after that human's response. If either side selects withdrawal, its existing
withdrawal eligibility and movement rules apply; when both select withdrawal, both plans are
prepared before either is applied and the encounter is a draw. The battle window shows response
receipt without exposing the other participant's choice. Pending responses belong to the runtime
continuation and survive recovery.

## Code organization

`Game` contains game objects, authored configuration and instructions, and campaign state. These types can own behavior, including their own calculations and invariants. They depend only on `Game`, `SceneGraph`, and `Util` within this codebase.

`Gameplay` contains the operations, queries, calculations, and reactions used to run a campaign. Both player-facing controllers and AI use these services. Supporting request and result types stay with the feature that owns them.

| Area | Ownership |
| --- | --- |
| `Game/Scripting` | Authored action, conditional, selector, binding, and trigger instructions. |
| `Game/Scripting/Events` | Event definitions, schedules, and saved activation history. |
| `Gameplay/Scripting` | Interpretation, evaluation, temporary execution contexts, and gameplay action handling. |
| `Gameplay/Scripting/Events` | Event validation, activation, and campaign tick/result integration. |
| `Gameplay` (root) | Session construction, service registration, tick scheduling, and result delivery. |

Gameplay feature folders retain commands for operations, queries for inspection and estimates, tick processors for time-driven work, and observers for result-driven reactions. A supporting class does not need its own tick to belong to a gameplay feature.

## Game scripting

`GameActionInterpreter` interprets branching and action sequences. It passes concrete actions through `IGameActionExecutor`, implemented by `GameActionCommands`, which owns movement, mission, ownership, message, and encounter dependencies.

`GameEvaluationContext` holds bindings, source attribution, the triggering result, and results produced during an activation. `GameConditionContext` supplies condition-evaluation inputs. `GameActionContext` collects action results and deferred operations. These temporary contexts stay within the scripting subsystem.

Actions retain their authored order. Capture cleanup remains immediate; queued command requests execute after the full action batch. Event scheduling, activation history, XML names, and save fields retain their existing contracts.

See the [scripting reference](Modding/Scripting/Index.md) for authored XML.
