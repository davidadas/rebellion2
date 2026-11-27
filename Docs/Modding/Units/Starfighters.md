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
    <UprisingDefense>0</UprisingDefense>
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
| `Role` | One combat role: `Interceptor`, `Multirole`, `Bomber`, or `HeavyAssault`. Without an explicit formation priority, present designs are ordered by role, then type ID. Leave unset to order after classified designs. |
| `FormationPriority` | Optional fixed place in the faction's formation order, including when the design is absent. Lower numbers come first and override role ordering. |
| `MaxSquadronSize` | Full number of fighters in one squadron. |
| `CurrentSquadronSize` | Starting number copied from the definition; normally equal to the maximum. |
| `DetectionRating` | Detection strength. |
| `Bombardment` | Planetary bombardment strength. |
| `ShieldStrength` | Shield strength per fighter. |
| `UprisingDefense` | Value contributed when the complete squadron is destroyed in the global officer-loyalty battle calculation; partial fighter losses do not count. |
| `Hyperdrive` | Strategic travel rating. A non-hyperdrive fighter must travel aboard a carrier. |
| `SublightSpeed` | Tactical sublight speed. |
| `Agility` | Tactical agility. |
| `LaserCannon`, `IonCannon`, `Torpedoes` | Per-fighter weapon strengths. |
| `LaserRange`, `IonRange`, `TorpedoRange` | Ranges for the corresponding weapons. |

The deprecated `Roles` list remains readable in older packs and saved games. Its lowest-valued
role is used, with the former `Fighter` role treated as `Multirole`. An explicit `Role` takes
precedence over the list. New saves write the single `Role`; changing it in the content editor
replaces the older list. Existing explicit formation priorities are preserved.

Battle reports may use `BattleResultImagePath`, `BattleResultInTransitImagePath`, and
`BattleResultDamagedImagePath`. Strategy views use the common normal, in-transit, and damaged image
paths described by the [unit guide](Index.md#common-fields).

Starfighters use shipyards and the `Ship` research sequence. Starting squadrons are created through
fixed-fleet cargo or scenario unit-deployment budgets.

---

<p align="center"><a href="CapitalShips.md">← Capital ships</a> · <a href="Index.md">Unit guide</a> · <a href="Regiments.md">Regiments →</a></p>
