# Conditions

Conditions inspect game state without changing it. The XML collection is named `Conditionals`, but
this guide consistently calls the individual checks **conditions**. Every top-level condition must
pass before an event executes.

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

Sibling conditions are ANDed. Use `Any` for OR, `Not` for negation, `All` for an explicit AND, and
`Xor` when exactly one nested condition must pass.

## Collections and logic

### Conditionals

`Conditionals` is the XML collection used at the top level of an event. It accepts zero or more
conditions and passes when all of them pass. An empty collection does not block execution.

```xml
<Conditionals>
  <IsOwned PlanetInstanceID="NABOO"/>
</Conditionals>
```

### Until

`Until` accepts the same conditions as `Conditionals`. When all of them pass, the event becomes
permanently exhausted. It is checked before each activation. An empty `Until` does not exhaust the
event.

```xml
<Until>
  <IsCapturedBy OfficerInstanceID="HAN_SOLO"
                CaptorFactionInstanceID="FNEMP1"/>
</Until>
```

### All

`All` passes when every nested condition passes. Its `Conditionals` child must contain one or more
conditions.

```xml
<All>
  <Conditionals>
    <IsOwned PlanetInstanceID="NABOO" FactionInstanceID="FNALL1"/>
    <HasBuildingType Type="Defense"/>
  </Conditionals>
</All>
```

### Any

`Any` passes when at least one nested condition passes. Its `Conditionals` child must contain one or
more conditions.

```xml
<Any>
  <Conditionals>
    <IsCaptured OfficerInstanceID="HAN_SOLO"/>
    <IsKilled OfficerInstanceID="HAN_SOLO"/>
  </Conditionals>
</Any>
```

### Not

`Not` passes when every nested condition fails. Its `Conditionals` child must contain one or more
conditions.

```xml
<Not>
  <Conditionals>
    <IsInTransit UnitInstanceID="HAN_SOLO"/>
    <IsOnMission UnitInstanceID="HAN_SOLO"/>
  </Conditionals>
</Not>
```

### Xor

`Xor` passes when exactly one nested condition passes. Its `Conditionals` child must contain one or
more conditions.

```xml
<Xor>
  <Conditionals>
    <IsCaptured OfficerInstanceID="HAN_SOLO"/>
    <IsInTransit UnitInstanceID="HAN_SOLO"/>
  </Conditionals>
</Xor>
```

## Time and event state

### TickCount

Compares the current campaign tick.

- `Comparison` — required comparison operator.
- `Ticks` — required non-negative integer.

```xml
<TickCount Comparison="GreaterThanOrEqual" Ticks="500"/>
```

### HasEventTriggered

Passes after the referenced event has executed at least once.

- `EventInstanceID` — required event ID.

```xml
<HasEventTriggered EventInstanceID="EVENT_A"/>
```

### IsEventExhausted

Passes when the referenced event can never execute again because it reached its trigger count,
matched `Until`, or completed a one-time schedule.

- `EventInstanceID` — required event ID.

```xml
<IsEventExhausted EventInstanceID="EVENT_B"/>
```

### EvaluateEventVariable

Compares a saved integer event variable.

- `Key` — required variable key.
- `Comparison` — required comparison operator.
- `CompareTo` — required integer value.

```xml
<EvaluateEventVariable Key="naboo.attacks"
                       Comparison="GreaterThanOrEqual"
                       CompareTo="3"/>
```

## Trigger bindings

### EvaluateBinding

Compares a scalar value supplied by a trigger binding. Ordered comparisons require an integer
binding.

- `Binding` — required `$alias` reference.
- `Comparison` — required comparison operator.
- `CompareTo` — required value.

```xml
<EvaluateBinding Binding="$outcome" Comparison="Equal" CompareTo="Succeeded"/>
```

### BindingIncludesUnit

Passes when a bound collection contains the named unit.

- `Binding` — required `$alias` reference to a collection.
- `UnitInstanceID` — required unit ID.

```xml
<BindingIncludesUnit Binding="$participants" UnitInstanceID="LUKE_SKYWALKER"/>
```

## Ownership and support

### IsOwned

Passes when the selected planet has a non-neutral owner. Supply `FactionInstanceID` to require a
specific owner. If neither planet property is supplied, the condition uses the event's `$target`
planet.

- `PlanetInstanceID` — optional explicit planet ID.
- `PlanetBinding` — optional binding containing a planet.
- `FactionInstanceID` — optional required owner.
- `PlanetInstanceID` and `PlanetBinding` are mutually exclusive.

```xml
<IsOwned PlanetInstanceID="NABOO" FactionInstanceID="FNALL1"/>
```

### RollAgainstPopularSupport

Rolls a random percentage against the selected faction's current support on a planet. If neither
planet property is supplied, it uses the event's `$target` planet.

- `FactionInstanceID` — required faction ID.
- `PlanetInstanceID` — optional explicit planet ID.
- `PlanetBinding` — optional binding containing a planet.
- `PlanetInstanceID` and `PlanetBinding` are mutually exclusive.

```xml
<RollAgainstPopularSupport PlanetBinding="$target" FactionInstanceID="FNALL1"/>
```

## Location and scene relationships

### IsAtLocation

Passes when the unit is the location itself or is contained anywhere beneath it.

- `UnitInstanceID` — required child element.
- `LocationInstanceID` — required child element.

```xml
<IsAtLocation>
  <UnitInstanceID>LUKE_SKYWALKER</UnitInstanceID>
  <LocationInstanceID>NABOO</LocationInstanceID>
</IsAtLocation>
```

### ShareParent

