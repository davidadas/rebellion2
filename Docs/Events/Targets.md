# Targets and Selectors

Selectors find typed collections of scene nodes. Targets, conditions, and actions use them wherever
authored XML needs to identify game objects. Direct selectors return every active matching object
when no optional filter is supplied.

## Target

`Target` selects exactly one scene node and exposes it to the event as `$target`. Zero results or
multiple results fail the activation.

- `From` — required child containing exactly one selector.

```xml
<Target>
  <From>
    <SelectRandom Count="1">
      <From>
        <SelectPlanets SectorType="Core"/>
      </From>
    </SelectRandom>
  </From>
</Target>
```

## Planet selectors

### SelectPlanets

Selects planets. All filters are optional and combine using AND.

- `InstanceID` — select one specific planet.
- `OwnerFactionInstanceID` — require the current owner.
- `SectorType` — require `Core` or `OuterRim`.

```xml
<SelectPlanets OwnerFactionInstanceID="FNALL1" SectorType="Core"/>
```

### SelectPlanetSectors

Selects planet sectors.

- `InstanceID` — optional specific sector ID.
- `SectorType` — optional `Core` or `OuterRim` filter.

```xml
<SelectPlanetSectors SectorType="OuterRim"/>
```

## Personnel and mission selectors

### SelectOfficers

Selects officers. Inactive officers are excluded unless explicitly requested.

- `InstanceID` — optional officer ID.
- `PlanetInstanceID` — optional explicit planet location.
- `PlanetBinding` — optional binding containing a planet.
- `OwnerFactionInstanceID` — optional owner filter.
- `IsCaptured` — optional capture-state filter.
- `IncludeInactive` — optionally include inactive officers; defaults to `false`.
- `PlanetInstanceID` and `PlanetBinding` are mutually exclusive.

```xml
<SelectOfficers PlanetInstanceID="NABOO"
                OwnerFactionInstanceID="FNALL1"
                IsCaptured="false"/>
```

### SelectSpecialForces

Selects special-forces units.

- `InstanceID` — optional unit ID.
- `PlanetInstanceID` or `PlanetBinding` — optional, mutually exclusive location.
- `OwnerFactionInstanceID` — optional owner filter.

```xml
<SelectSpecialForces PlanetInstanceID="NABOO" OwnerFactionInstanceID="FNALL1"/>
```

### SelectMissions

Selects missions.

- `InstanceID` — optional mission ID.
- `PlanetInstanceID` or `PlanetBinding` — optional, mutually exclusive location.
- `OwnerFactionInstanceID` — optional owner filter.

```xml
<SelectMissions PlanetBinding="$target" OwnerFactionInstanceID="FNEMP1"/>
```

## Fleet and unit selectors

### SelectFleets

Selects fleets.

- `InstanceID` — optional fleet ID.
- `PlanetInstanceID` or `PlanetBinding` — optional, mutually exclusive location.
- `OwnerFactionInstanceID` — optional owner filter.

```xml
<SelectFleets PlanetInstanceID="NABOO" OwnerFactionInstanceID="FNALL1"/>
```

### SelectCapitalShips

Selects capital ships.

- `InstanceID` — optional ship ID.
- `PlanetInstanceID` or `PlanetBinding` — optional, mutually exclusive location.
- `OwnerFactionInstanceID` — optional owner filter.
- `TypeID` — optional unit-definition ID.
- `ManufacturingStatus` — optional `Building` or `Complete` filter.

```xml
<SelectCapitalShips TypeID="MON_CALAMARI_CRUISER" ManufacturingStatus="Complete"/>
```

### SelectStarfighters

Selects starfighters. It supports the same filters as `SelectCapitalShips`.

```xml
<SelectStarfighters PlanetBinding="$target"
                    OwnerFactionInstanceID="FNEMP1"
                    ManufacturingStatus="Complete"/>
```

### SelectRegiments

Selects regiments. It supports the same filters as `SelectCapitalShips`.

```xml
<SelectRegiments PlanetBinding="$target" OwnerFactionInstanceID="FNALL1"/>
```

### SelectBuildings

Selects buildings.

- `InstanceID` — optional building ID.
- `PlanetInstanceID` or `PlanetBinding` — optional, mutually exclusive location.
- `OwnerFactionInstanceID` — optional owner filter.
- `TypeID` — optional unit-definition ID.
- `ManufacturingStatus` — optional `Building` or `Complete` filter.
- `Category` — optional `Any`, `PlanetaryDefense`, or `ManufacturingFacility` filter.

```xml
<SelectBuildings PlanetBinding="$target" Category="PlanetaryDefense"/>
```

### SelectManufacturingOrders

Selects queued manufacturing items.

- `PlanetInstanceID` or `PlanetBinding` — optional, mutually exclusive location.
- `OwnerFactionInstanceID` — optional owner filter.
- `ManufacturingType` — optional `Ship`, `Building`, or `Troop` filter.

```xml
<SelectManufacturingOrders PlanetInstanceID="NABOO" ManufacturingType="Ship"/>
```

## Selector composition

### From

`From` combines the results of every child selector in authored order and removes duplicate scene
nodes. It is used by selectors that transform another selection.

```xml
<From>
  <SelectCapitalShips PlanetBinding="$target"/>
  <SelectStarfighters PlanetBinding="$target"/>
  <SelectRegiments PlanetBinding="$target"/>
</From>
```

### SelectRandom

Selects a random subset of the candidates returned by `From`.

- `ChancePercent` — optional independent inclusion chance; defaults to `100`.
- `Count` — optional exact result count.
- `MinimumCount` and `MaximumCount` — optional inclusive result-count range.
- Use either `Count` or the minimum/maximum pair, never both.
- `From` — required selector collection.

```xml
<SelectRandom ChancePercent="25" MinimumCount="1" MaximumCount="3">
  <From>
    <SelectBuildings PlanetBinding="$target" Category="PlanetaryDefense"/>
    <SelectRegiments PlanetBinding="$target"/>
  </From>
</SelectRandom>
```

### SelectFirst

Returns the first distinct candidate from `From`. When used as a destination, placement or movement
checks candidates in authored order and uses the first one that accepts the units.

- `From` — required ordered selector collection.

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

Returns the scene node or scene-node collection stored by a trigger binding. Its runtime type must
be valid for the consumer.

- `Binding` — required `$alias` reference.

```xml
<SelectBinding Binding="$participants"/>
```

### SelectNearestParent

Maps each source to its nearest parent of the requested type. It never returns the source itself.

- `Type` — required scene-node type.
- `From` — required source selector collection.

```xml
<SelectNearestParent Type="Planet">
  <From>
    <SelectOfficers InstanceID="LUKE_SKYWALKER"/>
  </From>
</SelectNearestParent>
```

### SelectPreviousLocation

Returns a unit's registered `LastParentInstanceID` when that node still resolves.

- Exactly one of `UnitInstanceID` or `UnitBinding` is required.

```xml
<SelectPreviousLocation UnitInstanceID="LUKE_SKYWALKER"/>
```

### SpawnUnits

Creates detached units from an existing unit definition. It is valid only inside the `Units`
collection of `PlaceUnits`; placement attaches the resulting units to their destination.

- `TypeID` — required unit-definition ID.
- `OwnerFactionInstanceID` — required owner faction.
- `Count` — optional positive quantity; defaults to `1`.

```xml
<SpawnUnits TypeID="X_WING" OwnerFactionInstanceID="FNALL1" Count="3"/>
```

---

<p align="center"><a href="Conditions.md">← Conditions</a> · <a href="Index.md">Event guide</a> · <a href="Actions.md">Actions →</a></p>
