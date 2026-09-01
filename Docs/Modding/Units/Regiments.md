# Regiments

Regiments are reusable ground-unit definitions. Add `Regiment` entries beneath the faction
catalog's `Regiments` root.

## Example

```xml
<Regiments>
  <Regiment>
    <TypeID>REGIMENT_EXAMPLE</TypeID>
    <DisplayName>Example Regiment</DisplayName>
    <DisplayImagePath>Pack/Factions/Example/Units/Regiments/REGIMENT_EXAMPLE/display</DisplayImagePath>
    <ManufacturingFactionInstanceIDs>
      <String>FACTION_EXAMPLE</String>
    </ManufacturingFactionInstanceIDs>
    <ConstructionCost>8</ConstructionCost>
    <MaintenanceCost>6</MaintenanceCost>
    <ResearchOrder>0</ResearchOrder>
    <ResearchDifficulty>0</ResearchDifficulty>
    <AttackRating>6</AttackRating>
    <DefenseRating>4</DefenseRating>
    <DetectionRating>12</DetectionRating>
    <BombardmentDefense>5</BombardmentDefense>
    <UprisingDefense>5</UprisingDefense>
  </Regiment>
</Regiments>
```

## Regiment fields

| Field | Purpose |
| --- | --- |
| `AttackRating` | Strength contributed during a planetary assault. |
| `DefenseRating` | Strength contributed while defending a planet. |
| `DetectionRating` | Detection strength against hostile activity. |
| `BombardmentDefense` | Resistance to orbital bombardment. |
| `UprisingDefense` | Strength contributed to suppressing unrest. |

Regiments use training facilities and the `Troop` research sequence. They may begin directly on a
planet through fixed garrisons and deployment budgets, or aboard a capital ship through fixed-fleet
cargo. A carried regiment consumes one point of `RegimentCapacity`.

---

<p align="center"><a href="Starfighters.md">← Starfighters</a> · <a href="Index.md">Unit guide</a> · <a href="SpecialForces.md">Special forces →</a></p>
