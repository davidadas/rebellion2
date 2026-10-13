# Development

## Requirements

- Unity 6000.4.0f1
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

## Tactical scene preview

Rebuild the UI after changing tactical prefab builders, then open the generated `TacticalView`
scene. Its standalone preview deploys the default fleets and plays their hyperspace arrivals.
Copy the matching media branch's `Content/` changes into `Assets/Content/` first. The pack's
`BattleArrivalPath` points to `Shared/Tactical/arrival.xml`, which defines the common arrival
colors and `Pack/Shared/Tactical/Audio/hyperspace-arrival.wav` sound independently of faction themes.

Select friendly units and right-click the battlefield to move, or press **Move** and left-click
the destination. **Stop** clears the selected units' pending move orders. Right-drag still orbits
the camera. Regular and Shift camera panning use the reduced speed.

Units move forward through curved turns at their configured sublight speed. Fighters turn at
ten degrees per speed point per second; capital ships turn at half that rate. Destinations
preserve the selected formation's offsets and keep its unit centers inside the map. Curved
travel does not yet avoid collisions or enforce map boundaries along the route. Engine-component
damage does not yet change the configured speed. Pending destinations are saved with battle state;
loading an existing battle preserves its poses without replaying the arrival presentation.
