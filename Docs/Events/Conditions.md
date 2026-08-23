# Conditions

Conditions inspect game state without changing it. The XML collection is named `Conditionals`, but
this guide consistently calls the individual checks **conditions**. Every top-level condition must
pass before an event activates.

```xml
<Conditionals>
  <IsOwned PlanetInstanceID="NABOO" FactionInstanceID="FNALL1"/>
  <Not>
    <Any>
      <IsCaptured OfficerInstanceID="LEIA_ORGANA"/>
      <IsOnMission UnitInstanceID="LEIA_ORGANA"/>
      <IsInTransit UnitInstanceID="LEIA_ORGANA"/>
    </Any>
  </Not>
</Conditionals>
```

Sibling conditions are ANDed. Use `Any` for OR, `Not` for negation, `All` for an explicit AND, and
`Xor` when exactly one nested condition must pass.

## Collections and logic

### Conditionals

`Conditionals` is the XML collection used at the top level of an event. It accepts zero or more
conditions and passes when all of them pass. An empty collection does not block execution.

**Options**

- Child conditions — optional; siblings are combined using AND.

```xml
<Conditionals>
  <IsOwned PlanetInstanceID="NABOO"/>
</Conditionals>
```

### All

`All` passes when every nested condition passes.

**Options**

- Child conditions — one or more; every child must pass.

```xml
<All>
  <IsOwned PlanetInstanceID="NABOO" FactionInstanceID="FNALL1"/>
  <HasBuildingType Type="Defense"/>
</All>
```

### Any

`Any` passes when at least one nested condition passes.

**Options**

- Child conditions — one or more; at least one child must pass.

```xml
<Any>
  <IsCaptured OfficerInstanceID="HAN_SOLO"/>
  <IsKilled OfficerInstanceID="HAN_SOLO"/>
</Any>
```

### Not

`Not` passes when every nested condition fails.

**Options**

- Child conditions — one or more; every child must fail.

```xml
<Not>
  <IsInTransit UnitInstanceID="HAN_SOLO"/>
  <IsOnMission UnitInstanceID="HAN_SOLO"/>
</Not>
```

### Xor

`Xor` passes when exactly one nested condition passes.

**Options**

- Child conditions — one or more; exactly one child must pass.

```xml
<Xor>
  <IsCaptured OfficerInstanceID="HAN_SOLO"/>
  <IsInTransit UnitInstanceID="HAN_SOLO"/>
</Xor>
```

## Time and event state

### TickCount

Compares the current campaign tick.

**Options**

- `Comparison` — required comparison operator.
- `Ticks` — required non-negative integer.

```xml
<TickCount Comparison="GreaterThanOrEqual" Ticks="500"/>
```

### HasEventActivated

Passes after the referenced event has activated at least once.

**Options**

- `EventInstanceID` — required event ID.

```xml
<HasEventActivated EventInstanceID="EVENT_A"/>
```

### IsEventComplete

Passes when the referenced event can no longer activate because it reached `MaximumActivations`,
matched a recurring schedule's `Until`, or completed a one-shot schedule.

**Options**

- `EventInstanceID` — required event ID.

```xml
<IsEventComplete EventInstanceID="EVENT_B"/>
```

### EvaluateEventVariable

Compares a saved integer event variable.

**Options**

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

**Options**

- `Binding` — required `$alias` reference.
- `Comparison` — required comparison operator.
- `CompareTo` — required value.

```xml
<!-- Given <MissionCompleted As="mission"/> in this event's Triggers. -->
<EvaluateBinding Binding="$mission.Outcome" Comparison="Equal" CompareTo="Success"/>
```

### BindingIncludesUnit

Passes when a bound collection contains the named unit.

**Options**

- `Binding` — required `$alias` reference to a collection.
- `UnitInstanceID` — required unit ID.

```xml
<!-- Given <MissionCompleted As="mission"/> in this event's Triggers. -->
<BindingIncludesUnit Binding="$mission.Participants" UnitInstanceID="LUKE_SKYWALKER"/>
```

## Ownership and support

### IsOwned

Passes when the selected planet has a non-neutral owner. Supply `FactionInstanceID` to require a
specific owner.

**Options**

