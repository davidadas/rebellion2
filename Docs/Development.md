# Development

## Requirements

- Unity 6000.4.12f1
- .NET SDK
- A Bash-compatible shell and `rg` (ripgrep)
- Python 3.11 or later for desktop packaging
- Ownership of *Star Wars: Rebellion* or *Star Wars: Empire at War: Gold Pack* through GOG or Steam
- Access to the separate `rebellion2-media` repository

Access to development content does not grant redistribution rights. Do not share, upload, commit,
or otherwise redistribute `rebellion2-media`, `Assets/Content`, or copyrighted game assets.

## Setup

1. Clone `rebellion2` and `rebellion2-media` beside one another.
2. Copy `rebellion2-media/Content/` into `rebellion2/Assets/Content/`.
3. Copy `rebellion2-media/Models/MainMenu/` into
   `rebellion2/Assets/Art/Models/MainMenu/`.
4. Overlay `rebellion2-media/UnityAssets/` onto `rebellion2/Assets/`, preserving `.meta` files.
   This installs native Unity packages, including the main-menu planet, at their logical art
   paths.
5. Build the host vector library using the instructions in [Native vector math](NativeMath.md).
6. Open `rebellion2` in Unity.
7. Run **Rebellion > Build > Build All UI**.

Copied media, generated prefabs, and generated scenes are local development artifacts ignored by
Git. Refresh copied media with Unity closed. The revision in `.github/media-revision` pins the
content expected by this branch.

## Commands

Run these from the repository root:

```bash
./build.sh format     # Check C# formatting
./build.sh lint       # Run static analysis
./build.sh test       # Run Unity EditMode and PlayMode tests
./build.sh coverage   # Run tests with coverage thresholds
./build.sh build      # Build and verify the game and content-pack editor
./build.sh clean      # Remove build artifacts
./build.sh all        # Run formatting, lint, and tests with coverage
```

Running `./build.sh` without a command is equivalent to `./build.sh all`.

Set `UNITY` if Unity is installed somewhere other than the detected platform default. Set `PYTHON`
to use a different Python executable. Standalone builds use the host platform unless `BUILD_TARGET`
and `BUILD_PLAYER_PATH` are set:

```bash
BUILD_TARGET=StandaloneWindows64 BUILD_PLAYER_PATH=build/rebellion2.exe ./build.sh build
```

## Tactical space combat

The battle alert opens manual combat when the selected content pack supplies tactical definitions
for both factions and every participating unit. The campaign stays suspended while the encounter
runs. Closing a completed encounter applies its losses, component damage, fighter recovery, and
withdrawals through the shared combat-result pipeline. Surviving fighters without a carrier may
remain at the encounter planet without changing its surface ownership or colonization.

In multiplayer campaigns, both commanders respond to the alert before combat starts. If either
takes command, the encounter opens for both players; a side that selected automatic control starts
with its existing battle automation enabled. Withdrawal responses retain the shared campaign
withdrawal path, and two automatic responses use the existing automatic resolution path.

The tactical frame uses a fixed 640x480 layout. Left-click to replace the current selection; hold
Ctrl to extend or toggle it. Right-click addresses an object or visible navigation marker within
the current command context. Use the category buttons or number keys 1-8 for object groups and
Space for the nearest opposing object. The Pause button pauses and resumes the battle. Tab advances
inspection once per press; Shift+Tab inspects the previous object. Orders and Tactics open with the
addressed group's current settings. Confirming an unchanged panel preserves its order and engagement
mode.

The camera buttons and arrow keys rotate or tilt the view. Page Up and Page Down zoom; Home saves
the primary camera and End restores it. Return focuses the current selection. F1-F12 open category
camera views. Opening Options isolates battle input and returns to a paused encounter.

`TacticalCombatSession` owns battle state and queued commands independently of Unity objects.
`TacticalCombatFactory` captures campaign participants and the pack's shared definitions at entry.
`TacticalCombatController` translates screen input, and `TacticalCombatRenderer` owns instantiated
models, effects, and render targets. Required media finishes loading before the session advances.
The shared content caches retain source assets; disposing the encounter releases its presentation
instances and audio handles.

