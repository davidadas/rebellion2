# Rebellion 2

An open-source remake of the 1998 strategy game *Star Wars: Rebellion*, built with Unity.
Despite the name, Rebellion 2 is not a sequel. Think of it as a remaster of the original game,
but with the updates we have always wanted.

<img width="1421" height="794" alt="Rebellion 2 strategy view screenshot" src="https://github.com/user-attachments/assets/a440fc8c-6916-47a5-a7eb-5a8811700844" />

## Current Status

Rebellion 2 is approximately **55% complete toward a feature-complete single-player campaign**.

| Area | Estimate |
| --- | ---: |
| Foundation and Data | 60% |
| Strategy Simulation | 60% |
| Strategic AI | 50% |
| Missions | 80% |
| Events | 0% |
| Tactical Simulation | 0% |
| Tactical AI | 0% |
| Strategy Interface | 80% |
| UI Upscaling | 40% |
| Save Games* | 100% |
| Settings | 10% |
| Moddability | 65% |
| Modding Tools | 0% |
| Multiplayer | 0% |

\* Save compatibility is not guaranteed between versions during development.

Campaign generation, core strategy systems, missions, save games, and most of the Strategy
interface are functional. Major remaining work includes story events, strategic AI depth, tactical
AI, UI upscaling, settings, modding tools, tactical simulation, multiplayer, balance, and release
polish.

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

Game assets and generated UI artifacts are intentionally kept outside this source repository.
For a development checkout, clone `rebellion2-media` beside this repository and run
`./build.sh sync-media`; see the development guide for the `MEDIA_PATH` override and full setup.

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
