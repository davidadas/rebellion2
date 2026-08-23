# Triggers

Triggers activate events from typed gameplay results. An event uses either `Triggers` or `Schedule`, never both. Multiple triggers are alternatives: any matching result gives the event one activation opportunity.

If the event's top-level conditionals fail, that opportunity is discarded. The event waits for another matching result. Every trigger option is optional and narrows which results match.

All triggers support:

- `As` **[Optional]:** Exposes the complete matched result as a binding.
- `SourceEventInstanceID` **[Optional]:** Accepts only results produced by that authored event.

When multiple triggers use `As`, they must use the same alias and expose the same result type.

## Planet

### PlanetOwnershipChanged

Activates after a planet changes owner.

**Optional options**

- `PlanetInstanceID` **[Optional]:** Limits the trigger to one planet.
- `PreviousOwnerFactionInstanceID` **[Optional]:** Limits the trigger by previous owner.
- `NewOwnerFactionInstanceID` **[Optional]:** Limits the trigger by new owner.
- `Reason` **[Optional]:** Accepts `None` or `PopularSupport`.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `PlanetOwnershipChangedResult`.

```xml
<Triggers>
  <PlanetOwnershipChanged PlanetInstanceID="NABOO"
                          NewOwnerFactionInstanceID="FNALL1"
                          As="ownershipChange"/>
</Triggers>
```

### PlanetStatChanged

Activates after a recorded planet value changes.

**Optional options**

- `PlanetInstanceID` **[Optional]:** Limits the trigger to one planet.
- `FactionInstanceID` **[Optional]:** Limits the trigger to a faction-specific value.
- `Category` **[Optional]:** Accepts `Energy`, `EnergyAllocated`, `Loyalty`, `ProductionModifier`, `RawMaterial`, `RawMaterialAllocated`, `Smuggling`, `TroopWithdrawPercent`, `TroopSurplus`, `TroopRequired`, or `ControlUprising`.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `PlanetStatChangedResult`.

```xml
<Triggers>
  <PlanetStatChanged PlanetInstanceID="NABOO" Category="Loyalty" As="change"/>
</Triggers>
```

### BlockadeChanged

Activates when a blockade begins or ends.

**Optional options**

- `PlanetInstanceID` **[Optional]:** Limits the trigger to one planet.
- `IsBlockaded` **[Optional]:** Use `true` for a new blockade or `false` for a lifted blockade.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `BlockadeChangedResult`.

```xml
<Triggers>
  <BlockadeChanged PlanetInstanceID="CORUSCANT" IsBlockaded="true"/>
</Triggers>
```

### UprisingStarted

Activates when an uprising starts.

**Optional options**

- `PlanetInstanceID` **[Optional]:** Limits the trigger to one planet.
- `InstigatorFactionInstanceID` **[Optional]:** Limits the trigger by the instigating faction.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `PlanetUprisingStartedResult`.

```xml
<Triggers>
  <UprisingStarted PlanetInstanceID="NABOO" InstigatorFactionInstanceID="FNALL1"/>
</Triggers>
```

### UprisingEnded

Activates when an uprising ends.

**Optional options**

- `PlanetInstanceID` **[Optional]:** Limits the trigger to one planet.
- `FactionInstanceID` **[Optional]:** Limits the trigger by the affected faction.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `PlanetUprisingEndedResult`.

```xml
<Triggers>
  <UprisingEnded PlanetInstanceID="NABOO" FactionInstanceID="FNALL1"/>
</Triggers>
```

## Faction

### ResearchAdvanced

Activates when a faction advances a research discipline.

**Optional options**

- `FactionInstanceID` **[Optional]:** Limits the trigger to one faction.
- `Discipline` **[Optional]:** Accepts `None`, `ShipDesign`, `FacilityDesign`, or `TroopTraining`.
- `TechnologyTypeID` **[Optional]:** Limits the trigger to one technology type.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `ResearchOrderedResult`.

```xml
<Triggers>
  <ResearchAdvanced FactionInstanceID="FNALL1"
                    Discipline="ShipDesign"
                    As="research"/>
</Triggers>
```

## Mission

### MissionCompleted

Activates when a mission completes.

**Optional options**

