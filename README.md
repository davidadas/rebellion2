# Rebellion 2

A work-in-progress remake of the 1998 *Star Wars: Rebellion* PC strategy game.

<img width="1421" height="794" alt="Rebellion 2 strategy view screenshot" src="https://github.com/user-attachments/assets/a440fc8c-6916-47a5-a7eb-5a8811700844" />

## Project Status

Rebellion 2 is under active development. Core strategy systems, data loading, missions, manufacturing, movement, and the Unity strategy UI are being rebuilt and validated incrementally.

This is a development build, not a public playable release. The project can be opened and exercised in Unity with the required assets, but it is not currently a complete campaign experience or a stable replacement for the original game.

## Setup

### Prerequisites
- [Unity 6000.4.0f1](https://unity.com/releases/editor/whats-new/6000.4.0) via Unity Hub
- [.NET SDK](https://dotnet.microsoft.com/en-us/download) for local formatting and analyzer tools
- Bash-compatible shell for `build.sh`

### Steps
1. Clone this repository to a local directory.
2. Install Unity 6000.4.0f1 via Unity Hub.
3. In Unity Hub, select **Projects** from the left-hand menu, click **Open**, and select the cloned `rebellion2` folder.
4. Once the project opens, import TextMesh Pro assets if Unity prompts for them, or use **Window > TextMeshPro > Import TMP Essential Resources**.
5. After installing the game assets below and allowing Unity to import them, select
   **Rebellion > UI > Build All**. Generated UI prefab payloads and the Main Menu, Save Menu, and
   Strategy scenes are intentionally absent from a fresh clone and ignored by Git; run this command
   whenever you first set up the project or change UI builder code.
6. Hit the play button.

### Game Assets

The game's art, audio, and video assets are not included in this repository. To run the project with the original assets, you must provide those assets separately from a legally owned copy of the original game.

1. Join the [Star Wars Rebellion Discord](https://discord.com/invite/rWP4vzw8Gg).
2. Follow the instructions in the server to verify ownership of the original game.
3. Once verified, you will be granted access to the asset pack.
4. Copy the media repository's `Content/` directory to `Assets/Content/`.
5. Copy `Models/MainMenu/` to `Assets/Art/Models/MainMenu/`.

Copy the complete directories and preserve their relative paths. Image `.meta` files are not
part of the media contract. The Unity prefab builders generate ignored local prefabs with preview
assets for editor use. Player builds regenerate clean prefabs and load the same raw files from the
external `Content/` directory.

> **Note:** The game will not run without these assets.

### Packaged Content and Mods

Standalone builds intentionally do not contain or copy the private game assets. An authorized installer must place a raw `Content` directory beside the game executable or `.app` bundle before the game can run. Art, audio, video, configuration, and game-data files are loaded from that directory at runtime rather than from a Unity asset bundle.

To modify a packaged build, replace files under `Content/` while preserving their relative paths and file names, then restart the game. Numbered main-menu animation frames are loaded from their existing file-name sequence while retaining the timings authored by the game.

An alternate content directory can be selected when launching the game:

```bash
"/path/to/Rebellion 2" -contentPath "/path/to/custom/Content"
```

On macOS, pass the same option to the executable inside the application bundle or use a launcher that forwards command-line arguments.

## Building, Testing, and Linting

All commands are available via `build.sh`:

```bash
./build.sh format     # Check C# formatting with CSharpier
./build.sh xmlformat  # Format XML data files in-place with xmllint
./build.sh lint       # Run Roslynator static analysis
./build.sh test       # Run EditMode tests via Unity
./build.sh coverage   # Run EditMode tests with coverage thresholds
./build.sh build      # Build standalone player
./build.sh clean      # Remove build artifacts
./build.sh all        # Run format + lint + coverage
```

Running `./build.sh` with no command is equivalent to `./build.sh all`.

The Unity editor path is detected per platform. Override it with the `UNITY` environment variable if your installation differs.

Standalone builds default to the host platform. Override with `BUILD_TARGET` or `BUILD_PLAYER_PATH` when needed:

```bash
BUILD_TARGET=StandaloneWindows64 BUILD_PLAYER_PATH=build/rebellion2.exe ./build.sh build
```

> **Note:** `xmllint` is not available on Windows by default. Install it via Chocolatey with `choco install xsltproc` if you need XML formatting on Windows.

## Contributing

Contributions are welcome. The most useful contributions are focused fixes or improvements with clear reproduction steps, tests where appropriate, and behavior that matches the original game where source parity is the goal.

Before opening a larger pull request, start a discussion or issue so the design can be aligned first.

## Legal

This is an unofficial fan project. It is not affiliated with, endorsed by, or sponsored by Disney, Lucasfilm, or the owners of *Star Wars*.

No original game assets are distributed in this repository. Users are responsible for supplying any required assets from their own legally obtained copy of the original game.
