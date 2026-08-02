# Development

## Requirements

- Unity 6000.4.0f1
- .NET SDK
- A Bash-compatible shell
- Access to the separate `rebellion2-media` repository

## Setup

1. Clone `rebellion2` and `rebellion2-media` beside one another.
2. Copy `rebellion2-media/Content/` to `rebellion2/Assets/Content/`.
3. Copy `rebellion2-media/Models/MainMenu/` to
   `rebellion2/Assets/Art/Models/MainMenu/`.
4. Open `rebellion2` in Unity and allow the assets to import.
5. Run **Rebellion > UI > Build All**.

The copied content, generated UI prefabs, and generated scenes are local development artifacts and
are ignored by Git. Repeat the copies when `rebellion2-media` changes and rebuild the UI after
changing builder code or content used by a builder.

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
