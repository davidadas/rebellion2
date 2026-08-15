# Modding

Rebellion 2 loads its art, audio, video, configuration, and game data from an external `Content`
directory. Most content can therefore be changed without rebuilding the game.

## Redistribution

**NEVER redistribute the installed `Content` directory, the original content pack, or copyrighted
assets taken from them.** Every player and mod developer must own a copy of the original game and
obtain the base content through the ownership-verifying installer.

A distributed mod may contain original work, patch files, or instructions that modify a user's own
installed copy. It must not include the original pack or unchanged copyrighted assets. When in
doubt, distribute the changes required to reproduce the mod rather than a copied content tree.

## Protect the installed pack

Do not edit the installed pack in place. Copy the existing pack before making a mod because future
installer updates may replace files in the original directory and overwrite those edits.

Start by copying:

```text
Content/Packs/ClassicGalacticCivilWar/
```

to a new directory beneath `Content/Packs/`. In the copied `pack.xml`, give the pack a unique `ID`,
`DisplayName`, and `Version`. The directory name is for organization; the `ID` declared by
`pack.xml` is the identity used by the game and save files.

## Select a pack and scenario

`Content/catalog.xml` selects the active pack and scenario:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<ContentCatalog>
  <ActivePackID>my-content-pack</ActivePackID>
  <ActiveScenarioID>standard</ActiveScenarioID>
</ContentCatalog>
```

`ActivePackID` must match the copied pack's `pack.xml` ID. `ActiveScenarioID` must match a scenario
declared by that pack. If `ActiveScenarioID` is empty, the pack's `DefaultScenarioID` is used.

You can keep an entirely separate `Content` directory and launch the game with it:

```bash
"/path/to/Rebellion 2" -contentPath "/path/to/Content"
```

## Pack structure

```text
Content/
  catalog.xml
  Application/                 Shared application UI, audio, video, and preload manifests
  Packs/
    MyPack/
      pack.xml                 Pack identity and definition paths
      Rules/                   Campaign rules
      Shared/                  Shared data and presentation
      Factions/                Faction data, units, UI themes, and media
      Scenarios/               Scenario definitions and generation settings
      Preload/                 Assets loaded before each major screen
```

Paths in `pack.xml`, faction definitions, and scenario definitions are relative to the pack root.
Content addresses beginning with `Application/` resolve from the shared application directory;
addresses beginning with `Pack/` resolve from the active pack.

## Common changes

### Campaign setup

Copy or edit a scenario under `Scenarios/`. Its `scenario.xml` controls the scenario ID, display
name, playable factions, default player faction, and generation-config path. The referenced
`generation.xml` controls starting planets, headquarters, garrisons, difficulty profiles, and
initial officer counts.

Add a new scenario's `scenario.xml` path to `ScenarioPaths` in `pack.xml`, then select its ID in
`catalog.xml`.

### Units, officers, planets, and buildings

The XML files referenced by `pack.xml` and each faction's `faction.xml` define the game's entities.
Faction files point to capital ships, starfighters, regiments, special forces, officers, faction
data, themes, and encyclopedia entries. Shared definitions include planet systems, buildings,
events, and messages.

IDs must be present and unique within their definition type. References between files use those
IDs, so renaming one requires updating every reference to it. Event and message catalogs are
separate files selected by `GameEventsPath` and `MessageDefinitionsPath` in `pack.xml`.

### UI appearance

Faction presentation is controlled by its `ThemePath`. Theme XML files contain addressed textures
for Strategy UI windows, controls, advisors, messages, and faction-specific presentation. Replace
an existing file while preserving its address, or copy the theme and update the faction's
`ThemePath`.

PNG, JPG, and JPEG images are loaded directly from the content directory. Unity `.meta` files are
not part of the content format and should not be distributed with a mod.

### Audio and video

Audio and video are loose files referenced by extensionless content addresses. Preserve the address
expected by the XML or update the corresponding definition. Application-level main-menu and
cutscene media lives under `Application/`; faction- and scenario-specific media belongs in the
pack.

### Game events

Game events define scheduled and result-triggered narrative behavior without a code rebuild. See
[Creating custom game events](GameEvents.md) for lifecycle, scheduling, targeting, conditions,
actions, schema validation, and complete examples.

### Preloaded assets

Preload manifests list textures, texture directories, and audio needed before a major screen is
shown. When adding required UI art or audio, update the corresponding manifest under
`Application/Preload/` or the pack's `Preload/` directory. Missing required preload content causes
the game to fail loudly instead of silently displaying an incomplete interface.

## Compatibility

Save files record the pack ID, pack version, and scenario ID. Changing those values may make an
existing save incompatible. Increment the pack version when publishing changes and test a new game
after modifying definitions or generation rules.
