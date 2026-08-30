# Modding

Rebellion 2 loads its art, audio, video, configuration, and game data from an external `Content`
directory. Most content can therefore be changed without rebuilding the game.

## Current support

The game currently loads complete content packs. A complete pack supplies every catalog, faction,
scenario, theme, and media file referenced by its `pack.xml` and faction definitions.

The loader does not currently support derived packs, sparse overlays, or fallback to another pack.
It also does not scan a separate `Mods` directory. Packs must be located beneath `Content/Packs/`
within the active content root.

These limitations matter when modifying the installed classic pack: a private copy is useful for
development, but it is not automatically a redistributable mod.

## Redistribution

**NEVER redistribute the installed `Content` directory, the original content pack, or copyrighted
assets taken from them.** Every player and mod developer must own a copy of the original game and
obtain the base content through the ownership-verifying installer.

A complete pack derived from `ClassicGalacticCivilWar` still contains the original pack's files,
even when only a few were changed. Do not distribute that complete derived pack. A complete pack
may be distributed only when the author has the right to distribute every file it contains.

Until sparse derived packs are supported, a classic-based project must distribute only the
author's changes and require each player to apply them to a private workspace created from their
own installed content. Do not distribute the resulting patched content tree.

## Create a private development workspace

Do not edit the installed `Content` directory in place. Installer updates and repairs own that
directory and may replace it.

For local development, privately copy the complete installed `Content` directory to a workspace
outside the game installation. Then copy the base pack inside that workspace:

```text
MyModWorkspace/
  Content/
    catalog.xml
    Application/
    Packs/
      ClassicGalacticCivilWar/
      MyPack/
```

The private `MyPack/` copy may contain the owned base content required for development, but it must
not be distributed. In `MyPack/pack.xml`, give the pack a unique `ID`, `DisplayName`, and `Version`.
The directory name is only organizational; the `ID` declared by `pack.xml` is the identity used by
the game and save files.

Launch the game against the workspace instead of the installed content:

```bash
"/path/to/Rebellion 2" -contentPath "/path/to/MyModWorkspace/Content"
```

## Select a pack and scenario

`Content/catalog.xml` declares the default pack and scenario for a content root:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<ContentCatalog>
  <ActivePackID>my-content-pack</ActivePackID>
  <ActiveScenarioID>standard</ActiveScenarioID>
</ContentCatalog>
```

The catalog selection is the fallback. At startup, a non-empty selection in
`user-settings.json` overrides it:

```json
"Content": {
  "ActivePackID": "my-content-pack",
  "ActiveScenarioID": "standard"
}
```

`ActivePackID` must match a `pack.xml` ID beneath the active content root's `Packs/` directory.
`ActiveScenarioID` must match a scenario declared by that pack. If the scenario ID is empty, the
pack's `DefaultScenarioID` is used. If a saved pack override cannot be opened, the game falls back
to the catalog selection.

There is no in-game content-pack selector yet. Edit `user-settings.json` only while the game is
closed. For development, the simpler option is to leave the saved content selection empty and make
the work-in-progress pack the default in the workspace's `catalog.xml`. Loading errors then remain
visible instead of being hidden by fallback to a different catalog pack.

## Install a complete third-party pack

The game can load a complete third-party pack placed beneath the installed `Content/Packs/`
directory and selected through user settings. This is not a patch-safe mod installation location:
an installer repair may remove the pack. Keep the distributable source outside the installation so
it can be installed again.

Do not distribute a complete pack derived from the copyrighted classic pack. This installation
workflow is suitable only for packs whose contents may all be redistributed.

## Pack structure

```text
Content/
  catalog.xml
  Application/                 Shared application UI, audio, video, and preload manifests
    Rules/                     Application-level game-config defaults
  Packs/
    MyPack/
      pack.xml                 Pack identity and definition paths
      Rules/                   Optional game-config overrides
      Shared/                  Shared data and presentation
      Factions/                Faction data, units, UI themes, and media
      Scenarios/               Scenario definitions and generation settings
      Preload/                 Assets loaded before each major screen
