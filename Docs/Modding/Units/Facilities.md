# Facilities

Facilities are serialized as `Building` entries beneath the shared `Buildings` root. Unlike the
other repeatable unit catalogs, their file is selected by the pack's `BuildingsPath`.

## Example

```xml
<Buildings>
  <Building>
    <TypeID>FACILITY_EXAMPLE</TypeID>
    <DisplayName>Example Shipyard</DisplayName>
    <DisplayImagePath>Pack/Shared/Units/Facilities/FACILITY_EXAMPLE/display</DisplayImagePath>
    <SmallDisplayImagePath>Pack/Shared/Units/Facilities/FACILITY_EXAMPLE/small-display</SmallDisplayImagePath>
    <EncyclopediaImagePath>Pack/Shared/Units/Facilities/FACILITY_EXAMPLE/encyclopedia</EncyclopediaImagePath>
    <ManufacturingFactionInstanceIDs>
      <String>FACTION_EXAMPLE</String>
    </ManufacturingFactionInstanceIDs>
    <BuildingType>Shipyard</BuildingType>
    <ProductionType>Ship</ProductionType>
    <ProcessRate>6</ProcessRate>
    <ConstructionCost>40</ConstructionCost>
    <MaintenanceCost>20</MaintenanceCost>
    <ResearchOrder>0</ResearchOrder>
    <ResearchDifficulty>0</ResearchDifficulty>
  </Building>
</Buildings>
```

## Facility behavior

`BuildingType` accepts `Mine`, `Refinery`, `Shipyard`, `TrainingFacility`,
`ConstructionFacility`, `Defense`, `Weapon`, or `Headquarters`. `None` supplies no category
behavior.

| Field | Purpose |
| --- | --- |
| `ProductionType` | What the facility produces: `Ship`, `Building`, `Troop`, or `None`. |
| `ProcessRate` | Ticks between production points or resource-processing cycles. Lower positive values are faster. |
| `Bombardment` | Bombardment value supplied by the facility. |
| `ShieldStrength` | Planetary shield strength supplied by a defense facility. |
| `WeaponPower` | Strength of a planetary defense weapon. |
| `DefenseWeaponEffect` | Whether the weapon inflicts `HullDamage` or `ShieldDamage`. |
| `ProtectedUnitTypeIDs` | Unit types protected by a unit-specific shield facility. |
| `Upgrades` | Facility `TypeID` values that may replace this facility as authored upgrades. |

`ProductionModifier` is loaded and preserved but is not currently consumed by production
resolution. Do not use it as a substitute for `ProcessRate`.

Resource facilities use the same `ProcessRate` field. Their exact resource inputs and outputs are
controlled by `BuildingType` and the game configuration rather than additional per-entry fields.

## Starting facilities

The scenario's `FacilityGeneration` section controls ordinary starting facilities. Use its weighted
core and rim facility tables, `MineTypeID`, and `HQLoadouts` to reference facility `TypeID` values.
Defining a facility in `buildings.xml` alone does not place it on a planet.

---

<p align="center"><a href="Officers.md">← Officers</a> · <a href="Index.md">Unit guide</a> · <a href="CapitalShips.md">Capital ships →</a></p>
