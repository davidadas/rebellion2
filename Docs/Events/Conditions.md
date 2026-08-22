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

| Condition | What it checks |
| --- | --- |
| `All`, `Any`, `Not`, `Xor` | Nested boolean logic. |
| `TickCount` | Current tick using `Comparison` and `Ticks`. |
| `HasEventTriggered`, `IsEventExhausted` | Whether another event has run or can ever run again. |
| `EvaluateEventVariable` | A saved integer using `Key`, `Comparison`, and `CompareTo`. |
| `EvaluateBinding`, `BindingIncludesUnit` | A scalar binding or collection binding. |
| `IsOwned`, `RollAgainstPopularSupport` | Planet ownership or a support roll for a faction. |
| `IsAtLocation`, `ShareParent`, `ShareAncestor` | Unit location and scene relationships. |
| `AreOnOpposingFactions` | Whether listed units belong to opposing factions. |
| `IsOnMission`, `IsInTransit` | Unit activity. |
| `IsCaptured`, `IsCapturedBy`, `IsKilled`, `IsInjured` | Officer state. |
| `IsForceEligible`, `HasForceRank` | Force eligibility or configured rank label. |
| `CompareOfficerRating`, `CompareOfficerForce` | Numeric officer values. |
| `ComparePlanetStat`, `HasBuildingType` | Planet stats and facilities. |

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

[← Triggers](Triggers.md) · [Event guide](README.md) · [Targets →](Targets.md)
