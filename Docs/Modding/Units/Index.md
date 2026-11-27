# Creating and Modifying Units

Unit definitions describe the reusable types loaded when a campaign begins. Facilities are shared
by the pack; every other unit catalog belongs to a faction. Runtime copies are created from these
definitions during galaxy generation, manufacturing, and event actions.

This guide explains where unit catalogs are declared, which fields are common to every unit, and
how unit definitions become campaign instances.

## Unit catalogs

`pack.xml` points to the shared facility catalog and each faction definition:

```xml
<ContentPack>
  <BuildingsPath>Shared/Data/buildings.xml</BuildingsPath>
  <FactionPaths>
    <Path>Factions/Example/faction.xml</Path>
  </FactionPaths>
</ContentPack>
```

The faction definition points to its five unit catalogs:

```xml
<Faction>
  <ID>FACTION_EXAMPLE</ID>
  <CapitalShipsPath>Factions/Example/Data/capital-ships.xml</CapitalShipsPath>
  <StarfightersPath>Factions/Example/Data/starfighters.xml</StarfightersPath>
  <RegimentsPath>Factions/Example/Data/regiments.xml</RegimentsPath>
  <SpecialForcesPath>Factions/Example/Data/special-forces.xml</SpecialForcesPath>
  <OfficersPath>Factions/Example/Data/officers.xml</OfficersPath>
</Faction>
```

| Catalog | Root | Entry | Guide |
| --- | --- | --- | --- |
| Facilities | `Buildings` | `Building` | [Facilities](Facilities.md) |
| Capital ships | `CapitalShips` | `CapitalShip` | [Capital ships](CapitalShips.md) |
| Starfighters | `Starfighters` | `Starfighter` | [Starfighters](Starfighters.md) |
| Regiments | `Regiments` | `Regiment` | [Regiments](Regiments.md) |
| Special forces | `SpecialForces` | `SpecialForce` | [Special forces](SpecialForces.md) |
| Officers | `Officers` | `Officer` | [Officers](Officers.md) |

## Definitions and instances

`TypeID` identifies a reusable unit definition. Manufactured and generated facilities, ships,
starfighters, regiments, and special-forces units receive new `InstanceID` values at runtime while
retaining their definition's `TypeID`.

Officers are different: each officer is a unique authored character, so an officer definition has
both a `TypeID` and a stable `InstanceID`. Events and starting-officer rules normally refer to that
`InstanceID`.

Keep every `TypeID` unique across all repeatable unit catalogs. Keep every officer `InstanceID`
unique across the campaign. Changing an identifier can break scenario references, events, and
existing saves.

## Common fields

All unit entries may use the following presentation fields:

| Field | Purpose |
| --- | --- |
| `TypeID` | Stable definition identifier. |
| `DisplayName` | Player-facing name. |
| `DisplayImagePath` | Primary strategy-view image. |
| `SmallDisplayImagePath` | Compact strategy-view image. |
| `MessageImagePath` | Image used when the unit is a message subject. |
| `InTransitImagePath` | Primary image used while moving. |
| `InTransitSmallImagePath` | Compact image used while moving. |
| `DamagedImagePath` | Primary damaged-state image. |
| `DamagedSmallImagePath` | Compact damaged-state image. |
| `Description` | General descriptive text. |
| `EncyclopediaImagePath` | Encyclopedia image. |
| `EncyclopediaStats` | Authored label-and-value rows shown in the encyclopedia. |
| `EncyclopediaDescription` | Encyclopedia prose. |

Asset paths omit file extensions. Use `Pack/` for assets inside the active pack and `Application/`
for shared application assets.

### Artwork imports and older packs

The content editor imports unit images into their faction and unit folders, using the image slot
as the filename. For example, a starfighter's main image is stored at
`Factions/Alliance/Units/Starfighters/<TypeID>/display.png`. The source image's filename does not
become its content address. Imports are converted to PNG. Replacing an image and regenerating
artwork keep its address; undo and redo restore both the image and its editable layers.
Choosing **Import as new** deliberately creates a separate file beside the unit's other artwork.