- `PlanetInstanceID` — explicit planet ID.
- `PlanetBinding` — binding containing a planet.
- `FactionInstanceID` — optional required owner.
- `PlanetInstanceID` or `PlanetBinding` identifies the planet. `PlanetBinding` takes precedence when
  both are present; the condition fails when neither resolves.

```xml
<IsOwned PlanetInstanceID="NABOO" FactionInstanceID="FNALL1"/>
```

### RollAgainstPopularSupport

Rolls a random percentage against the selected faction's current support on a planet.

**Options**

- `FactionInstanceID` — required faction ID.
- `PlanetInstanceID` — explicit planet ID.
- `PlanetBinding` — binding containing a planet.
- `PlanetInstanceID` or `PlanetBinding` identifies the planet. `PlanetBinding` takes precedence when
  both are present; the condition fails when neither resolves.

```xml
<RollAgainstPopularSupport PlanetBinding="$planet" FactionInstanceID="FNALL1"/>
```

## Location and scene relationships

### IsAtLocation

Passes when the unit is the location itself or is contained anywhere beneath it.

**Options**

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

**Options**

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

**Options**

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

**Options**

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

**Options**

- `UnitInstanceID` — required unit ID.

```xml
<IsOnMission UnitInstanceID="HAN_SOLO"/>
```

### IsInTransit

Passes when the unit currently has movement state.

**Options**

- `UnitInstanceID` — required unit ID.

```xml
<IsInTransit UnitInstanceID="HAN_SOLO"/>
```

### IsCaptured

Passes when the officer is captured. Add `CaptorFactionInstanceID` when the capturing faction also
matters.

**Options**

- `OfficerInstanceID` — required officer ID.
- `CaptorFactionInstanceID` — optional capturing-faction filter.

```xml
<IsCaptured OfficerInstanceID="HAN_SOLO"/>
<IsCaptured OfficerInstanceID="HAN_SOLO" CaptorFactionInstanceID="FNEMP1"/>
```

### IsKilled

Passes when the officer is killed.

**Options**

- `OfficerInstanceID` — required officer ID.

```xml
<IsKilled OfficerInstanceID="HAN_SOLO"/>
```

### IsInjured

Passes when the officer is injured.

**Options**

- `OfficerInstanceID` — required officer ID.

```xml
<IsInjured OfficerInstanceID="HAN_SOLO"/>
```

### IsForceEligible

Passes when the officer is eligible to use and develop Force ability.

**Options**

- `OfficerInstanceID` — required officer ID.

```xml
<IsForceEligible OfficerInstanceID="LUKE_SKYWALKER"/>
```

## Ratings, Force, and planet state

### HasForceRank

Compares an officer's effective Force rank against a named rank.

**Options**

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

**Options**

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

**Options**

- `OfficerInstanceID` — required officer ID.
- `Comparison` — required comparison operator.
- `Value` — required integer.

```xml
<CompareOfficerForce OfficerInstanceID="LUKE_SKYWALKER"
                     Comparison="GreaterThanOrEqual"
                     Value="100"/>
```

### ComparePlanetStat

Compares one planet stat against an integer.

**Options**

- `Stat` — required planet stat.
- `Comparison` — required comparison operator.
- `Value` — required integer.
- `PlanetInstanceID` — explicit planet ID.
- `PlanetBinding` — binding containing a planet.
- `PlanetInstanceID` or `PlanetBinding` identifies the planet. `PlanetBinding` takes precedence when
  both are present; the condition fails when neither resolves.

```xml
<ComparePlanetStat PlanetInstanceID="NABOO"
                   Stat="EnergyCapacity"
                   Comparison="GreaterThan"
                   Value="0"/>
```

### HasBuildingType

Passes when the selected planet contains a completed building of the requested type.

**Options**

- `Type` — required building type.
- `PlanetInstanceID` — explicit planet ID.
- `PlanetBinding` — binding containing a planet.
- `PlanetInstanceID` or `PlanetBinding` identifies the planet. `PlanetBinding` takes precedence when
  both are present; the condition fails when neither resolves.

```xml
<Conditionals>
  <HasBuildingType Type="Defense" PlanetInstanceID="NABOO"/>
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

<p align="center"><a href="Triggers.md">← Triggers</a> · <a href="Index.md">Event guide</a> · <a href="Actions.md">Actions →</a></p>
