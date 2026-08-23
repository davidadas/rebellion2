# Conditionals

Conditionals inspect game state without changing it. Every top-level conditional must pass before
an event activates.

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

Sibling conditionals are ANDed. Use `Any` for OR, `Not` for negation, `All` for an explicit AND, and
`Xor` when exactly one nested conditional must pass.

## Collections and logic

### Conditionals

`Conditionals` is the XML collection used at the top level of an event. It accepts zero or more
conditionals and passes when all of them pass. An empty collection does not block execution.

**Optional options**

- Child conditionals **[Optional]:** Siblings are combined using AND.

```xml
<Conditionals>
  <IsOwned PlanetInstanceID="NABOO"/>
</Conditionals>
```

### All

`All` passes when every nested conditional passes.

**Required options**

- Child conditionals **[Required]:** One or more; every child must pass.

```xml
<All>
  <IsOwned PlanetInstanceID="NABOO" FactionInstanceID="FNALL1"/>
  <HasBuildingType Type="Defense"/>
</All>
```

### Any

`Any` passes when at least one nested conditional passes.

**Required options**

- Child conditionals **[Required]:** One or more; at least one child must pass.

```xml
<Any>
  <IsCaptured OfficerInstanceID="HAN_SOLO"/>
  <IsKilled OfficerInstanceID="HAN_SOLO"/>
</Any>
```

### Not

`Not` passes when every nested conditional fails.

**Required options**

- Child conditionals **[Required]:** One or more; every child must fail.

```xml
<Not>
  <IsInTransit UnitInstanceID="HAN_SOLO"/>
  <IsOnMission UnitInstanceID="HAN_SOLO"/>
</Not>
```

### Xor

`Xor` passes when exactly one nested conditional passes.

**Required options**

- Child conditionals **[Required]:** One or more; exactly one child must pass.

```xml
<Xor>
  <IsCaptured OfficerInstanceID="HAN_SOLO"/>
  <IsInTransit UnitInstanceID="HAN_SOLO"/>
</Xor>
```

## Time and event state

### TickCount

Compares the current campaign tick.

**Required options**

- `Comparison` **[Required]:** `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, or `LessThanOrEqual`.
- `Ticks` **[Required]:** Non-negative integer.

```xml
<TickCount Comparison="GreaterThanOrEqual" Ticks="500"/>
```

### HasEventActivated

Passes after the referenced event has activated at least once.

**Required options**

- `EventInstanceID` **[Required]:** The `InstanceID` of the event to evaluate.

```xml
<HasEventActivated EventInstanceID="EVENT_A"/>
```

### IsEventComplete

Passes when the referenced event can no longer activate because it reached `MaximumActivations`,
matched a recurring schedule's `Until`, or completed a one-shot schedule.

**Required options**

- `EventInstanceID` **[Required]:** The `InstanceID` of the event to evaluate.

```xml
<IsEventComplete EventInstanceID="EVENT_B"/>
```

### EvaluateEventVariable

Compares a saved integer event variable.

**Required options**

- `Key` **[Required]:** The persistent key of the event variable to evaluate.
- `Comparison` **[Required]:** `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, or `LessThanOrEqual`.
- `CompareTo` **[Required]:** The integer to compare against.

```xml
<EvaluateEventVariable Key="naboo.attacks"
                       Comparison="GreaterThanOrEqual"
                       CompareTo="3"/>
```

## Trigger bindings

### EvaluateBinding

Compares a scalar value supplied by a trigger binding. Ordered comparisons require an integer
binding.

**Required options**

