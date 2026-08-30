# Capital Ships

Capital ships are reusable `CapitalShip` definitions beneath the faction catalog's `CapitalShips`
root. Runtime ships can contain officers, regiments, special forces, and starfighter squadrons.

## Example

```xml
<CapitalShips>
  <CapitalShip>
    <TypeID>CAPITAL_SHIP_EXAMPLE</TypeID>
    <DisplayName>Example Cruiser</DisplayName>
    <ShipNamePoolID>CRUISER_NAMES</ShipNamePoolID>
    <DisplayImagePath>Pack/Factions/Example/Units/CapitalShips/CAPITAL_SHIP_EXAMPLE/display</DisplayImagePath>
    <SmallDisplayImagePath>Pack/Factions/Example/Units/CapitalShips/CAPITAL_SHIP_EXAMPLE/small-display</SmallDisplayImagePath>
    <ManufacturingFactionInstanceIDs>
      <String>FACTION_EXAMPLE</String>
    </ManufacturingFactionInstanceIDs>
    <Roles>
      <CapitalShipRole>SecondaryLine</CapitalShipRole>
    </Roles>
    <ConstructionCost>45</ConstructionCost>
    <MaintenanceCost>30</MaintenanceCost>
    <ResearchOrder>0</ResearchOrder>
    <ResearchDifficulty>0</ResearchDifficulty>
    <MaxHullStrength>900</MaxHullStrength>
    <CurrentHullStrength>900</CurrentHullStrength>
    <DamageControl>8</DamageControl>
    <MaxShieldStrength>200</MaxShieldStrength>
    <ShieldRechargeRate>10</ShieldRechargeRate>
    <Hyperdrive>80</Hyperdrive>
    <SublightSpeed>4</SublightSpeed>
    <Maneuverability>2</Maneuverability>
    <StarfighterCapacity>1</StarfighterCapacity>
    <RegimentCapacity>2</RegimentCapacity>
    <PrimaryWeapons>
      <Entry>
        <Key><PrimaryWeaponType>Turbolaser</PrimaryWeaponType></Key>
        <Value><ArrayOfInt><int>20</int><int>10</int><int>30</int><int>30</int><int>20</int></ArrayOfInt></Value>
      </Entry>
      <Entry>
        <Key><PrimaryWeaponType>IonCannon</PrimaryWeaponType></Key>
        <Value><ArrayOfInt><int>0</int><int>0</int><int>0</int><int>0</int><int>0</int></ArrayOfInt></Value>
      </Entry>
      <Entry>
        <Key><PrimaryWeaponType>LaserCannon</PrimaryWeaponType></Key>
        <Value><ArrayOfInt><int>5</int><int>5</int><int>5</int><int>5</int><int>5</int></ArrayOfInt></Value>
      </Entry>
    </PrimaryWeapons>
    <WeaponRecharge>6</WeaponRecharge>
    <Bombardment>1</Bombardment>
    <TractorBeamPower>1</TractorBeamPower>
    <TractorBeamnRange>20</TractorBeamnRange>
    <HasGravityWell>false</HasGravityWell>
    <CanDestroyPlanets>false</CanDestroyPlanets>
    <DetectionRating>10</DetectionRating>
  </CapitalShip>
</CapitalShips>
```

`TractorBeamnRange` preserves the current serialized spelling, including the extra `n`.

## Roles and naming

`Roles` supplies AI and fleet-composition classifications. Accepted values are `PrimaryLine`,
`SecondaryLine`, `Escort`, `Interdictor`, `Transport`, `Carrier`, and `Flagship`.

`ShipNamePoolID` selects a faction name pool. If naming is enabled and the pool can supply a name,
the runtime ship receives a name without changing its `TypeID`.

Name pools are authored in the faction data selected by `FactionDataPath`:

```xml
<ShipNamePools>
  <NamePool>
    <NamePoolID>CRUISER_NAMES</NamePoolID>
    <FallbackNamePoolID>GENERAL_SHIP_NAMES</FallbackNamePoolID>
    <Names>
      <Name>Valiant</Name>
      <Name>Resolute</Name>
    </Names>
  </NamePool>
</ShipNamePools>
```

`FallbackNamePoolID` is optional. It supplies another pool when the selected pool is exhausted.
`NextNameIndex` is runtime state and should not be authored.

## Combat, movement, and capacity

| Field | Purpose |
| --- | --- |
| `MaxHullStrength` | Maximum hull points. |
| `CurrentHullStrength` | Starting hull points copied from the definition; normally equal to the maximum. |
| `DamageControl` | Hull repair capability. |
| `MaxShieldStrength` | Maximum shield points. |
| `ShieldRechargeRate` | Shield recovery rate. |
| `Hyperdrive` | Strategic travel rating. |
| `SublightSpeed` | Tactical sublight speed. |
| `Maneuverability` | Tactical maneuver rating. |
| `StarfighterCapacity` | Number of starfighter squadrons the ship can carry. |
| `RegimentCapacity` | Number of regiments the ship can carry. |
| `PrimaryWeapons` | Five authored values for each of `Turbolaser`, `IonCannon`, and `LaserCannon`. |
| `WeaponRecharge` | Weapon recharge rating. |
| `Bombardment` | Planetary bombardment strength. |
| `HasGravityWell` | Enables gravity-well behavior. |
| `CanDestroyPlanets` | Enables planet-destroying behavior. |
| `DetectionRating` | Detection strength. |

`ProductionCapacity` is loaded on a capital ship but is not currently used to manufacture cargo.
Do not use it to model a mobile shipyard.

## Starting fleets

Scenario generation's `UnitDeployment/FixedFleets` and faction budget tables create capital ships by
`TypeID`. A fixed fleet may also declare cargo entries. Keep fighter and regiment counts within each
ship's authored capacities.

---

<p align="center"><a href="Facilities.md">← Facilities</a> · <a href="Index.md">Unit guide</a> · <a href="Starfighters.md">Starfighters →</a></p>
