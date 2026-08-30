# Special Forces

Special forces are reusable mission participants. Add `SpecialForce` entries beneath the faction
catalog's `SpecialForces` root. The singular entry name is `SpecialForce`, not `SpecialForces`.

## Example

```xml
<SpecialForces>
  <SpecialForce>
    <TypeID>SPECIAL_FORCES_EXAMPLE</TypeID>
    <DisplayName>Example Operatives</DisplayName>
    <DisplayImagePath>Pack/Factions/Example/Units/SpecialForces/SPECIAL_FORCES_EXAMPLE/display</DisplayImagePath>
    <SmallDisplayImagePath>Pack/Factions/Example/Units/SpecialForces/SPECIAL_FORCES_EXAMPLE/small-display</SmallDisplayImagePath>
    <MessageImagePath>Pack/Factions/Example/Units/SpecialForces/SPECIAL_FORCES_EXAMPLE/message</MessageImagePath>
    <ManufacturingFactionInstanceIDs>
      <String>FACTION_EXAMPLE</String>
    </ManufacturingFactionInstanceIDs>
    <ConstructionCost>2</ConstructionCost>
    <MaintenanceCost>1</MaintenanceCost>
    <ResearchOrder>0</ResearchOrder>
    <ResearchDifficulty>0</ResearchDifficulty>
    <Ratings>
      <Entry><Key>Diplomacy</Key><Value>0</Value></Entry>
      <Entry><Key>Espionage</Key><Value>45</Value></Entry>
      <Entry><Key>Combat</Key><Value>30</Value></Entry>
      <Entry><Key>Leadership</Key><Value>0</Value></Entry>
    </Ratings>
    <AllowedMissionTypeIDs>
      <String>Espionage</String>
      <String>Sabotage</String>
    </AllowedMissionTypeIDs>
  </SpecialForce>
</SpecialForces>
```

## Mission fields

`Ratings` uses the same `Diplomacy`, `Espionage`, `Combat`, and `Leadership` keys as officer
mission ratings. Special-forces ratings do not improve through mission use.

`AllowedMissionTypeIDs` is an explicit allowlist. A special-forces unit is eligible only for the
listed mission type IDs, even if it has a useful rating for another mission. These values must match
the runtime mission type IDs exactly, such as `Espionage`, `Sabotage`, `Reconnaissance`,
`InciteUprising`, or `SubdueUprising`.

Special forces use training facilities and the `Troop` research sequence. They may be stationed on
a planet or carried aboard a capital ship. The current capital-ship model does not expose a separate
special-forces capacity, but ordinary ownership and movement validation still apply.

---

<p align="center"><a href="Regiments.md">← Regiments</a> · <a href="Index.md">Unit guide</a></p>