- `Binding` **[Required]:** `$alias` reference.
- `Comparison` **[Required]:** `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, or `LessThanOrEqual`.
- `CompareTo` **[Required]:** The scalar value to compare against.

```xml
<!-- Given <MissionCompleted As="mission"/> in this event's Triggers. -->
<EvaluateBinding Binding="$mission.Outcome" Comparison="Equal" CompareTo="Success"/>
```

### BindingIncludesUnit

Passes when a bound collection contains the named unit.

**Required options**

- `Binding` **[Required]:** `$alias` reference to a collection.
- `UnitInstanceID` **[Required]:** The `InstanceID` of the unit to evaluate.

```xml
<!-- Given <MissionCompleted As="mission"/> in this event's Triggers. -->
<BindingIncludesUnit Binding="$mission.Participants" UnitInstanceID="LUKE_SKYWALKER"/>
```

## Ownership and support

### IsOwned

Passes when the selected planet has a non-neutral owner.

**Required options**

- `PlanetInstanceID` **[Required]:** The `InstanceID` of the planet to evaluate; use either this or `PlanetBinding`.
- `PlanetBinding` **[Required]:** A binding that resolves the planet to evaluate; use either this or `PlanetInstanceID`.

**Optional options**

- `FactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must own the planet.

```xml
<IsOwned PlanetInstanceID="NABOO" FactionInstanceID="FNALL1"/>
```

### RollAgainstPopularSupport

Rolls a random percentage against the selected faction's current support on a planet.

**Required options**

- `FactionInstanceID` **[Required]:** The `InstanceID` of the faction whose support is evaluated.
- `PlanetInstanceID` **[Required]:** The `InstanceID` of the planet to evaluate; use either this or `PlanetBinding`.
- `PlanetBinding` **[Required]:** A binding that resolves the planet to evaluate; use either this or `PlanetInstanceID`.

```xml
<RollAgainstPopularSupport PlanetBinding="$planet" FactionInstanceID="FNALL1"/>
```

## Location and scene relationships

### IsAtLocation

Passes when the unit is the location itself or is contained anywhere beneath it.

**Required options**

- `UnitInstanceID` **[Required]:** The `InstanceID` of the unit whose location is evaluated.
- `LocationInstanceID` **[Required]:** The `InstanceID` of the location that must contain the unit.

```xml
<IsAtLocation>
  <UnitInstanceID>LUKE_SKYWALKER</UnitInstanceID>
  <LocationInstanceID>NABOO</LocationInstanceID>
</IsAtLocation>
```

### ShareParent

Passes when every listed unit has the exact same immediate parent.

**Required options**

- `Units` **[Required]:** Child containing at least two `Unit` elements.
- `UnitInstanceID` **[Required]:** The `InstanceID` referenced by each `Unit` element.

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

**Required options**

- `Type` **[Required]:** `Galaxy`, `PlanetSector`, `Planet`, `Fleet`, `Mission`, or `CapitalShip`.
- `Units` **[Required]:** Child containing at least two `Unit` elements.
- `UnitInstanceID` **[Required]:** The `InstanceID` referenced by each `Unit` element.

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

**Required options**

- `UnitInstanceIDs` **[Required]:** Child containing exactly two `String` unit IDs.

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

**Required options**

- `UnitInstanceID` **[Required]:** The `InstanceID` of the unit to evaluate.

```xml
<IsOnMission UnitInstanceID="HAN_SOLO"/>
```

### IsInTransit

Passes when the unit currently has movement state.

**Required options**

- `UnitInstanceID` **[Required]:** The `InstanceID` of the unit to evaluate.

```xml
<IsInTransit UnitInstanceID="HAN_SOLO"/>
```

### IsCaptured

Passes when the officer is captured.

**Required options**

- `OfficerInstanceID` **[Required]:** The `InstanceID` of the officer to evaluate.

**Optional options**

- `CaptorFactionInstanceID` **[Optional]:** Capturing-faction filter.

```xml
<IsCaptured OfficerInstanceID="HAN_SOLO"/>
<IsCaptured OfficerInstanceID="HAN_SOLO" CaptorFactionInstanceID="FNEMP1"/>
```

### IsKilled

Passes when the officer is killed.

**Required options**

- `OfficerInstanceID` **[Required]:** The `InstanceID` of the officer to evaluate.

