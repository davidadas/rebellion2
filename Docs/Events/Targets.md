# Targets and Selectors

Selectors return typed collections of scene nodes. They are reused by targets, conditions, and actions wherever authored XML needs to identify game objects. A top-level `Target` requires exactly one result and exposes it to the rest of the event as `$target`:

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

## Selector reference

Direct-selector filters are optional unless marked required. Omitting every filter selects every active object of that direct selector's type.

| Element | Attributes | Child elements | Result |
| --- | --- | --- | --- |
| `Target` | None | Required `From` containing exactly one selector | Exactly one scene node, exposed as `$target`; zero or multiple results fail. |
| `SelectPlanets` | `InstanceID`, `OwnerFactionInstanceID`, `SectorType` | None | Matching planets. |
| `SelectPlanetSectors` | `InstanceID`, `SectorType` | None | Matching planet sectors. |
| `SelectOfficers` | `InstanceID`, `PlanetInstanceID` or `PlanetBinding`, `OwnerFactionInstanceID`, `IsCaptured`, `IncludeInactive` | None | Matching officers. `IncludeInactive` defaults to `false`. |
| `SelectSpecialForces` | `InstanceID`, `PlanetInstanceID` or `PlanetBinding`, `OwnerFactionInstanceID` | None | Matching special forces. |
| `SelectFleets` | Same location and ownership filters | None | Matching fleets. |
| `SelectMissions` | Same location and ownership filters | None | Matching missions. |
| `SelectCapitalShips` | Location and ownership filters plus `TypeID`, `ManufacturingStatus` | None | Matching capital ships. |
| `SelectStarfighters` | Location and ownership filters plus `TypeID`, `ManufacturingStatus` | None | Matching starfighters. |
| `SelectRegiments` | Location and ownership filters plus `TypeID`, `ManufacturingStatus` | None | Matching regiments. |
| `SelectBuildings` | Location and ownership filters plus `TypeID`, `ManufacturingStatus`, `Category` | None | Matching buildings. |
| `SelectManufacturingOrders` | `PlanetInstanceID` or `PlanetBinding`, `OwnerFactionInstanceID`, `ManufacturingType` | None | Matching queued manufacturing items. |
| `SelectRandom` | `ChancePercent` (default `100`), either `Count` or `MinimumCount`/`MaximumCount` | Required `From` selector collection | A random subset of the combined candidates. |
| `SelectFirst` | None | Required ordered `From` selector collection | The first distinct candidate. In a destination, placement checks candidates in order. |
| `SelectBinding` | `Binding` — required | None | The scene node or scene-node collection stored in the binding. |
| `SelectNearestParent` | `Type` — required | Required `From` selector collection | Each candidate's nearest parent of `Type`; never the candidate itself. |
| `SelectPreviousLocation` | Exactly one of `UnitInstanceID` or `UnitBinding` | None | The unit's registered `LastParentInstanceID`, when it still resolves. |
| `SpawnUnits` | `TypeID`, `OwnerFactionInstanceID` — required; `Count` — optional, default `1` | None | Newly created detached units. Valid only inside `PlaceUnits/Units`. |

## Combining selectors

A selector collection combines the results of every child selector. This lets one action operate on several unit categories without introducing an untyped catch-all selector:

```xml
<Units>
  <SelectCapitalShips PlanetBinding="$target" OwnerFactionInstanceID="FNEMP1"/>
  <SelectStarfighters PlanetBinding="$target" OwnerFactionInstanceID="FNEMP1"/>
  <SelectRegiments PlanetBinding="$target" OwnerFactionInstanceID="FNEMP1"/>
</Units>
```

Selectors remove duplicate scene nodes before actions are applied.

## Selecting from a binding

Use `SelectBinding` when a trigger already supplied the object or collection:

```xml
<Units>
  <SelectBinding Binding="$participants"/>
</Units>
```

The binding's runtime type must be valid for the action consuming the selector.

## Choosing a destination

`SelectFirst` is explicit fallback behavior: it returns candidates in selector order and lets placement or movement use the first destination that accepts the units.

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

`SelectNearestParent` maps each source to its nearest parent of the requested scene type. It does not return the source itself:

```xml
<SelectNearestParent Type="Planet">
  <From>
    <SelectOfficers InstanceID="LUKE_SKYWALKER"/>
  </From>
</SelectNearestParent>
```

`SelectPreviousLocation` is intended for returning a unit whose location changed or whose active state was temporarily disabled:

```xml
<SelectPreviousLocation UnitInstanceID="LUKE_SKYWALKER"/>
```

Planet location filters use `PlanetInstanceID` or `PlanetBinding`. Sector types are `Core` and
`OuterRim`. Manufacturing statuses are `Building` and `Complete`. Manufacturing types are `Ship`,
`Building`, and `Troop`. Building categories are `Any`, `PlanetaryDefense`, and
`ManufacturingFacility`.

`SelectRandom` accepts `ChancePercent`, exact `Count`, or `MinimumCount` and `MaximumCount`:

```xml
<SelectRandom ChancePercent="25" MinimumCount="1" MaximumCount="3">
  <From>
    <SelectBuildings PlanetBinding="$target" Category="PlanetaryDefense"/>
    <SelectRegiments PlanetBinding="$target"/>
  </From>
</SelectRandom>
```

---

<p align="center"><a href="Conditions.md">← Conditions</a> · <a href="Index.md">Event guide</a> · <a href="Actions.md">Actions →</a></p>