Passes when every listed unit has the exact same immediate parent.

- `Units` — required child containing at least two `Unit` elements.
- `UnitInstanceID` — required attribute on each `Unit`.

```xml
<ShareParent>
  <Units>
    <Unit UnitInstanceID="LUKE_SKYWALKER"/>
    <Unit UnitInstanceID="HAN_SOLO"/>
  </Units>
</ShareParent>
```

### ShareAncestor

Passes when every listed unit shares the same nearest ancestor of the requested scene type.

- `Type` — required: `Galaxy`, `PlanetSector`, `Planet`, `Fleet`, `Mission`, or `CapitalShip`.
- `Units` — required child containing at least two `Unit` elements.
- `UnitInstanceID` — required attribute on each `Unit`.

```xml
<ShareAncestor Type="Planet">
  <Units>
    <Unit UnitInstanceID="LUKE_SKYWALKER"/>
    <Unit UnitInstanceID="DARTH_VADER"/>
  </Units>
</ShareAncestor>
```

### AreOnOpposingFactions

Passes when exactly two supplied units exist and have different owners.

- `UnitInstanceIDs` — required child containing exactly two `String` unit IDs.

```xml
<AreOnOpposingFactions>
  <UnitInstanceIDs>
    <String>LUKE_SKYWALKER</String>
    <String>DARTH_VADER</String>
  </UnitInstanceIDs>
</AreOnOpposingFactions>
```

## Unit and officer state

### IsOnMission

Passes when the unit has a mission parent.

- `UnitInstanceID` — required unit ID.

```xml
<IsOnMission UnitInstanceID="HAN_SOLO"/>
```

### IsInTransit

Passes when the unit currently has movement state.

- `UnitInstanceID` — required unit ID.

```xml
<IsInTransit UnitInstanceID="HAN_SOLO"/>
```

### IsCaptured

Passes when the officer is captured.

- `OfficerInstanceID` — required officer ID.

```xml
<IsCaptured OfficerInstanceID="HAN_SOLO"/>
```

### IsCapturedBy

Passes when the officer is captured by the specified faction.

- `OfficerInstanceID` — required officer ID.
- `CaptorFactionInstanceID` — required capturing faction ID.

```xml
<IsCapturedBy OfficerInstanceID="HAN_SOLO" CaptorFactionInstanceID="FNEMP1"/>
```

### IsKilled

Passes when the officer is killed.

- `OfficerInstanceID` — required officer ID.

```xml
<IsKilled OfficerInstanceID="HAN_SOLO"/>
```

### IsInjured

Passes when the officer is injured.

- `OfficerInstanceID` — required officer ID.

```xml
<IsInjured OfficerInstanceID="HAN_SOLO"/>
```

### IsForceEligible

Passes when the officer is eligible to use and develop Force ability.

- `OfficerInstanceID` — required officer ID.

```xml
<IsForceEligible OfficerInstanceID="LUKE_SKYWALKER"/>
```

## Ratings, Force, and planet state

### HasForceRank

Compares an officer's effective Force rank against a named rank.

- `OfficerInstanceID` — required officer ID.
- `Comparison` — required comparison operator.
- `Rank` — required Force rank.

```xml
<HasForceRank OfficerInstanceID="LUKE_SKYWALKER"
              Comparison="GreaterThanOrEqual"
              Rank="ForceKnight"/>
```

### CompareOfficerRating

Compares one effective officer rating against an integer.

- `OfficerInstanceID` — required officer ID.
- `Rating` — required officer rating.
- `Comparison` — required comparison operator.
- `Value` — required integer.

```xml
<CompareOfficerRating OfficerInstanceID="HAN_SOLO"
                      Rating="Combat"
                      Comparison="GreaterThanOrEqual"
                      Value="80"/>
```

### CompareOfficerForce

Compares an officer's effective Force value against an integer.

- `OfficerInstanceID` — required officer ID.
- `Comparison` — required comparison operator.
- `Value` — required integer.

```xml
<CompareOfficerForce OfficerInstanceID="LUKE_SKYWALKER"
                     Comparison="GreaterThanOrEqual"
                     Value="100"/>
```

### ComparePlanetStat

Compares one planet stat against an integer. If neither planet property is supplied, it uses the
event's `$target` planet.

- `Stat` — required planet stat.
- `Comparison` — required comparison operator.
- `Value` — required integer.
- `PlanetInstanceID` — optional explicit planet ID.
- `PlanetBinding` — optional binding containing a planet.
- `PlanetInstanceID` and `PlanetBinding` are mutually exclusive.

```xml
<ComparePlanetStat PlanetInstanceID="NABOO"
                   Stat="EnergyCapacity"
                   Comparison="GreaterThan"
                   Value="0"/>
```

### HasBuildingType

Passes when the event's `$target` planet contains a completed building of the requested type.

- `Type` — required building type.

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

## Allowed values

Comparisons are `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, and
`LessThanOrEqual`. Officer ratings are `Diplomacy`, `Espionage`, `Combat`, `Leadership`,
`ShipResearch`, `TroopResearch`, and `FacilityResearch`. Planet stats are `RawResourceNodes` and
`EnergyCapacity`. Force ranks are `None`, `Novice`, `Trainee`, `ForceStudent`, `ForceKnight`, and
`ForceMaster`. Building types are `Mine`, `Refinery`, `Shipyard`, `TrainingFacility`,
`ConstructionFacility`, `Defense`, `Weapon`, and `Headquarters`.

---

<p align="center"><a href="Triggers.md">← Triggers</a> · <a href="Index.md">Event guide</a> · <a href="Targets.md">Targets →</a></p>
