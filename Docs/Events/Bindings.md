# Bindings and Selectors

Selectors find typed collections of scene nodes. Bindings, conditions, and actions use them wherever
authored XML needs to identify game objects. Direct selectors return every active matching object
when no optional filter is supplied.

## Bindings

Each `Bind` selects exactly one scene node and exposes it under its `As` name for the complete event
evaluation. Bindings are resolved before schedules, so recurring `Until` conditions can consume
them. In a triggered event, bindings follow `Triggers` and may consume the matched result. Zero
results or multiple results raise a runtime authoring error.

**Options**

- `As` **[Required]:** unique binding name.
- `From` **[Required]:** child containing exactly one supported selector. The schema currently accepts
  direct planet, officer, special-forces, fleet, mission, ship, regiment, building, and
  manufacturing-order selectors, plus `SelectRandom` and `SelectBinding`.

```xml
<Bindings>
  <Bind As="planet">
    <From>
      <SelectRandom Count="1">
        <From>
          <SelectPlanets SectorType="Core"/>
        </From>
      </SelectRandom>
    </From>
  </Bind>
</Bindings>
```

## Planet selectors

### SelectPlanets

Selects active, non-destroyed planets.

**Options**

- `InstanceID` **[Optional]:** select one specific planet.
- `OwnerFactionInstanceID` **[Optional]:** require the current owner.
- `SectorType` **[Optional]:** require `Core` or `OuterRim`.

```xml
<SelectPlanets OwnerFactionInstanceID="FNALL1" SectorType="Core"/>
```

### SelectPlanetSectors

Selects planet sectors.

**Options**

- `InstanceID` **[Optional]:** specific sector ID.
- `SectorType` **[Optional]:** `Core` or `OuterRim` filter.

```xml
<SelectPlanetSectors SectorType="OuterRim"/>
```

## Personnel and mission selectors

### SelectOfficers

Selects officers.

**Options**

- `InstanceID` **[Optional]:** officer ID.
- `OwnerFactionInstanceID` **[Optional]:** owner filter.
- `IsCaptured` **[Optional]:** capture-state filter.
- `IncludeInactive` **[Optional]:** include inactive officers; defaults to `false`.
- `PlanetInstanceID` **[Optional]:** direct location.
- `PlanetBinding` **[Optional]:** bound location. Takes precedence over `PlanetInstanceID`.

```xml
<SelectOfficers PlanetInstanceID="NABOO"
                OwnerFactionInstanceID="FNALL1"
                IsCaptured="false"/>
```

### SelectSpecialForces

Selects special-forces units.

**Options**

- `InstanceID` **[Optional]:** unit ID.
- `PlanetInstanceID` **[Optional]:** direct location.
- `PlanetBinding` **[Optional]:** bound location. Takes precedence over `PlanetInstanceID`.
- `OwnerFactionInstanceID` **[Optional]:** owner filter.

```xml
<SelectSpecialForces PlanetInstanceID="NABOO" OwnerFactionInstanceID="FNALL1"/>
```

### SelectMissions

Selects missions.

**Options**

- `InstanceID` **[Optional]:** mission ID.
- `PlanetInstanceID` **[Optional]:** direct location.
- `PlanetBinding` **[Optional]:** bound location. Takes precedence over `PlanetInstanceID`.
- `OwnerFactionInstanceID` **[Optional]:** owner filter.

```xml
<SelectMissions PlanetBinding="$planet" OwnerFactionInstanceID="FNEMP1"/>
```

## Fleet and unit selectors

### SelectFleets

Selects fleets.

**Options**

- `InstanceID` **[Optional]:** fleet ID.
- `PlanetInstanceID` **[Optional]:** direct location.
- `PlanetBinding` **[Optional]:** bound location. Takes precedence over `PlanetInstanceID`.
- `OwnerFactionInstanceID` **[Optional]:** owner filter.

```xml
<SelectFleets PlanetInstanceID="NABOO" OwnerFactionInstanceID="FNALL1"/>
```

### SelectCapitalShips

Selects capital ships.

**Options**

- `InstanceID` **[Optional]:** ship ID.
- `PlanetInstanceID` **[Optional]:** direct location.
- `PlanetBinding` **[Optional]:** bound location. Takes precedence over `PlanetInstanceID`.
- `OwnerFactionInstanceID` **[Optional]:** owner filter.
- `TypeID` **[Optional]:** unit-definition ID.
- `ManufacturingStatus` **[Optional]:** `Building` or `Complete` filter.

```xml
<SelectCapitalShips TypeID="ALCS008" ManufacturingStatus="Complete"/>
```

### SelectStarfighters

Selects starfighters. It supports the same filters as `SelectCapitalShips`.

**Options**

- `InstanceID` **[Optional]:** starfighter ID.
- `PlanetInstanceID` **[Optional]:** direct location.
- `PlanetBinding` **[Optional]:** bound location. Takes precedence over `PlanetInstanceID`.
- `OwnerFactionInstanceID` **[Optional]:** owner filter.
- `TypeID` **[Optional]:** unit-definition ID.
- `ManufacturingStatus` **[Optional]:** `Building` or `Complete` filter.

