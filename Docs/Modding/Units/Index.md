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

Encyclopedia statistics preserve their authored order:

```xml
<EncyclopediaStats>
  <EncyclopediaEntryStat>
    <Label>Maintenance Cost</Label>
    <Value>10</Value>
  </EncyclopediaEntryStat>
</EncyclopediaStats>
```

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

---

<p align="center"><a href="../Index.md">Modding guide</a> · <a href="Officers.md">Officers →</a></p>
