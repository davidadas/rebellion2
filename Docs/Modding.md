# Modding

Rebellion 2 loads its art, audio, video, configuration, and game data from an external `Content`
directory. Most content can therefore be changed without rebuilding the game.

## Limitations and redistribution

The game currently loads complete packs from `Content/Packs/`. It does not support derived packs,
sparse overlays, base-pack fallback, or a separate `Mods` directory.

**NEVER redistribute the installed `Content` directory, the original content pack, or copyrighted
assets taken from them.** Players and mod developers must own the original game and obtain its
content through the ownership-verifying installer.

A complete pack may be distributed only when the author has the right to distribute every file it
contains. For a classic-based project, distribute only the changes and have each player apply them
to a private copy of their own installed content. Never distribute that resulting content tree.

Complete, independently authored packs may be installed beneath `Content/Packs/`, but an installer
repair may remove them. Keep their distributable source elsewhere.

## Create a private development workspace

Installer updates and repairs own the installed `Content` directory. Copy it to a private workspace
outside the installation, then copy the base pack inside that workspace:

```text
MyModWorkspace/
  Content/
    catalog.xml
    Application/
    Packs/
      ClassicGalacticCivilWar/
      MyPack/
```

Give `MyPack/pack.xml` a unique `ID`, `DisplayName`, and `Version`. The declared `ID`, not the folder
name, identifies the pack in settings and saves.

Launch against the workspace:

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
{
  "Content": {
    "ActivePackID": "my-content-pack",
    "ActiveScenarioID": "standard"
  }
}
```

IDs must match the selected pack and one of its scenarios. An empty scenario ID uses the pack's
`DefaultScenarioID`; an unavailable saved pack falls back to the catalog. There is no in-game pack
selector yet, so edit `user-settings.json` only while the game is closed.

For development, leave the saved selection empty and make the working pack the workspace catalog
default. Pack-loading errors will then remain visible instead of triggering fallback.

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

The following sections describe complete-pack authoring. A classic-derived result remains private.

## Common changes

### Simulation rules

`Content/Application/Rules/game.xml` contains application defaults and is owned by the game. A pack
may declare a sparse `GameConfigPath` override in `pack.xml`:

- A leaf value in the pack file replaces the default value.
- A section merges recursively, so unlisted siblings keep their defaults.
- A lookup table (repeated `Entry` elements) is replaced wholesale, never merged entry-by-entry.

Omit unchanged values and omit `GameConfigPath` when no override is needed. The merged result must
match `Content/Application/Schemas/game-config.xsd`.

### Campaign setup

`scenario.xml` selects the scenario identity, factions, default player faction, and generation
configuration. `generation.xml` controls starting planets, headquarters, garrisons, difficulty,
and officer counts and must match `Content/Application/Schemas/generation-config.xsd`.

Add a new scenario's `scenario.xml` path to `ScenarioPaths` in `pack.xml`, then select its ID through
the workspace catalog or user settings.

### Units, officers, planets, and buildings

`pack.xml` and each `faction.xml` reference the entity catalogs. Pack-level catalogs include
sectors, buildings, events, and messages; faction catalogs include units, officers, themes, and
encyclopedia entries.

`TypeID` and `InstanceID` values must be present and unique within their respective scopes.
References between files use the appropriate identifier, so renaming one requires updating every
reference to it. Event and message catalogs are separate files selected by `GameEventsPath` and
`MessageDefinitionsPath` in `pack.xml`.

### UI appearance

Faction `ThemePath` files address textures for windows, controls, advisors, and messages. Preserve
an existing address when replacing media, or update the theme reference.

PNG, JPG, and JPEG images are loaded directly from the content directory. Unity `.meta` files are
not part of the content format and should not be distributed with a pack.

### Audio and video

Audio and video use extensionless content addresses. Application media belongs under
`Application/`; faction and scenario media belongs in the pack.

### Game events

See [Creating game events](Events/Index.md) for event lifecycle, targeting, actions, validation,
and examples.

### Preloaded assets

Preload manifests list media required before a screen is shown. Update the applicable manifest
under `Application/Preload/` or the pack's `Preload/` directory when adding required media.

## Compatibility

Saves require the active pack ID, version, and scenario to match. Increment the pack version when
publishing changes, then test both new games and existing saves.
