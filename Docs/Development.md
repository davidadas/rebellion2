# Development

## Requirements

- Unity 6000.4.0f1
- .NET SDK
- A Bash-compatible shell
- A copy of *Star Wars: Rebellion* owned through GOG or Steam
- Access to the separate `rebellion2-media` repository

Access to development content does not grant redistribution rights. **NEVER share, upload, commit,
or otherwise redistribute `rebellion2-media`, `Assets/Content`, or any copyrighted game asset.**

## Setup

1. Clone `rebellion2` and `rebellion2-media` beside one another.
2. Copy `rebellion2-media/Content/` into `rebellion2/Assets/Content/`.
3. Copy `rebellion2-media/Models/MainMenu/` into
   `rebellion2/Assets/Art/Models/MainMenu/`.
4. Open `rebellion2` in Unity.
5. Run **Rebellion > Build > Build All UI**.

The copied content and models, all generated prefabs, and all generated scenes are local development
artifacts and are ignored by Git. This includes the boot scene and cutscene playback assets. Manage
the media checkout and refresh these local copies outside Unity.

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