`MultiplayerTacticalDriver` gives the host sole ownership of live combat progression. Each player
loads their own faction HUD, camera, models, and audio before the ready barrier admits battle time.
Authenticated orders address that player's side. Replaceable compressed snapshots update the passive
view independently of reliable commands and presentation events. The host clock continues without
waiting for snapshot receipts; the guest displays timestamped poses and effects on one delayed timeline.
Shared movie acknowledgements hold the battle until both presentations finish. A terminal checkpoint
and effect watermark precede the host's ordered campaign-result commit, preserving one result and one
tick resumption. See [Multiplayer combat replication](Multiplayer.md) for delivery and recovery contracts.
Disconnects release the battle view and retain the existing shared recovery boundary rather than
importing an unfinished encounter. Campaign orders and persistence remain blocked during combat;
peer chat continues through its existing ordered request path.

Operational weapon batteries enter combat charged. A unit shares its recharge budget across
depleted batteries in queue order. Ion hits beyond shields accumulate the selected attack and
movement delays and discharge the selected batteries. Damage control includes weapon damage
already present when the encounter starts.

In single-player, Get immediate result ends interactive combat and imports its losses before the
existing automatic encounter resolver settles the surviving forces. The campaign receives one
combined result and resumes after all pending encounters have been resolved. Immediate results are
unavailable during multiplayer battles.

Battle-station fighter attacks announce approach and attack entry. Alliance calls also reflect
fighter category, outcome, and timed progress; Empire views use the approach and start cues.

A pack selects its definition XML with `TacticalCombatDefinitionsPath` in `pack.xml`. It contains
faction deployment, unit geometry and model detail paths, fighter images, environments, sound
variants, impact sequences, looping tractor/gravity indicators, retreat settings, and component-repair
intervals. Paths resolve through the pack's existing file resolver. Keep these assets in the private
media checkout and copy them into the ignored development content directory before rebuilding the
UI or running tests.

Keep tactical models, fighter sprites, and palette textures beside the unit's existing artwork.
Shared planet backgrounds belong in `Shared/Planets`; HUD images use the existing shared and
faction `Strategy/UI/Combat` folders. World effects and the starfield use `Shared/Strategy/Combat`,
shared effects audio uses `Shared/Strategy/Audio/Combat`, and faction voice lines use the existing
`Strategy/Advisor/Audio/Notifications` folders. Asset addresses in XML omit file extensions.
Filenames describe their purpose; native message IDs and GLB texture identifiers remain separate
authored metadata. See the media repository's README for the complete layout.

Capital-ship component damage and its campaign repair timer are saved per ship. Shared tactical
geometry and media references remain pack data. Older saves load with an undamaged component record.

## Windows on Arm

Windows x64 and ARM64 players share Unity's `StandaloneWindows64` build target and differ only in
CPU architecture. `./build.sh build` builds for the host CPU, so a Windows on Arm machine produces
a native ARM64 player by default. Set `BUILD_ARCHITECTURE` to choose explicitly:

```bash
BUILD_ARCHITECTURE=arm64 ./build.sh build   # native Windows on Arm player
BUILD_ARCHITECTURE=x64 ./build.sh build     # Intel/AMD player
```

The **Rebellion > Build** menu items build for the architecture of the running editor. Scripted
builds that call `StandalonePlayerBuild.Build` directly can pass `-buildArchitecture arm64` or
`-buildArchitecture x64`; `Tools/package_desktop.py` takes the matching `--architecture` value.

ARM64 builds add the `EOS_PLATFORM_WINDOWS_ARM64` scripting define so the Epic Online Services
plugin binds its ARM64 library; the plugin only injects that symbol for builds started from Unity's
Build window.

Notes for a Windows on Arm development machine:

- Install the Windows Arm64 build of the pinned Unity Editor; Unity Hub selects it on Arm hosts.
  The Mono scripting backend used by this project needs no Visual Studio components.
- Unity does not support Vulkan on Windows on Arm, so players use Direct3D 12 or Direct3D 11.
  Qualcomm Adreno drivers have had Direct3D 12 crashes that can restart the whole machine (Unity
  issue UUM-131104, resolved by a GPU driver update). Start the editor and a new player with
  `-force-d3d11` first, and update the GPU driver before relying on Direct3D 12.
- Run `build.sh` from Git Bash, not the WSL `bash.exe` that Windows puts first on `PATH`.
- Set `PYTHON=python` when `python3` resolves to the Microsoft Store alias.
- `Assets/Plugins/NativeFileDialog/win-arm64/nfd.dll` is the native dialog library for the ARM64
  content-pack editor and Unity Editor. `NOTICE.txt` beside it records how it was built.
