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

| Selector | Filters or behavior |
| --- | --- |
| `SelectPlanets` | `InstanceID`, `OwnerFactionInstanceID`, `SectorType` |
| `SelectPlanetSectors` | `InstanceID`, `SectorType` |
| `SelectOfficers` | ID, planet, owner, capture state, and whether inactive officers are included |
| `SelectSpecialForces`, `SelectFleets`, `SelectMissions` | ID, planet, and owner |
| `SelectCapitalShips`, `SelectStarfighters`, `SelectRegiments` | ID, planet, owner, `TypeID`, and `ManufacturingStatus` |
| `SelectBuildings` | The same filters plus `Category` |
| `SelectManufacturingOrders` | Planet, owner, and `ManufacturingType` |
| `SelectRandom` | Samples its combined candidates by chance and count. |
| `SelectFirst` | Returns the first valid destination candidate. |
| `SelectBinding` | Returns the object or collection in a binding. |
| `SelectNearestParent` | Maps candidates to their nearest parent of `Type`. The selected node itself is not considered. |
| `SelectPreviousLocation` | Returns a unit's recorded previous scene location. |
| `SpawnUnits` | Creates `Count` detached units from a catalog `TypeID` for immediate use by `PlaceUnits`. |

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

[← Conditions](Conditions.md) · [Event guide](README.md) · [Actions →](Actions.md)