Saving or exporting a pack organizes unit artwork imported by older editor versions under
`Assets/`. Conversion checks every destination before writing, retains the old image files, and
backs up the affected XML under `.rebellion2-editor/Backups/`. Exported packs exclude these private
backups and undo revisions. The included `artwork-aliases.xml` keeps older saved image addresses
pointing at the current artwork; retain this file when distributing updates.

Shared images with different unit destinations stay shared and are reported instead of being
assigned to an arbitrary unit. Use **Replace file** to update every use, or explicitly import
separate images for those slots. If another image or layer file already occupies a destination,
conversion preserves it and chooses an unused numbered slot filename. The stored address remains
stable for later replacements and regeneration. Converted legacy addresses keep resolving through
the artwork map. Missing source images still stop conversion before any files are changed.

Encyclopedia statistics preserve their authored order:

```xml
<EncyclopediaStats>
  <EncyclopediaEntryStat>
    <Label>Maintenance Cost</Label>
    <Value>10</Value>
  </EncyclopediaEntryStat>
</EncyclopediaStats>
```

### Gameplay layers and shared officer effects

In a unit's **Artwork** tab, open **Edit layers** beside the combined gameplay preview to change
the drawing order of the unit, travel, damage or injury, and capture images. This reuses the
existing image files. Saving applies the order to gameplay cards; packs without a custom order
retain their current appearance.

For officers, **Apply to all** beside an injury, capture, or hyperspace effect offers a confirmation
before assigning that image to every officer in the pack. Officers share the selected file, so
replacing it updates every officer that uses it. Individual portrait images are unchanged.

### Manufacturing fields

Manufacturable definitions also share these authored fields:

| Field | Purpose |
| --- | --- |
| `ConstructionCost` | Production points required to finish one unit. |
| `MaintenanceCost` | Maintenance charged for the unit. |
| `ManufacturingFactionInstanceIDs` | Factions allowed to manufacture the definition. Omission permits any faction; an empty collection permits none. |
| `ResearchOrder` | Position in its research sequence. `0` is available without research. |
| `ResearchDifficulty` | Research capacity required to unlock that position. |
| `BaseBuildSpeed` | Loaded legacy value; current production timing does not consume it. |

Do not author runtime state such as `ProducerOwnerID`, `ProducerPlanetID`,
`ManufacturingQueueSequence`, `ManufacturingProgress`, `ManufacturingStatus`, or `Movement` in a
unit catalog. The campaign owns those values.

## Making units available at game start

Adding a definition makes it available to the loader; it does not guarantee that an instance starts
on the map. Scenario generation controls initial deployment:

- `FacilityGeneration` selects starting facilities and headquarters loadouts.
- `UnitDeployment` creates fixed and budgeted fleets and garrisons from unit `TypeID` values.
- `Officers` selects starting officers by their authored `InstanceID` values.
- Events may create repeatable units with `SpawnUnits` or place existing officers.

See the scenario's `GenerationConfigPath` and
[`generation-config.xsd`](../../../Assets/Content/Application/Schemas/generation-config.xsd) for the
complete generation contract.

## Validation

Unit catalogs do not currently have dedicated XSD files. Treat a successful content load as the
authoritative structural check. After editing a catalog:

1. Run the content repository's validation build.
2. Install or copy the content into a private development workspace.
3. Start a new campaign and inspect the unit in its encyclopedia and relevant production menu.
4. Manufacture, move, save, and reload the unit before publishing the change.

Existing saves retain runtime copies, so definition changes are most reliably tested in a new game.

The content editor rejects negative construction and maintenance costs, production rates, and ship
cargo capacities. Force-sensitivity probability must be between 0 and 100. Authored `ForceValue` and
`ForceTrainingAdjustment` values must fit the runtime's signed 16-bit range (-32768 through 32767).
Validation also checks faction theme keys and rejects resource profiles whose minimum exceeds
their maximum. These checks also apply to XML edits and staged exports or test-game launches.

Zero production rates, explicitly empty manufacturing-faction lists, factions with every normal
mission disabled, and bookmark layouts with no usable rows remain legal. Validation and export
show warnings identifying these restrictions so authors can review them without being prevented
from exporting intentional settings. An absent manufacturing-faction list remains unrestricted.

---

<p align="center"><a href="../Index.md">Modding guide</a> · <a href="Officers.md">Officers →</a></p>
