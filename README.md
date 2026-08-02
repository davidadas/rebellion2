# Rebellion 2

An open-source remake of the 1998 strategy game *Star Wars: Rebellion*, built with Unity.

<img width="1421" height="794" alt="Rebellion 2 strategy view screenshot" src="https://github.com/user-attachments/assets/a440fc8c-6916-47a5-a7eb-5a8811700844" />

## Current Status

Rebellion 2 is approximately **55% complete toward a feature-complete single-player campaign**.
This is an engineering estimate based on implemented gameplay and remaining work, not a count of
files or issues.

| Area | Estimate | Current state |
| --- | ---: | --- |
| Foundation and data | 60% | Typed game data, campaign generation, serialization, fog of war, messages, research, and encyclopedia data are operational. Significant integration, validation, and completeness work remains. |
| Strategy simulation | 60% | Movement, fleets, blockades, production, maintenance, personnel, uprisings, bombardment, planetary assault, victory conditions, and strategic combat resolution exist but still need substantial depth, balance, and campaign validation. |
| Missions | 80% | Diplomacy, espionage, sabotage, recruitment, rescue, research, Jedi training, abduction, assassination, reconnaissance, and uprising missions are implemented. Duel resolution and additional refinement remain. |
| Events | 0% | Supporting code and data structures exist, but story-driven in-game events are not implemented yet. |
| Strategy interface | 80% | Galaxy map, HUD, advisors, messages, construction, facilities, fleets, missions, defense, encyclopedia, status, planet systems, targeting, and contextual commands are functional. Runtime-content integration and presentation polish remain in progress. |
| Computer opponent | 50% | The AI can plan fleet actions, missions, production, manufacturing, and unit transfers, but its strategy, balance, and handling of campaign situations need major work. |
| Save games* | 100% | A current game can be saved and loaded. |
| Settings | 10% | Basic audio settings exist. The settings interface and other player-facing options remain to be built. |
| Moddability | 65% | External content packs, scenarios, loose media, preload manifests, validation, and alternate content roots are supported. More game behavior still needs to become data-driven. |
| Modding tools | 0% | There are no player-facing tools for creating, validating, or packaging mods yet. |

\* Save compatibility is not guaranteed between versions during development.

The project has more than **3,700 automated tests**, but it is still an early-access build rather
than a finished replacement for the original game.

## Playing the game

Rebellion 2 is currently available through an early-access installer. Join the
[Star Wars Rebellion Discord](https://discord.com/invite/rWP4vzw8Gg) and ask **@DavidAdas** for
access.

The installer verifies ownership automatically. A copy of *Star Wars: Rebellion* from either
**GOG** or **Steam** is required.

## Documentation

- [Development setup and commands](Docs/Development.md)
- [Modding and content packs](Docs/Modding.md)

Game assets and generated UI artifacts are intentionally kept outside this source repository.

## Reporting bugs

Search [existing issues](https://github.com/davidadas/rebellion2/issues) before filing a new bug.
Include your platform, game version, reproduction steps, expected and actual behavior, and relevant
logs or screenshots. **DO NOT** attach copyrighted game assets or secrets.

## Contributing

Focused fixes and improvements are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) before opening a
pull request.

## Legal

This unofficial fan project is not affiliated with or endorsed by Disney, Lucasfilm, or the owners
of *Star Wars*. Copyrighted game assets are not distributed in this repository.