```xml
<IsKilled OfficerInstanceID="HAN_SOLO"/>
```

### IsInjured

Passes when the officer is injured.

**Required options**

- `OfficerInstanceID` **[Required]:** The `InstanceID` of the officer to evaluate.

```xml
<IsInjured OfficerInstanceID="HAN_SOLO"/>
```

### IsForceEligible

Passes when the officer is eligible to use and develop Force ability.

**Required options**

- `OfficerInstanceID` **[Required]:** The `InstanceID` of the officer to evaluate.

```xml
<IsForceEligible OfficerInstanceID="LUKE_SKYWALKER"/>
```

## Ratings, Force, and planet state

### HasForceRank

Compares an officer's effective Force rank against a named rank.

**Required options**

- `OfficerInstanceID` **[Required]:** The `InstanceID` of the officer to evaluate.
- `Comparison` **[Required]:** `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, or `LessThanOrEqual`.
- `Rank` **[Required]:** `None`, `Novice`, `Trainee`, `ForceStudent`, `ForceKnight`, or `ForceMaster`.

```xml
<HasForceRank OfficerInstanceID="LUKE_SKYWALKER"
              Comparison="GreaterThanOrEqual"
              Rank="ForceKnight"/>
```

### CompareOfficerRating

Compares one effective officer rating against an integer.

**Required options**

- `OfficerInstanceID` **[Required]:** The `InstanceID` of the officer to evaluate.
- `Rating` **[Required]:** `Diplomacy`, `Espionage`, `Combat`, `Leadership`, `ShipResearch`, `TroopResearch`, or `FacilityResearch`.
- `Comparison` **[Required]:** `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, or `LessThanOrEqual`.
- `Value` **[Required]:** Integer.

```xml
<CompareOfficerRating OfficerInstanceID="HAN_SOLO"
                      Rating="Combat"
                      Comparison="GreaterThanOrEqual"
                      Value="80"/>
```

### CompareOfficerForce

Compares an officer's effective Force value against an integer.

**Required options**

- `OfficerInstanceID` **[Required]:** The `InstanceID` of the officer to evaluate.
- `Comparison` **[Required]:** `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, or `LessThanOrEqual`.
- `Value` **[Required]:** Integer.

```xml
<CompareOfficerForce OfficerInstanceID="LUKE_SKYWALKER"
                     Comparison="GreaterThanOrEqual"
                     Value="100"/>
```

### ComparePlanetStat

Compares one planet stat against an integer.

**Required options**

- `Stat` **[Required]:** `RawResourceNodes` or `EnergyCapacity`.
- `Comparison` **[Required]:** `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, or `LessThanOrEqual`.
- `Value` **[Required]:** Integer.
- `PlanetInstanceID` **[Required]:** The `InstanceID` of the planet to evaluate; use either this or `PlanetBinding`.
- `PlanetBinding` **[Required]:** A binding that resolves the planet to evaluate; use either this or `PlanetInstanceID`.

```xml
<ComparePlanetStat PlanetInstanceID="NABOO"
                   Stat="EnergyCapacity"
                   Comparison="GreaterThan"
                   Value="0"/>
```

### HasBuildingType

Passes when the selected planet contains a completed building of the requested type.

**Required options**

- `Type` **[Required]:** `Mine`, `Refinery`, `Shipyard`, `TrainingFacility`, `ConstructionFacility`, `Defense`, `Weapon`, or `Headquarters`.
- `PlanetInstanceID` **[Required]:** The `InstanceID` of the planet to evaluate; use either this or `PlanetBinding`.
- `PlanetBinding` **[Required]:** A binding that resolves the planet to evaluate; use either this or `PlanetInstanceID`.

```xml
<Conditionals>
  <HasBuildingType Type="Defense" PlanetInstanceID="NABOO"/>
</Conditionals>
```

---

<p align="center"><a href="Triggers.md">← Triggers</a> · <a href="Index.md">Event guide</a> · <a href="Actions.md">Actions →</a></p>
