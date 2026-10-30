# Development

## Requirements

- Unity 6000.4.12f1
- .NET SDK
- A Bash-compatible shell
- Python 3.11 or later for desktop packaging
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
./build.sh build      # Build and verify the game and content-pack editor
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

## Content-pack editor and packaging

Windows and Linux builds include `rebellion2-editor` beside `rebellion2`. The macOS editor is a
helper app inside the game app's `Contents/Helpers` folder, so game installers and updates carry it
with the game. Both apps use the same external Content directory. Launch the editor to create or open
a pack in the installation's `Mods` folder. A new pack inherits its selected base; saving writes
only edited XML files and imported assets. Export creates a zip of those local files after
checking every declared scenario through the content loader, reference checks, campaign generation,
runtime initialization, and referenced media decoding. Progress shows the current check; Escape
cancels validation or export without replacing an existing archive.

Package a completed Windows build with:

```bash
python3 Tools/package_desktop.py --platform StandaloneWindows64 \
  --build-dir build/content-editor --output dist/rebellion2-windows.zip
```

Use `StandaloneLinux64` or `StandaloneOSX` and an output ending in `.tar.gz` for Linux or macOS.
Without `--output`, the command only verifies the build. The shared desktop layout requires Mono
players. Use `--game-name Rebellion2` for release builds that use that executable base name.
Set `PYTHON` when `build.sh` should use a
different Python executable.

`--stage-dir <new-folder>` creates the same clean runtime file tree for installer packaging without
making an archive. Release installers use this stage to exclude Unity debug output and local files.

Archives contain both players, their runtimes, native file dialogs, dependency notices, and a
`bundle.json` inventory with file hashes. A `.sha256` file accompanies each archive. Installed
content, local mods, and debug output are excluded. The installer must supply the existing external
`Content` folder beside the players; this archive does not replace the content distribution.

CI runs packaging tests during lint, waits for the game tests before building, then verifies and
uploads a desktop bundle for each build target. These are downloadable CI artifacts retained for
seven days; creating them does not publish a release or update an installation.
## Code organization

`Game` contains game objects, authored configuration and instructions, and campaign state. These types can own behavior, including their own calculations and invariants. They depend only on `Game`, `SceneGraph`, and `Util` within this codebase.

`Gameplay` contains the operations, queries, calculations, and reactions used to run a campaign. Both player-facing controllers and AI use these services. Supporting request and result types stay with the feature that owns them.

| Area | Ownership |
| --- | --- |
| `Game/Scripting` | Authored action, conditional, selector, binding, and trigger instructions. |
| `Game/GameEvents` | Event definitions, schedules, and saved activation history. |
| `Gameplay/Scripting` | Interpretation, evaluation, temporary execution contexts, and gameplay action handling. |
| `Gameplay/GameEvents` | Event validation, activation, and campaign tick/result integration. |
| `Gameplay` (root) | Session construction, service registration, tick scheduling, and result delivery. |

Gameplay feature folders retain commands for operations, queries for inspection and estimates, tick processors for time-driven work, and observers for result-driven reactions. A supporting class does not need its own tick to belong to a gameplay feature.

`ContentEditor` is a separate assembly included in Unity and the standalone authoring player. Its
workspace controller handles editing requests, its session owns selection and continuous edits, and
its projector supplies presentation data. Projects and documents own editable state; storage owns
file access, while validation and export have their own owners. General input, tooltip, dialog,
preview, scrolling, and divider behavior lives in `UI/Components` and has no editor dependency.
Shared menu builders support textured and flat control styles.

The map's **Fit to view** command changes zoom and pan only. **Auto arrange** adjusts saved
coordinates to clear overlaps and the window frame, moving planets in a sector or whole sector
clusters on the galaxy map. Layout runs off the UI thread, supports Escape to cancel, and applies
as one undoable edit after the complete placement is valid.

## Game scripting

`GameActionInterpreter` interprets branching and action sequences. It passes concrete actions through `IGameActionExecutor`, implemented by `GameActionCommands`, which owns movement, mission, ownership, message, and encounter dependencies.

`GameEvaluationContext` holds bindings, source attribution, the triggering result, and results produced during an activation. `GameConditionContext` supplies condition-evaluation inputs. `GameActionContext` collects action results and deferred operations. These temporary contexts stay within the scripting subsystem.

Actions retain their authored order. Capture cleanup remains immediate; queued command requests execute after the full action batch. Event scheduling, activation history, XML names, and save fields retain their existing contracts.

See the [scripting reference](Modding/Scripting/Index.md) for authored XML.
