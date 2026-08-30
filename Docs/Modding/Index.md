# Modding

Rebellion 2 loads its art, audio, video, configuration, and game data from an external `Content`
directory. Most content can therefore be changed without rebuilding the game.

## Limitations and redistribution

The game currently loads complete packs from `Content/Packs/`. It does not support derived packs,
sparse overlays, base-pack fallback, or a separate `Mods` directory.

**Only distribute files you have the right to distribute.** For mods based on the classic pack,
distribute your changes rather than the modified pack, and have players apply them to their installed
content. Players and mod developers must own the original game and obtain its content through the
ownership-verifying installer.

## Create a private development workspace

Installer updates and repairs own the installed `Content` directory and may remove additional packs
placed there. Keep your mod source outside the installation. Copy the installed content to a private
workspace, then copy the base pack inside that workspace:

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

## Guides

- [Creating game events](Events/Index.md)

## Compatibility

Saves require the active pack ID, version, and scenario to match. Increment the pack version when
publishing changes, then test both new games and existing saves.
