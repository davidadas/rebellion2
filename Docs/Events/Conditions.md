# Conditions

Conditions inspect game state without changing it. All top-level entries must pass before an event executes.


Sibling conditions are ANDed. Use `Any` for OR, `Not` for negation, `All` for an explicit AND, and
`Xor` when exactly one nested condition must pass:

```xml
<Conditionals>
  <IsOwned PlanetInstanceID="NABOO" FactionInstanceID="FNALL1"/>
  <Not>
    <Conditionals>
      <Any>
        <Conditionals>
          <IsCaptured OfficerInstanceID="LEIA_ORGANA"/>
          <IsOnMission UnitInstanceID="LEIA_ORGANA"/>
          <IsInTransit UnitInstanceID="LEIA_ORGANA"/>
        </Conditionals>
      </Any>
    </Conditionals>
  </Not>
</Conditionals>
```

## Condition reference

“Required” below means the XML must provide the property. Planet-aware conditions that allow neither `PlanetInstanceID` nor `PlanetBinding` use the event's `$target` planet.

| Element | Attributes | Child elements or context | Passes when |
| --- | --- | --- | --- |
| `Conditionals` | None | Zero or more conditions | Every top-level condition passes. An empty collection does not block execution. |
| `Until` | None | Zero or more conditions | Every authored stop condition passes; an empty `Until` does not exhaust the event. |
| `All` | None | `Conditionals` containing one or more conditions | Every nested condition passes. |
| `Any` | None | `Conditionals` containing one or more conditions | At least one nested condition passes. |
| `Not` | None | `Conditionals` containing one or more conditions | Every nested condition fails. |
| `Xor` | None | `Conditionals` containing one or more conditions | Exactly one nested condition passes. |
| `TickCount` | `Comparison`, `Ticks` — required | None | The current campaign tick satisfies the comparison. |
| `HasEventTriggered` | `EventInstanceID` — required | None | The referenced event executed at least once. |
| `IsEventExhausted` | `EventInstanceID` — required | None | The referenced event can no longer execute. |
| `EvaluateEventVariable` | `Key`, `Comparison`, `CompareTo` — required | None | The saved integer satisfies the comparison. |
| `EvaluateBinding` | `Binding`, `Comparison`, `CompareTo` — required | None | The bound scalar satisfies the comparison. Ordered comparisons require an integer binding. |
| `BindingIncludesUnit` | `Binding`, `UnitInstanceID` — required | None | The bound collection contains that unit. |
| `IsOwned` | `PlanetInstanceID` or `PlanetBinding`; optional `FactionInstanceID` | Falls back to `$target` | The planet has a non-neutral owner, optionally the specified faction. |
| `RollAgainstPopularSupport` | `FactionInstanceID` — required; `PlanetInstanceID` or `PlanetBinding` optional | Falls back to `$target` | A random percentage roll is below that faction's support. |
| `IsAtLocation` | None | Required `UnitInstanceID` and `LocationInstanceID` elements | The unit is the location or is contained anywhere beneath it. |
| `ShareParent` | None | `Units` containing at least two `Unit UnitInstanceID="…"` elements | Every unit has the exact same immediate parent. |
| `ShareAncestor` | `Type` — required | Same `Units` collection as `ShareParent` | Every unit shares the same nearest ancestor of `Type`. |
| `AreOnOpposingFactions` | None | `UnitInstanceIDs` containing exactly two `String` elements | Both units exist and have different owners. |
| `IsOnMission` | `UnitInstanceID` — required | None | The unit has a mission parent. |
| `IsInTransit` | `UnitInstanceID` — required | None | The unit currently has movement state. |
| `IsCaptured`, `IsKilled`, `IsInjured`, `IsForceEligible` | `OfficerInstanceID` — required | None | The named officer has the corresponding state. |
| `IsCapturedBy` | `OfficerInstanceID`, `CaptorFactionInstanceID` — required | None | The officer is captured by that faction. |
| `HasForceRank` | `OfficerInstanceID`, `Comparison`, `Rank` — required | None | The officer's effective Force rank satisfies the configured rank threshold. |
| `CompareOfficerRating` | `OfficerInstanceID`, `Rating`, `Comparison`, `Value` — required | None | The effective rating satisfies the numeric comparison. |
| `CompareOfficerForce` | `OfficerInstanceID`, `Comparison`, `Value` — required | None | The effective Force value satisfies the numeric comparison. |
| `ComparePlanetStat` | `Stat`, `Comparison`, `Value` — required; `PlanetInstanceID` or `PlanetBinding` optional | Falls back to `$target` | The selected planet stat satisfies the comparison. |
| `HasBuildingType` | `Type` — required | Requires a planet `$target` | The target contains a completed building of that type. |