```xml
<SelectStarfighters PlanetBinding="$planet"
                    OwnerFactionInstanceID="FNEMP1"
                    ManufacturingStatus="Complete"/>
```

### SelectRegiments

Selects regiments. It supports the same filters as `SelectCapitalShips`.

**Options**

- `InstanceID` **[Optional]:** regiment ID.
- `PlanetInstanceID` **[Optional]:** direct location.
- `PlanetBinding` **[Optional]:** bound location. Takes precedence over `PlanetInstanceID`.
- `OwnerFactionInstanceID` **[Optional]:** owner filter.
- `TypeID` **[Optional]:** unit-definition ID.
- `ManufacturingStatus` **[Optional]:** `Building` or `Complete` filter.

```xml
<SelectRegiments PlanetBinding="$planet" OwnerFactionInstanceID="FNALL1"/>
```

### SelectBuildings

Selects buildings.

**Options**

- `InstanceID` **[Optional]:** building ID.
- `PlanetInstanceID` **[Optional]:** direct location.
- `PlanetBinding` **[Optional]:** bound location. Takes precedence over `PlanetInstanceID`.
- `OwnerFactionInstanceID` **[Optional]:** owner filter.
- `TypeID` **[Optional]:** unit-definition ID.
- `ManufacturingStatus` **[Optional]:** `Building` or `Complete` filter.
- `Category` **[Optional]:** `Any`, `PlanetaryDefense`, or `ManufacturingFacility` filter.

`PlanetaryDefense` includes `Defense` and `Weapon` buildings. `ManufacturingFacility` includes
`Shipyard`, `TrainingFacility`, and `ConstructionFacility` buildings.

```xml
<SelectBuildings PlanetBinding="$planet" Category="PlanetaryDefense"/>
```

### SelectManufacturingOrders

Selects queued manufacturing items.

**Options**

- `PlanetInstanceID` **[Optional]:** direct location.
- `PlanetBinding` **[Optional]:** bound location. Takes precedence over `PlanetInstanceID`.
- `OwnerFactionInstanceID` **[Optional]:** filter on the planet that owns the queue.
- `ManufacturingType` **[Optional]:** `Ship`, `Building`, or `Troop` filter.

```xml
<SelectManufacturingOrders PlanetInstanceID="NABOO" ManufacturingType="Ship"/>
```

## Selector composition

### From

`From` combines the results of every child selector in authored order and removes duplicate scene
nodes. It is used by selectors that transform another selection.

**Options**

- Child selectors **[Required]:** their results form the candidate collection.

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

**Options**

- `ChancePercent` **[Optional]:** independent inclusion chance from `0` through `100`; defaults to `100`.
- `Count` **[Optional]:** exact result count when at least that many candidates exist; otherwise every
  available candidate is returned.
- `MinimumCount` **[Optional]:** nonnegative lower bound applied after independent chance rolls; defaults to `0`.
- `MaximumCount` **[Optional]:** nonnegative upper bound applied after independent chance rolls.
- `From` **[Required]:** selector collection.

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

**Options**

- `From` **[Required]:** ordered selector collection.

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

Returns the scene node or scene-node collection stored by an event binding or reachable through a
trigger-result property path. Its runtime type must be valid for the consumer.

**Options**

- `Binding` **[Required]:** `$alias` reference.

```xml
<!-- Given <MissionCompleted As="mission"/> in this event's Triggers. -->
<RevealToFaction FactionInstanceID="FNALL1">
  <Targets>
    <SelectBinding Binding="$mission.Participants"/>
  </Targets>
</RevealToFaction>
```

### SelectNearestParent

Maps each source to its nearest parent of the requested type. It never returns the source itself.

**Options**

- `Type` **[Required]:** `Galaxy`, `PlanetSector`, `Planet`, `Fleet`, `Mission`, or `CapitalShip` type.
- `From` **[Required]:** source selector collection.

```xml
<SelectNearestParent Type="Planet">
  <From>
    <SelectOfficers InstanceID="LUKE_SKYWALKER"/>
  </From>
</SelectNearestParent>
```

### SelectPreviousLocation

Returns a unit's registered `LastParentInstanceID` when that node still resolves.

**Options**

- `UnitInstanceID` **[Required]:** direct unit source; use either this or `UnitBinding`.
- `UnitBinding` **[Required]:** bound unit source; use either this or `UnitInstanceID`.

```xml
<SelectPreviousLocation UnitInstanceID="LUKE_SKYWALKER"/>
```

### SpawnUnits

Creates detached units from an existing unit definition.

**Options**

- `TypeID` **[Required]:** unit-definition ID; manufacturing access does not restrict event spawning or later ownership transfers.
- `OwnerFactionInstanceID` **[Required]:** owner faction.
- `Count` **[Optional]:** positive quantity; defaults to `1`.

`SpawnUnits` is valid only inside the `Units` collection of `PlaceUnits`; placement attaches the
resulting units to their destination.

```xml
<SpawnUnits TypeID="SFAL02" OwnerFactionInstanceID="FNALL1" Count="3"/>
```

---

<p align="center"><a href="Index.md">← Event guide</a> · <a href="Schedules.md">Schedules →</a></p>
