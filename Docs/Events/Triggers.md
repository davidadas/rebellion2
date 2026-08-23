# Triggers

Triggers activate events from typed gameplay results. An event uses either `Triggers` or `Schedule`, never both. Multiple triggers are alternatives: any matching result gives the event one activation opportunity.

If the event's top-level conditionals fail, that opportunity is discarded. The event waits for another matching result. Every trigger option is optional and narrows which results match.

All triggers support:

- `SourceEventInstanceID` **[Optional]:** Accepts only results produced by that authored event.
- `Bindings` **[Optional]:** Contains explicit trigger-argument bindings. Each trigger lists its supported arguments below.

Alternative triggers must expose the same binding aliases and value types.
See [Bindings](Bindings.md#trigger-bindings) for binding scope, explicit arguments, and
evaluation order.

## Planet

### PlanetOwnershipChanged

Activates after a planet changes owner.

**Optional options**

- `PlanetInstanceID` **[Optional]:** Limits the trigger to one planet.
- `PreviousOwnerFactionInstanceID` **[Optional]:** Limits the trigger by previous owner.
- `NewOwnerFactionInstanceID` **[Optional]:** Limits the trigger by new owner.
- `Reason` **[Optional]:** Accepts `None` or `PopularSupport`.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `Bindings` **[Optional]:** Supports `Planet`, `PreviousOwner`, `NewOwner`, and `Reason`.

```xml
<Triggers>
  <PlanetOwnershipChanged PlanetInstanceID="NABOO"
                          NewOwnerFactionInstanceID="FNALL1"/>
</Triggers>
```

### PlanetStatChanged

Activates after a recorded planet value changes.

**Optional options**

- `PlanetInstanceID` **[Optional]:** Limits the trigger to one planet.
- `FactionInstanceID` **[Optional]:** Limits the trigger to a faction-specific value.
- `Category` **[Optional]:** Accepts `Energy`, `EnergyAllocated`, `Loyalty`, `ProductionModifier`, `RawMaterial`, `RawMaterialAllocated`, `Smuggling`, `TroopWithdrawPercent`, `TroopSurplus`, `TroopRequired`, or `ControlUprising`.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `Bindings` **[Optional]:** Supports `Planet`, `Faction`, `Category`, `PreviousValue`, and `CurrentValue`.

```xml
<Triggers>
  <PlanetStatChanged PlanetInstanceID="NABOO" Category="Loyalty"/>
</Triggers>
```

### BlockadeChanged

Activates when a blockade begins or ends.

**Optional options**

- `PlanetInstanceID` **[Optional]:** Limits the trigger to one planet.
- `IsBlockaded` **[Optional]:** Use `true` for a new blockade or `false` for a lifted blockade.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `Bindings` **[Optional]:** Supports `Planet`, `BlockadingFleet`, and `IsBlockaded`.

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
- `Bindings` **[Optional]:** Supports `Planet` and `InstigatorFaction`.

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
- `Bindings` **[Optional]:** Supports `Planet` and `Faction`.

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
- `Bindings` **[Optional]:** Supports `Faction`, `Discipline`, `ResearchOrder`, `Capacity`, and `Technology`.

```xml
<Triggers>
  <ResearchAdvanced FactionInstanceID="FNALL1"
                    Discipline="ShipDesign"/>
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
- `Bindings` **[Optional]:** Supports `Mission`, `MissionName`, `MissionTypeID`, `TargetName`, `Location`, `ReturnDestination`, `Participants`, `Outcome`, `CompletionReason`, and `CanContinue`.
- `Participants` **[Optional]:** Optionally requires `Any` or `All` listed units to have participated.

```xml
<Triggers>
  <MissionCompleted MissionTypeID="Sabotage" Outcome="Success">
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
- `Bindings` **[Optional]:** Supports `Officer`, `IsCaptured`, `LinkedOfficer`, and `Context`.

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
- `Bindings` **[Optional]:** Supports `Officer`, `Assassin`, and `Context`.

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
- `Bindings` **[Optional]:** Supports `Officer` and `Severity`.

```xml
<Triggers>
  <OfficerInjured OfficerInstanceID="LUKE_SKYWALKER"/>
</Triggers>
```

### OfficerRecruited

Activates when an officer is recruited.

**Optional options**

- `OfficerInstanceID` **[Optional]:** Limits the trigger to one officer.
- `FactionInstanceID` **[Optional]:** Limits the trigger to one recruiting faction.
- `PlanetInstanceID` **[Optional]:** Limits the trigger to one recruitment planet.
- `SourceEventInstanceID` **[Optional]:** Limits the trigger to results from one event.
- `Bindings` **[Optional]:** Supports `Officer`, `Faction`, and `Planet`.

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
- `Bindings` **[Optional]:** Supports `Unit`, `PreviousOwner`, and `NewOwner`.

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
- `Bindings` **[Optional]:** Supports `Unit`.

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
- `Bindings` **[Optional]:** Supports `Unit`, `DestroyedBy`, and `Context`.

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
- `Bindings` **[Optional]:** Supports `Unit`, `Destination`, and `MovementGroupID`.

```xml
<Triggers>
  <UnitArrived UnitInstanceID="EMPEROR_PALPATINE"
               DestinationInstanceID="CORUSCANT"/>
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
- `Bindings` **[Optional]:** Supports `AttackerFleet`, `DefenderFleet`, `Planet`, and `Winner`.

```xml
<Triggers>
  <SpaceCombatCompleted PlanetInstanceID="ENDOR" Winner="Attacker"/>
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
- `Bindings` **[Optional]:** Supports `Planet`, `AttackingFaction`, `Type`, and `PlanetDestroyed`.

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
- `Bindings` **[Optional]:** Supports `Planet`, `AttackingFaction`, `Success`, and `BlockedByShields`.

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
- `Bindings` **[Optional]:** Supports `FirstOfficer`, `SecondOfficer`, `FirstOfficerInstanceID`, `SecondOfficerInstanceID`, `Location`, `FirstOfficerCaptured`, `FirstOfficerInjury`, `SecondOfficerInjury`, `ImagePath`, and `AudioPath`.

```xml
<Triggers>
  <DuelCompleted FirstOfficerInstanceID="LUKE_SKYWALKER"
                 SecondOfficerInstanceID="DARTH_VADER">
    <Bindings>
      <Bind Argument="ImagePath" As="imagePath"/>
      <Bind Argument="AudioPath" As="audioPath"/>
    </Bindings>
  </DuelCompleted>
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
- `Bindings` **[Optional]:** Supports `Faction`, `DeployedObject`, and `Location`.

```xml
<Triggers>
  <ManufacturingCompleted FactionInstanceID="FNALL1"
                          LocationInstanceID="YAVIN">
    <Bindings>
      <Bind Argument="DeployedObject" As="unit"/>
      <Bind Argument="Location" As="location"/>
    </Bindings>
  </ManufacturingCompleted>
</Triggers>
```

---

<p align="center"><a href="Schedules.md">← Schedules</a> · <a href="Index.md">Event guide</a> · <a href="Conditionals.md">Conditionals →</a></p>
