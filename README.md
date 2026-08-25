# Rebellion 2

An open-source remake of the 1998 strategy game *Star Wars: Rebellion*, built with Unity.
Despite the name, Rebellion 2 is not a sequel. Think of it as a remaster of the original game,
but with the updates we have always wanted.

<img width="1421" height="794" alt="Rebellion 2 strategy view screenshot" src="https://github.com/user-attachments/assets/a440fc8c-6916-47a5-a7eb-5a8811700844" />

## Current Status

Rebellion 2 is approximately **60% complete toward a feature-complete single-player campaign**.

| Area | Estimate |
| --- | ---: |
| Foundation and Data | 60% |
| Strategy Simulation | 60% |
| Strategic AI | 50% |
| Missions | 80% |
| Original Game Events | 85% |
| Custom Events API | 30% |
| Tactical Simulation | 0% |
| Tactical AI | 0% |
| Strategy Interface | 80% |
| UI Upscaling | 40% |
| Save Games | 100% |
| Settings | 10% |
| Moddability | 65% |
| Modding Tools | 0% |
| Multiplayer | 0% |

**NOTE: Save compatibility is not guaranteed between versions during development.**

## Playing the game

Rebellion 2 is currently available through an early-access installer. Join the
[Star Wars Rebellion Discord](https://discord.com/invite/rWP4vzw8Gg) and ask **@DavidAdas** for
access.

The installer verifies ownership automatically. A copy of *Star Wars: Rebellion* from either
**GOG** or **Steam** is required.

**NOTE: Installed game data and copyrighted assets must NEVER be redistributed, uploaded, or
shared under any circumstances.**

## Documentation

- [Development setup and commands](Docs/Development.md)
- [Modding and content packs](Docs/Modding.md)
- [Creating game events](Docs/Events/Index.md)

Game assets and generated UI artifacts are intentionally kept outside this source repository.
For a development checkout, obtain the separate `rebellion2-media` repository and populate the
ignored development asset directories described in the development guide. CI installs its media
checkout automatically.

## Reporting bugs

Search [existing issues](https://github.com/davidadas/rebellion2/issues) before filing a new bug.
Include your platform, game version, reproduction steps, expected and actual behavior, and relevant
logs or screenshots. **DO NOT** attach copyrighted game assets or secrets.

## Contributing

Focused fixes and improvements are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) before opening a
pull request.

## Legal

This unofficial fan project is not affiliated with or endorsed by Disney, Lucasfilm, or the owners
of *Star Wars*. Copyrighted game assets are not distributed in this repository and must not be
redistributed by players, modders, or contributors.
