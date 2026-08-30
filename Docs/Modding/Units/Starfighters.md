# Starfighters

Starfighters are reusable squadron definitions. Add `Starfighter` entries beneath the faction
catalog's `Starfighters` root.

## Example

```xml
<Starfighters>
  <Starfighter>
    <TypeID>STARFIGHTER_EXAMPLE</TypeID>
    <DisplayName>Example Fighter Squadron</DisplayName>
    <DisplayImagePath>Pack/Factions/Example/Units/Starfighters/STARFIGHTER_EXAMPLE/display</DisplayImagePath>
    <SmallDisplayImagePath>Pack/Factions/Example/Units/Starfighters/STARFIGHTER_EXAMPLE/small-display</SmallDisplayImagePath>
    <ManufacturingFactionInstanceIDs>
      <String>FACTION_EXAMPLE</String>
    </ManufacturingFactionInstanceIDs>
    <ConstructionCost>6</ConstructionCost>
    <MaintenanceCost>4</MaintenanceCost>
    <ResearchOrder>0</ResearchOrder>
    <ResearchDifficulty>0</ResearchDifficulty>
    <MaxSquadronSize>12</MaxSquadronSize>
    <CurrentSquadronSize>12</CurrentSquadronSize>
    <DetectionRating>10</DetectionRating>
    <Bombardment>1</Bombardment>
    <ShieldStrength>4</ShieldStrength>
    <Hyperdrive>60</Hyperdrive>
    <SublightSpeed>8</SublightSpeed>
    <Agility>5</Agility>
    <LaserCannon>5</LaserCannon>
    <IonCannon>0</IonCannon>
    <Torpedoes>4</Torpedoes>
    <LaserRange>10</LaserRange>
    <IonRange>0</IonRange>
    <TorpedoRange>6</TorpedoRange>
  </Starfighter>
</Starfighters>
```

## Squadron fields

| Field | Purpose |
| --- | --- |
| `MaxSquadronSize` | Full number of fighters in one squadron. |
| `CurrentSquadronSize` | Starting number copied from the definition; normally equal to the maximum. |
| `DetectionRating` | Detection strength. |
| `Bombardment` | Planetary bombardment strength. |
| `ShieldStrength` | Shield strength per fighter. |
| `Hyperdrive` | Strategic travel rating. A non-hyperdrive fighter must travel aboard a carrier. |
| `SublightSpeed` | Tactical sublight speed. |
| `Agility` | Tactical agility. |
| `LaserCannon`, `IonCannon`, `Torpedoes` | Per-fighter weapon strengths. |
| `LaserRange`, `IonRange`, `TorpedoRange` | Ranges for the corresponding weapons. |

Battle reports may use `BattleResultImagePath`, `BattleResultInTransitImagePath`, and
`BattleResultDamagedImagePath`. Strategy views use the common normal, in-transit, and damaged image
paths described by the [unit guide](Index.md#common-fields).

Starfighters use shipyards and the `Ship` research sequence. Starting squadrons are created through
fixed-fleet cargo or scenario unit-deployment budgets.

---

<p align="center"><a href="CapitalShips.md">← Capital ships</a> · <a href="Index.md">Unit guide</a> · <a href="Regiments.md">Regiments →</a></p>
