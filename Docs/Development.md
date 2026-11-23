# Development

## Requirements

- Unity 6000.4.12f1
- .NET SDK
- A Bash-compatible shell and `rg` (ripgrep)
- Python 3.11 or later for desktop packaging

Running the game, building it, or running the full test suite also requires:

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
./build.sh test-core  # Run Unity EditMode and PlayMode tests from the game repository alone
./build.sh test-media # Run tests that require the installed media or generated UI
./build.sh test       # Run both groups, with media installed
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
