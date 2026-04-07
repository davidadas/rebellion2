# Rebellion 2

A remake of the 1998 Star Wars Rebellion PC game by Coolhand Interactive.

## Setup

### Prerequisites
- [Unity 6000.4.0f1](https://unity.com/releases/editor/whats-new/6000.4.0) via Unity Hub
- [.NET Framework 4.7.1 Developer Pack](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net471) (required for building/testing outside Unity)

### Steps
1. Clone this repository to a local directory.
2. Install Unity 6000.4.0f1 via Unity Hub if you haven't already.
3. In Unity Hub, select **Projects** from the left-hand menu, click **Open**, and select the cloned `rebellion2` folder.
4. Once the project opens, import TextMesh Pro assets: **Window > TextMeshPro > Import TMP Essential Resources**.
5. Hit the play button.

### Game Assets

The game's art, audio, and video assets are **not included** in this repository. To obtain them, you must prove ownership of the original *Star Wars: Rebellion* game.

1. Join the [Star Wars Rebellion Discord](https://discord.com/invite/rWP4vzw8Gg).
2. Follow the instructions in the server to verify ownership of the original game.
3. Once verified, you will be granted access to the asset pack.
4. Place the downloaded assets into the following directories:
   - `Assets/Resources/Art/`
   - `Assets/Resources/Audio/`
   - `Assets/Resources/Videos/`

> **Note:** The game will not run without these assets.

### Building, Testing & Linting
All commands are available via `build.sh`:

```bash
./build.sh format     # Check C# formatting with CSharpier
./build.sh xmlformat  # Format XML data files in-place with xmllint
./build.sh lint       # Run Roslynator static analysis
./build.sh test       # Run EditMode tests via Unity
./build.sh build      # Build standalone player
./build.sh clean      # Remove build artifacts
./build.sh all        # Run format + lint + test
```

The Unity editor path defaults to `C:/Program Files/Unity/Hub/Editor/6000.4.0f1/Editor/Unity.exe`. Override it with the `UNITY` environment variable if your installation differs.

> **Note (Windows):** `xmllint` is not available on Windows by default. Install it via Chocolatey: `choco install xsltproc`

> **Note:** If `dotnet build` fails with a missing .NET Framework 4.7.1 reference assemblies error, ensure the Developer Pack is installed and update `FrameworkPathOverride` in `Directory.Build.props` to match your local installation path.

## Progress
[Placeholder]

## FAQ

### 1. How do I contribute?

Currently, my biggest **need is for an experienced gameplay developer**. If you are not interested in contributing directly, I would be happy just to use you as a mentor. I have been an engineer for well over a decade, but haven't worked on games since college.

That said, anyone is welcome to share their skills or ideas. Just shoot me an email at adasgames0@gmail.com. 

### 2. Where can I track project status?

I do not currently have a Trello board or similar project tracking mechanism. If enough people become interested in the probject, however, I will happily set one up.

### 3. When will this be complete?

This is unfortunately not my full time job. Unless I am joined by additional contributors, the ebbs and flows of commercial software development are likely to push this project into multiple hiatuses. It honestly may never be completed. I mostly just do this for fun.

### 4. Will Disney permit this?

I have absolutely *zero* desire to step on Disney's toes here. My ultimate hope is that I can produce enough to entice Disney into bringing this project in-house (even if that means closing the game's source code, which they would almost certainly require). Should they ask me to de-Rebellion the game, however, I will do so without hesitation and build something entirely different out of the existing code I have.
