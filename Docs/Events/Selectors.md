# Selectors

Selectors find typed collections of scene nodes for bindings, conditionals, and actions. Direct
selectors return every active matching object when no optional filter is supplied.

## Planet selectors

### SelectPlanets

Selects active, non-destroyed planets.

**Optional options**

- `InstanceID` **[Optional]:** The `InstanceID` of the planet to select.
- `OwnerFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must currently own the planet.
- `SectorType` **[Optional]:** Require `Core` or `OuterRim`.

```xml
<SelectPlanets OwnerFactionInstanceID="FNALL1" SectorType="Core"/>
```

### SelectPlanetSectors

Selects planet sectors.

**Optional options**

- `InstanceID` **[Optional]:** The `InstanceID` of the planet sector to select.
- `SectorType` **[Optional]:** `Core` or `OuterRim` filter.

```xml
<SelectPlanetSectors SectorType="OuterRim"/>
```

## Personnel and mission selectors

### SelectOfficers

Selects officers.

**Optional options**

- `InstanceID` **[Optional]:** The `InstanceID` of the officer to select.
- `OwnerFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must own the officer.
- `IsCaptured` **[Optional]:** Capture-state filter.
- `IncludeInactive` **[Optional]:** Include inactive officers; defaults to `false`.
- `PlanetInstanceID` **[Optional]:** The `InstanceID` of the planet where the officer must be located.
- `PlanetBinding` **[Optional]:** Bound location. Takes precedence over `PlanetInstanceID`.

```xml
<SelectOfficers PlanetInstanceID="NABOO"
                OwnerFactionInstanceID="FNALL1"
                IsCaptured="false"/>
```

### SelectSpecialForces

Selects special-forces units.

**Optional options**

- `InstanceID` **[Optional]:** The `InstanceID` of the special-forces unit to select.
- `PlanetInstanceID` **[Optional]:** The `InstanceID` of the planet where the unit must be located.
- `PlanetBinding` **[Optional]:** Bound location. Takes precedence over `PlanetInstanceID`.
- `OwnerFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must own the unit.

```xml
<SelectSpecialForces PlanetInstanceID="NABOO" OwnerFactionInstanceID="FNALL1"/>
```

### SelectMissions

Selects missions.

**Optional options**

- `InstanceID` **[Optional]:** The `InstanceID` of the mission to select.
- `PlanetInstanceID` **[Optional]:** The `InstanceID` of the planet where the mission must be located.
- `PlanetBinding` **[Optional]:** Bound location. Takes precedence over `PlanetInstanceID`.
- `OwnerFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must own the mission.

```xml
<SelectMissions PlanetBinding="$planet" OwnerFactionInstanceID="FNEMP1"/>
```

## Fleet and unit selectors

### SelectFleets

Selects fleets.

**Optional options**

- `InstanceID` **[Optional]:** The `InstanceID` of the fleet to select.
- `PlanetInstanceID` **[Optional]:** The `InstanceID` of the planet where the fleet must be located.
- `PlanetBinding` **[Optional]:** Bound location. Takes precedence over `PlanetInstanceID`.
- `OwnerFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must own the fleet.

```xml
<SelectFleets PlanetInstanceID="NABOO" OwnerFactionInstanceID="FNALL1"/>
```

### SelectCapitalShips

Selects capital ships.

**Optional options**

- `InstanceID` **[Optional]:** The `InstanceID` of the capital ship to select.
- `PlanetInstanceID` **[Optional]:** The `InstanceID` of the planet where the capital ship must be located.
- `PlanetBinding` **[Optional]:** Bound location. Takes precedence over `PlanetInstanceID`.
- `OwnerFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must own the capital ship.
- `TypeID` **[Optional]:** The `TypeID` of the capital-ship definition to select.
- `ManufacturingStatus` **[Optional]:** `Building` or `Complete` filter.

```xml
<SelectCapitalShips TypeID="ALCS008" ManufacturingStatus="Complete"/>
```

### SelectStarfighters

Selects starfighters. It supports the same filters as `SelectCapitalShips`.

**Optional options**

- `InstanceID` **[Optional]:** The `InstanceID` of the starfighter to select.
- `PlanetInstanceID` **[Optional]:** The `InstanceID` of the planet where the starfighter must be located.
- `PlanetBinding` **[Optional]:** Bound location. Takes precedence over `PlanetInstanceID`.
- `OwnerFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must own the starfighter.
- `TypeID` **[Optional]:** The `TypeID` of the starfighter definition to select.
- `ManufacturingStatus` **[Optional]:** `Building` or `Complete` filter.

```xml
<SelectStarfighters PlanetBinding="$planet"
                    OwnerFactionInstanceID="FNEMP1"
                    ManufacturingStatus="Complete"/>
```

### SelectRegiments

Selects regiments. It supports the same filters as `SelectCapitalShips`.

**Optional options**

- `InstanceID` **[Optional]:** The `InstanceID` of the regiment to select.
- `PlanetInstanceID` **[Optional]:** The `InstanceID` of the planet where the regiment must be located.
- `PlanetBinding` **[Optional]:** Bound location. Takes precedence over `PlanetInstanceID`.
- `OwnerFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must own the regiment.
- `TypeID` **[Optional]:** The `TypeID` of the regiment definition to select.
- `ManufacturingStatus` **[Optional]:** `Building` or `Complete` filter.

```xml
<SelectRegiments PlanetBinding="$planet" OwnerFactionInstanceID="FNALL1"/>
```

### SelectBuildings

Selects buildings.

**Optional options**