## Common condition shapes

### Time and event state

```xml
<TickCount Comparison="GreaterThanOrEqual" Ticks="500"/>
<HasEventTriggered EventInstanceID="EVENT_A"/>
<IsEventExhausted EventInstanceID="EVENT_B"/>
```

`HasEventTriggered` passes after the referenced event has executed at least once. `IsEventExhausted` passes when that event can never execute again because it reached its trigger count, matched `Until`, or completed a one-time schedule.

### Variables and trigger bindings

```xml
<EvaluateEventVariable Key="naboo.attacks"
                       Comparison="GreaterThanOrEqual"
                       CompareTo="3"/>

<EvaluateBinding Binding="$outcome" Comparison="Equal" CompareTo="Succeeded"/>
<BindingIncludesUnit Binding="$participants" UnitInstanceID="LUKE_SKYWALKER"/>
```

Use event variables for authored state that must persist independently of any one event. Use bindings for data supplied by the gameplay result that activated the current event.

### Ownership and support

```xml
<IsOwned PlanetInstanceID="NABOO" FactionInstanceID="FNALL1"/>
<RollAgainstPopularSupport PlanetBinding="$target"
                           FactionInstanceID="FNALL1"/>
```

`IsOwned` can omit the faction when any non-neutral ownership is sufficient. `RollAgainstPopularSupport` performs a probability check using the selected faction's current support on the planet.

### Unit activity and officer state

```xml
<IsOnMission UnitInstanceID="HAN_SOLO"/>
<IsInTransit UnitInstanceID="HAN_SOLO"/>
<IsCaptured OfficerInstanceID="HAN_SOLO"/>
<IsCapturedBy OfficerInstanceID="HAN_SOLO"
              CaptorFactionInstanceID="FNEMP1"/>
<IsKilled OfficerInstanceID="HAN_SOLO"/>
<IsInjured OfficerInstanceID="HAN_SOLO"/>
```

These checks are deliberately separate because capture, transit, mission assignment, injury, and death are independent states.

### Ratings, Force, and planets

```xml
<CompareOfficerRating OfficerInstanceID="HAN_SOLO"
                      Rating="Combat"
                      Comparison="GreaterThanOrEqual"
                      Value="80"/>
<IsForceEligible OfficerInstanceID="LUKE_SKYWALKER"/>
<HasForceRank OfficerInstanceID="LUKE_SKYWALKER"
              Comparison="GreaterThanOrEqual"
              Rank="ForceKnight"/>
<ComparePlanetStat PlanetInstanceID="NABOO"
                   Stat="EnergyCapacity"
                   Comparison="GreaterThan"
                   Value="0"/>
```

`HasBuildingType` reads the event's planet target:

```xml
<Target>
  <From>
    <SelectPlanets InstanceID="NABOO"/>
  </From>
</Target>
<Conditionals>
  <HasBuildingType Type="Defense"/>
</Conditionals>
```

## Values and comparisons

Comparisons are `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, and
`LessThanOrEqual`. Officer ratings are `Diplomacy`, `Espionage`, `Combat`, `Leadership`,
`ShipResearch`, `TroopResearch`, and `FacilityResearch`. Planet stats are `RawResourceNodes` and
`EnergyCapacity`. Force ranks are `None`, `Novice`, `Trainee`, `ForceStudent`, `ForceKnight`, and
`ForceMaster`.

## Scene relationships

`ShareParent` checks the exact immediate parent. `ShareAncestor` checks a shared nearest `Galaxy`,
`PlanetSector`, `Planet`, `Fleet`, `Mission`, or `CapitalShip` ancestor:

```xml
<ShareAncestor Type="Planet">
  <Units>
    <Unit UnitInstanceID="LUKE_SKYWALKER"/>
    <Unit UnitInstanceID="DARTH_VADER"/>
  </Units>
</ShareAncestor>
```

## Stopping an event with Until

`Until` uses the same condition language, but permanently exhausts the event once every listed condition passes. It is checked before each activation.

```xml
<Until>
  <IsCapturedBy OfficerInstanceID="HAN_SOLO" CaptorFactionInstanceID="FNEMP1"/>
</Until>
```

Building types are `Mine`, `Refinery`, `Shipyard`, `TrainingFacility`, `ConstructionFacility`,
`Defense`, `Weapon`, and `Headquarters`.

---

<p align="center"><a href="Triggers.md">← Triggers</a> · <a href="Index.md">Event guide</a> · <a href="Targets.md">Targets →</a></p>
