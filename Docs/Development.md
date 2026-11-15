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
5. Open `rebellion2` in Unity.
6. Run **Rebellion > Build > Build All UI**.

Copied media, generated prefabs, and generated scenes are local development artifacts ignored by
Git. Refresh copied media with Unity closed.

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