```

Paths in `pack.xml`, faction definitions, and scenario definitions are relative to the pack root.
Content addresses beginning with `Application/` resolve from the shared application directory;
addresses beginning with `Pack/` resolve from the active pack.

The following sections describe authoring a complete pack. When working from the classic pack,
remember that the complete result is a private development artifact rather than a distributable
mod.

## Common changes

### Simulation rules

Runtime simulation constants are layered. `Content/Application/Rules/game.xml` ships the complete
application-level defaults and is owned by the game: it changes with engine updates and must not be
copied into or edited by a distributed pack. A pack that tunes simulation constants declares a
`GameConfigPath` in `pack.xml` pointing at a sparse override merged over those defaults at load:

- A leaf value in the pack file replaces the default value.
- A section merges recursively, so unlisted siblings keep their defaults.
- A lookup table (repeated `Entry` elements) is replaced wholesale, never merged entry-by-entry.

State only the values your pack changes, and omit `GameConfigPath` entirely when the pack does not
change any. The merged configuration is validated against
`Content/Application/Schemas/game-config.xsd` when the pack loads, and an invalid or unknown value
is reported by name.

### Campaign setup

Copy or edit a scenario under `Scenarios/`. Its `scenario.xml` controls the scenario ID, display
name, playable factions, default player faction, and generation-config path. The referenced
`generation.xml` controls starting planets, headquarters, garrisons, difficulty profiles, and
starting officer counts. Generation configuration must conform to
`Content/Application/Schemas/generation-config.xsd`; invalid scenario rules are rejected when the
content pack loads.

Add a new scenario's `scenario.xml` path to `ScenarioPaths` in `pack.xml`, then select its ID through
the workspace catalog or user settings.

### Units, officers, planets, and buildings

The XML files referenced by `pack.xml` and each faction's `faction.xml` define the game's entities.
Faction files point to capital ships, starfighters, regiments, special forces, officers, faction
data, themes, and encyclopedia entries. Pack-level definitions include planet sectors, buildings,
events, and messages.

`TypeID` and `InstanceID` values must be present and unique within their respective scopes.
References between files use the appropriate identifier, so renaming one requires updating every
reference to it. Event and message catalogs are separate files selected by `GameEventsPath` and
`MessageDefinitionsPath` in `pack.xml`.

### UI appearance

Faction presentation is controlled by its `ThemePath`. Theme XML files contain addressed textures
for Strategy UI windows, controls, advisors, messages, and faction-specific presentation. Replace
an existing file while preserving its address, or copy the theme and update the faction's
`ThemePath`.

PNG, JPG, and JPEG images are loaded directly from the content directory. Unity `.meta` files are
not part of the content format and should not be distributed with a pack.

### Audio and video

Audio and video are loose files referenced by extensionless content addresses. Preserve the address
expected by the XML or update the corresponding definition. Application-level main-menu and
cutscene media lives under `Application/`; faction- and scenario-specific media belongs in the
pack.

### Game events

Game events define scheduled and result-triggered narrative behavior without a code rebuild. See
[Creating game events](Events/Index.md) for lifecycle, scheduling, targeting, conditions, actions,
schema validation, and complete examples.

### Preloaded assets

Preload manifests list textures, texture directories, and audio needed before a major screen is
shown. When adding required UI art or audio, update the corresponding manifest under
`Application/Preload/` or the pack's `Preload/` directory. Missing required preload content causes
the game to fail loudly instead of silently displaying an incomplete interface.

## Compatibility

Save files record the pack ID, pack version, and scenario ID. A save can be loaded only while the
matching pack version and scenario are active. Increment the pack version when publishing changes
and test both new-game creation and save loading after modifying definitions or generation rules.
