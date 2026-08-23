# Bindings and Selectors

Selectors find typed collections of scene nodes. Bindings, conditions, and actions use them wherever
authored XML needs to identify game objects. Direct selectors return every active matching object
when no optional filter is supplied.

## Bindings

Each `Bind` selects exactly one scene node and exposes it under its `As` name for the complete event
evaluation. Bindings are resolved before schedules, so recurring `Until` conditions can consume
them. In a triggered event, bindings follow `Triggers` and may consume the matched result. Zero
results or multiple results fail the evaluation.

**Options**

- `As` — required unique binding name.
- `From` — required child containing exactly one supported selector. The schema currently accepts
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

Selects planets. All filters are optional and combine using AND.

**Options**

- `InstanceID` — select one specific planet.
- `OwnerFactionInstanceID` — require the current owner.
- `SectorType` — require `Core` or `OuterRim`.

```xml
<SelectPlanets OwnerFactionInstanceID="FNALL1" SectorType="Core"/>
```

### SelectPlanetSectors

Selects planet sectors.

**Options**

- `InstanceID` — optional specific sector ID.
- `SectorType` — optional `Core` or `OuterRim` filter.

```xml
<SelectPlanetSectors SectorType="OuterRim"/>
```

## Personnel and mission selectors

### SelectOfficers

Selects officers. Inactive officers are excluded unless explicitly requested.

**Options**

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

**Options**

- `InstanceID` — optional unit ID.
- `PlanetInstanceID` or `PlanetBinding` — optional, mutually exclusive location.
- `OwnerFactionInstanceID` — optional owner filter.

```xml
<SelectSpecialForces PlanetInstanceID="NABOO" OwnerFactionInstanceID="FNALL1"/>
```

### SelectMissions

Selects missions.

**Options**

- `InstanceID` — optional mission ID.
- `PlanetInstanceID` or `PlanetBinding` — optional, mutually exclusive location.
- `OwnerFactionInstanceID` — optional owner filter.

```xml
<SelectMissions PlanetBinding="$planet" OwnerFactionInstanceID="FNEMP1"/>
```

## Fleet and unit selectors

### SelectFleets

Selects fleets.

**Options**

- `InstanceID` — optional fleet ID.
- `PlanetInstanceID` or `PlanetBinding` — optional, mutually exclusive location.
- `OwnerFactionInstanceID` — optional owner filter.

```xml
<SelectFleets PlanetInstanceID="NABOO" OwnerFactionInstanceID="FNALL1"/>
```

### SelectCapitalShips

Selects capital ships.

**Options**

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

**Options**

- `InstanceID` — optional starfighter ID.
- `PlanetInstanceID` or `PlanetBinding` — optional, mutually exclusive location.
- `OwnerFactionInstanceID` — optional owner filter.
- `TypeID` — optional unit-definition ID.
- `ManufacturingStatus` — optional `Building` or `Complete` filter.

```xml
<SelectStarfighters PlanetBinding="$planet"
                    OwnerFactionInstanceID="FNEMP1"
                    ManufacturingStatus="Complete"/>
```

### SelectRegiments

Selects regiments. It supports the same filters as `SelectCapitalShips`.

**Options**

- `InstanceID` — optional regiment ID.
- `PlanetInstanceID` or `PlanetBinding` — optional, mutually exclusive location.
- `OwnerFactionInstanceID` — optional owner filter.
- `TypeID` — optional unit-definition ID.
- `ManufacturingStatus` — optional `Building` or `Complete` filter.

```xml
<SelectRegiments PlanetBinding="$planet" OwnerFactionInstanceID="FNALL1"/>
```

### SelectBuildings

Selects buildings.

**Options**

- `InstanceID` — optional building ID.
- `PlanetInstanceID` or `PlanetBinding` — optional, mutually exclusive location.
- `OwnerFactionInstanceID` — optional owner filter.
- `TypeID` — optional unit-definition ID.
- `ManufacturingStatus` — optional `Building` or `Complete` filter.
- `Category` — optional `Any`, `PlanetaryDefense`, or `ManufacturingFacility` filter.

```xml
<SelectBuildings PlanetBinding="$planet" Category="PlanetaryDefense"/>
```

### SelectManufacturingOrders

Selects queued manufacturing items.

**Options**

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

**Options**

- Child selectors — required; their results form the candidate collection.

```xml
<From>
  <SelectCapitalShips PlanetBinding="$planet"/>
  <SelectStarfighters PlanetBinding="$planet"/>
  <SelectRegiments PlanetBinding="$planet"/>
</From>
```

### SelectRandom

Selects a random subset of the candidates returned by `From`.

**Options**

- `ChancePercent` — optional independent inclusion chance from `0` through `100`; defaults to `100`.
- `Count` — optional exact result count.
- `MinimumCount` and `MaximumCount` — optional nonnegative inclusive result-count range;
  `MinimumCount` defaults to `0`.
- Use either `Count` or the minimum/maximum pair, never both.
- `From` — required selector collection.

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

Returns the scene node or scene-node collection stored by an event binding or reachable through a
trigger-result property path. Its runtime type must be valid for the consumer.

**Options**

- `Binding` — required `$alias` reference.

```xml
<!-- Given <MissionCompleted As="mission"/> in this event's Triggers. -->
<SelectBinding Binding="$mission.Participants"/>
```

### SelectNearestParent

Maps each source to its nearest parent of the requested type. It never returns the source itself.

**Options**

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

**Options**

- Exactly one of `UnitInstanceID` or `UnitBinding` is required.

```xml
<SelectPreviousLocation UnitInstanceID="LUKE_SKYWALKER"/>
```

### SpawnUnits

Creates detached units from an existing unit definition. It is valid only inside the `Units`
collection of `PlaceUnits`; placement attaches the resulting units to their destination.

**Options**

- `TypeID` — required unit-definition ID.
- `OwnerFactionInstanceID` — required owner faction.
- `Count` — optional positive quantity; defaults to `1`.

```xml
<SpawnUnits TypeID="X_WING" OwnerFactionInstanceID="FNALL1" Count="3"/>
```

---

<p align="center"><a href="Index.md">← Event guide</a> · <a href="Schedules.md">Schedules →</a></p>