- `InstanceID` **[Optional]:** The `InstanceID` of the building to select.
- `PlanetInstanceID` **[Optional]:** The `InstanceID` of the planet where the building must be located.
- `PlanetBinding` **[Optional]:** Bound location. Takes precedence over `PlanetInstanceID`.
- `OwnerFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must own the building.
- `TypeID` **[Optional]:** The `TypeID` of the building definition to select.
- `ManufacturingStatus` **[Optional]:** `Building` or `Complete` filter.
- `Category` **[Optional]:** `Any`, `PlanetaryDefense`, or `ManufacturingFacility` filter.

`PlanetaryDefense` includes `Defense` and `Weapon` buildings. `ManufacturingFacility` includes
`Shipyard`, `TrainingFacility`, and `ConstructionFacility` buildings.

```xml
<SelectBuildings PlanetBinding="$planet" Category="PlanetaryDefense"/>
```

### SelectManufacturingOrders

Selects queued manufacturing items.

**Optional options**

- `PlanetInstanceID` **[Optional]:** The `InstanceID` of the planet whose manufacturing orders are selected.
- `PlanetBinding` **[Optional]:** Bound location. Takes precedence over `PlanetInstanceID`.
- `OwnerFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must own the manufacturing planet.
- `ManufacturingType` **[Optional]:** `Ship`, `Building`, or `Troop` filter.

```xml
<SelectManufacturingOrders PlanetInstanceID="NABOO" ManufacturingType="Ship"/>
```

## Selector composition

### From

`From` combines the results of every child selector in authored order and removes duplicate scene
nodes. It is used by selectors that transform another selection.

**Required options**

- Child selectors **[Required]:** Their results form the candidate collection.

```xml
<From>
  <SelectCapitalShips PlanetBinding="$planet"/>
  <SelectStarfighters PlanetBinding="$planet"/>
  <SelectRegiments PlanetBinding="$planet"/>
</From>
```

### SelectRandom

Selects a random subset of the candidates returned by `From`. Use `Count` or the
`MinimumCount`/`MaximumCount` pair, never both.

**Required options**

- `From` **[Required]:** Selector collection.

**Optional options**

- `ChancePercent` **[Optional]:** Independent inclusion chance from `0` through `100`; defaults to `100`.
- `Count` **[Optional]:** Exact result count when at least that many candidates exist; otherwise every
  available candidate is returned.
- `MinimumCount` **[Optional]:** Nonnegative lower bound applied after independent chance rolls; defaults to `0`.
- `MaximumCount` **[Optional]:** Nonnegative upper bound applied after independent chance rolls.

```xml
<SelectRandom ChancePercent="25" MinimumCount="1" MaximumCount="3">
  <From>
    <SelectBuildings PlanetBinding="$planet" Category="PlanetaryDefense"/>
    <SelectRegiments PlanetBinding="$planet"/>
  </From>
</SelectRandom>
```

### SelectFirst

Returns the first distinct candidate from `From`. When used as a destination, placement or movement
checks candidates in authored order and uses the first one that accepts the units.

**Required options**

- `From` **[Required]:** Ordered selector collection.

```xml
<Destination>
  <SelectFirst>
    <From>
      <SelectFleets PlanetInstanceID="YAVIN" OwnerFactionInstanceID="FNALL1"/>
      <SelectPlanets InstanceID="YAVIN"/>
    </From>
  </SelectFirst>
</Destination>
```

### SelectBinding

Returns the scene node or scene-node collection stored by an explicit event binding. Its runtime
type must be valid for the consumer.

**Required options**

- `Binding` **[Required]:** `$alias` reference.

```xml
<!-- Given a MissionCompleted trigger that binds Participants as missionParticipants. -->
<RevealToFaction FactionInstanceID="FNALL1">
  <Targets>
    <SelectBinding Binding="$missionParticipants"/>
  </Targets>
</RevealToFaction>
```

### SelectNearestParent

Maps each source to its nearest parent of the requested type. It never returns the source itself.

**Required options**

- `Type` **[Required]:** `Galaxy`, `PlanetSector`, `Planet`, `Fleet`, `Mission`, or `CapitalShip` type.
- `From` **[Required]:** Source selector collection.

```xml
<SelectNearestParent Type="Planet">
  <From>
    <SelectOfficers InstanceID="LUKE_SKYWALKER"/>
  </From>
</SelectNearestParent>
```

### SelectPreviousLocation

Returns a unit's registered `LastParentInstanceID` when that node still resolves.

**Required options**

- `UnitInstanceID` **[Required]:** The `InstanceID` of the unit whose previous location is selected; use either this or `UnitBinding`.
- `UnitBinding` **[Required]:** Bound unit source; use either this or `UnitInstanceID`.

```xml
<SelectPreviousLocation UnitInstanceID="LUKE_SKYWALKER"/>
```

### SpawnUnits

Creates detached units from an existing unit definition.

**Required options**

- `TypeID` **[Required]:** The `TypeID` of the unit definition to spawn; manufacturing access does not restrict event spawning or later ownership transfers.
- `OwnerFactionInstanceID` **[Required]:** The `InstanceID` of the faction that will own the spawned units.

**Optional options**

- `Count` **[Optional]:** Positive quantity; defaults to `1`.

`SpawnUnits` is valid only inside the `Units` collection of `PlaceUnits`; placement attaches the
resulting units to their destination.

```xml
<SpawnUnits TypeID="SFAL02" OwnerFactionInstanceID="FNALL1" Count="3"/>
```

---

<p align="center"><a href="Index.md">← Event guide</a> · <a href="Bindings.md">Bindings →</a></p>