- `MissionTypeID` **[Optional]:** Limits the trigger to one mission type.
- `Outcome` **[Optional]:** Accepts `Success`, `Failed`, or `Foiled`.
- `CompletionReason` **[Optional]:** Accepts `None`, `Success`, `Failure`, `Foiled`, `TargetUnavailable`, `NoResearchFacilities`, `ResearchProgress`, or `ResearchBreakthrough`.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `MissionCompletedResult`.
- `Participants` **[Optional]:** Optionally requires `Any` or `All` listed units to have participated.

```xml
<Triggers>
  <MissionCompleted MissionTypeID="Sabotage" Outcome="Success" As="mission">
    <Participants Match="Any">
      <Units>
        <Unit UnitInstanceID="HAN_SOLO"/>
        <Unit UnitInstanceID="LEIA_ORGANA"/>
      </Units>
    </Participants>
  </MissionCompleted>
</Triggers>
```

## Officer

### OfficerCaptureChanged

Activates when an officer becomes captured or is released.

**Optional options**

- `OfficerInstanceID` **[Optional]:** Limits the trigger to one officer.
- `IsCaptured` **[Optional]:** Use `true` for capture or `false` for release.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `OfficerCaptureStateResult`.

```xml
<Triggers>
  <OfficerCaptureChanged OfficerInstanceID="HAN_SOLO" IsCaptured="true"/>
</Triggers>
```

### OfficerKilled

Activates when an officer is killed.

**Optional options**

- `OfficerInstanceID` **[Optional]:** Limits the trigger to one officer.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `OfficerKilledResult`.

```xml
<Triggers>
  <OfficerKilled OfficerInstanceID="EMPEROR_PALPATINE"/>
</Triggers>
```

### OfficerInjured

Activates when an officer is injured.

**Optional options**

- `OfficerInstanceID` **[Optional]:** Limits the trigger to one officer.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `OfficerInjuredResult`.

```xml
<Triggers>
  <OfficerInjured OfficerInstanceID="LUKE_SKYWALKER" As="injury"/>
</Triggers>
```

### OfficerRecruited

Activates when an officer is recruited.

**Optional options**

- `OfficerInstanceID` **[Optional]:** Limits the trigger to one officer.
- `FactionInstanceID` **[Optional]:** Limits the trigger to one recruiting faction.
- `PlanetInstanceID` **[Optional]:** Limits the trigger to one recruitment planet.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `OfficerRecruitedResult`.

```xml
<Triggers>
  <OfficerRecruited OfficerInstanceID="HAN_SOLO" FactionInstanceID="FNALL1"/>
</Triggers>
```

## Unit lifecycle

### UnitOwnershipChanged

Activates after a unit changes owner.

**Optional options**

- `UnitInstanceID` **[Optional]:** Limits the trigger to one unit.
- `PreviousOwnerFactionInstanceID` **[Optional]:** Limits the trigger by previous owner.
- `NewOwnerFactionInstanceID` **[Optional]:** Limits the trigger by new owner.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `UnitOwnershipChangedResult`.

```xml
<Triggers>
  <UnitOwnershipChanged UnitInstanceID="CAPTURED_SHIP"
                        NewOwnerFactionInstanceID="FNALL1"/>
</Triggers>
```

### UnitCreated

Activates when a unit is created.

**Optional options**

- `UnitInstanceID` **[Optional]:** Limits the trigger to one unit.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `GameObjectCreatedResult`.

```xml
<Triggers>
  <UnitCreated UnitInstanceID="ROGUE_SQUADRON"/>
</Triggers>
```

### UnitDestroyed

Activates when a unit is destroyed.

**Optional options**

- `UnitInstanceID` **[Optional]:** Limits the trigger to one unit.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `GameObjectDestroyedResult`.

```xml
<Triggers>
  <UnitDestroyed UnitInstanceID="DEATH_STAR"/>
</Triggers>
```

### UnitArrived

Activates when a unit finishes movement.

**Optional options**

- `UnitInstanceID` **[Optional]:** Limits the trigger to one arriving unit.
- `DestinationInstanceID` **[Optional]:** Limits the trigger to one destination.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `UnitArrivedResult`.

```xml
<Triggers>
  <UnitArrived UnitInstanceID="EMPEROR_PALPATINE"
               DestinationInstanceID="CORUSCANT"
               As="arrival"/>
</Triggers>
```

