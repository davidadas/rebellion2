# Modding

Rebellion 2 loads modded art, audio, video, configuration, and game data from loose files. Most
content can therefore be changed without rebuilding the game.

## Mods and redistribution

Install all user-created content in the player-managed `Mods` directory. On Windows that directory
is inside the game's installation directory. On macOS it is
`~/Library/Application Support/Rebellion 2/Mods`.

**Only distribute files you have the right to distribute.** For mods based on the classic pack,
distribute only your mod definition and replacement files. Players and mod developers must own
*Star Wars: Rebellion* or
*Star Wars: Empire at War: Gold Pack* and obtain the content through the ownership-verifying
installer.

Each immediate subdirectory containing a `mod.xml` is loaded when its `BasePackID` matches the
selected content pack. Mods load by folder name in ordinal order; when multiple mods contain the
same logical file, the later mod wins. A missing mod file falls back to the selected content pack.

```text
Mods/
  CapitalShipRebalance/
    mod.xml
    Content/
      Pack/
        Factions/Alliance/Data/capital-ships.xml
      Application/
        MainMenu/UI/optional-replacement.png
```

The minimal mod definition is:

```xml
<ContentModDefinition>
  <ID>capital-ship-rebalance</ID>
  <Version>1.0.0</Version>
  <DisplayName>Capital Ship Rebalance</DisplayName>
  <BasePackID>classic-galactic-civil-war</BasePackID>
</ContentModDefinition>
```

A mod replaces a complete file at the matching logical path. It does not merge individual objects
or XML fields within that file. Files beneath the mod's `Content/Pack` directory replace files from
the selected base pack; files beneath `Content/Application` replace shared application files. The
installer, launcher, application updater, and content updater do not overwrite the `Mods` directory.

## Messages

Automatic strategy messages are selected from the catalog referenced by
`MessageDefinitionsPath`. Set the optional `ShowSubjectImage` element to `true` when an
automatic message should use its subject officer's current message image as an overlay. Every
automatic message honors this setting, which defaults to `false`:

```xml
<MessageDefinition>
  <ResultType>TraitorDiscovered</ResultType>
  <MessageType>Mission</MessageType>
  <Subject>{discoverer} Discovers Traitor</Subject>
  <Body>{discoverer} has discovered that {traitor} betrayed us.</Body>
  <ShowSubjectImage>true</ShowSubjectImage>
  <BackgroundImage Key="mission_report"/>
</MessageDefinition>
```

Authored event messages expose the same setting as the `ShowSubjectImage` attribute on
`SendMessage`. See [`SendMessage`](Events/Actions.md#sendmessage) for its complete options and
overlay precedence.

## Guides

- [Creating game events](Events/Index.md)
- [Creating and modifying units](Units/Index.md)

## Compatibility

Saves require the active pack ID, version, and scenario to match. Increment the pack version when
publishing changes, then test both new games and existing saves.

The enabled mod list is not yet recorded in saves, and there is not yet an in-game mod manager.