## Combat

### SpaceCombatCompleted

Activates after a space battle resolves.

**Optional options**

- `PlanetInstanceID` **[Optional]:** Limits the trigger to combat at one planet.
- `AttackerFactionInstanceID` **[Optional]:** Limits the trigger by attacker.
- `DefenderFactionInstanceID` **[Optional]:** Limits the trigger by defender.
- `Winner` **[Optional]:** Accepts `Attacker`, `Defender`, or `Draw`.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `SpaceCombatResult`.

```xml
<Triggers>
  <SpaceCombatCompleted PlanetInstanceID="ENDOR" Winner="Attacker" As="battle"/>
</Triggers>
```

### BombardmentCompleted

Activates after bombardment resolves.

**Optional options**

- `PlanetInstanceID` **[Optional]:** Limits the trigger to one planet.
- `AttackerFactionInstanceID` **[Optional]:** Limits the trigger by attacker.
- `DefenderFactionInstanceID` **[Optional]:** Limits the trigger by defender.
- `Type` **[Optional]:** Accepts `Military`, `Civilian`, `General`, or `DestroyPlanet`.
- `PlanetDestroyed` **[Optional]:** Limits the trigger by whether the planet was destroyed.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `BombardmentResult`.

```xml
<Triggers>
  <BombardmentCompleted PlanetInstanceID="ALDERAAN"
                        Type="DestroyPlanet"
                        PlanetDestroyed="true"/>
</Triggers>
```

### PlanetaryAssaultCompleted

Activates after a planetary assault resolves.

**Optional options**

- `PlanetInstanceID` **[Optional]:** Limits the trigger to one planet.
- `AttackerFactionInstanceID` **[Optional]:** Limits the trigger by attacker.
- `DefenderFactionInstanceID` **[Optional]:** Limits the trigger by defender.
- `Success` **[Optional]:** Limits the trigger by assault success.
- `BlockedByShields` **[Optional]:** Limits the trigger by whether shields prevented the assault.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `PlanetaryAssaultResult`.

```xml
<Triggers>
  <PlanetaryAssaultCompleted PlanetInstanceID="HOTH"
                             AttackerFactionInstanceID="FNEMP1"
                             Success="true"/>
</Triggers>
```

### DuelCompleted

Activates after a duel resolves.

**Optional options**

- `FirstOfficerInstanceID` **[Optional]:** Limits the first officer.
- `SecondOfficerInstanceID` **[Optional]:** Limits the second officer.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `DuelResult`.

```xml
<Triggers>
  <DuelCompleted FirstOfficerInstanceID="LUKE_SKYWALKER"
                 SecondOfficerInstanceID="DARTH_VADER"
                 As="duel"/>
</Triggers>
```

## Manufacturing

### ManufacturingCompleted

Activates when a manufactured unit is deployed.

**Optional options**

- `FactionInstanceID` **[Optional]:** Limits the trigger to one manufacturing faction.
- `UnitInstanceID` **[Optional]:** Limits the trigger to one deployed unit.
- `LocationInstanceID` **[Optional]:** Limits the trigger to one deployment location.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `As` **[Optional]:** Binds the complete `ManufacturingDeployedResult`.

```xml
<Triggers>
  <ManufacturingCompleted FactionInstanceID="FNALL1"
                          LocationInstanceID="YAVIN"
                          As="production"/>
</Triggers>
```

## Trigger bindings

`As` binds the complete matched result. Prefix the alias with `$` and traverse its public properties from conditionals or actions:

```xml
<Triggers>
  <UnitArrived UnitInstanceID="EMPEROR_PALPATINE" As="arrival"/>
</Triggers>
<Conditionals>
  <EvaluateBinding Binding="$arrival.Destination.InstanceID"
                   Comparison="Equal"
                   CompareTo="CORUSCANT"/>
</Conditionals>
```

Use top-level [`Bindings`](Bindings.md#bindings) when an event must select and retain a scene node independently of a gameplay result.

---

<p align="center"><a href="Schedules.md">← Schedules</a> · <a href="Index.md">Event guide</a> · <a href="Conditionals.md">Conditionals →</a></p>
