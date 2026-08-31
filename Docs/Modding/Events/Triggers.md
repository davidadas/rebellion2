# Triggers

Triggers activate events from typed gameplay results. An event uses either `Triggers` or `Schedule`, never both. Multiple triggers are alternatives: any matching result gives the event one activation opportunity.

If the event's top-level conditionals fail, that opportunity is discarded. The event waits for another matching result. Every trigger option is optional and narrows which results match.

All triggers support:

- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
- `Bindings` **[Optional]:** Contains explicit trigger-argument bindings. Each trigger lists its supported arguments below.

Alternative triggers must expose the same binding aliases and value types.
See [Bindings](Bindings.md#trigger-bindings) for binding scope, explicit arguments, and
evaluation order.

## Planet

### PlanetOwnershipChanged

Activates after a planet changes owner.

**Optional options**

- `PlanetInstanceID` **[Optional]:** The `InstanceID` of the planet that must have changed ownership.
- `PreviousOwnerFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must have previously owned the planet.
- `NewOwnerFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must now own the planet.
- `Reason` **[Optional]:** Accepts `None` or `PopularSupport`.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
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

- `PlanetInstanceID` **[Optional]:** The `InstanceID` of the planet whose statistic must have changed.
- `FactionInstanceID` **[Optional]:** The `InstanceID` of the faction associated with the changed statistic.
- `Category` **[Optional]:** Accepts `Energy`, `EnergyAllocated`, `Loyalty`, `ProductionModifier`, `RawMaterial`, `RawMaterialAllocated`, `Smuggling`, `TroopWithdrawPercent`, `TroopSurplus`, `TroopRequired`, or `ControlUprising`.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
- `Bindings` **[Optional]:** Supports `Planet`, `Faction`, `Category`, `PreviousValue`, and `CurrentValue`.

```xml
<Triggers>
  <PlanetStatChanged PlanetInstanceID="NABOO" Category="Loyalty"/>
</Triggers>
```

### BlockadeChanged

Activates when a blockade begins or ends.

**Optional options**

- `PlanetInstanceID` **[Optional]:** The `InstanceID` of the planet whose blockade state must have changed.
- `IsBlockaded` **[Optional]:** Use `true` for a new blockade or `false` for a lifted blockade.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
- `Bindings` **[Optional]:** Supports `Planet`, `BlockadingFleet`, and `IsBlockaded`.

```xml
<Triggers>
  <BlockadeChanged PlanetInstanceID="CORUSCANT" IsBlockaded="true"/>
</Triggers>
```

### UprisingStarted

Activates when an uprising starts.

**Optional options**

- `PlanetInstanceID` **[Optional]:** The `InstanceID` of the planet where the uprising must have started.
- `InstigatorFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must have instigated the uprising.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
- `Bindings` **[Optional]:** Supports `Planet` and `InstigatorFaction`.

```xml
<Triggers>
  <UprisingStarted PlanetInstanceID="NABOO" InstigatorFactionInstanceID="FNALL1"/>
</Triggers>
```

### UprisingEnded

Activates when an uprising ends.

**Optional options**

- `PlanetInstanceID` **[Optional]:** The `InstanceID` of the planet where the uprising must have ended.
- `FactionInstanceID` **[Optional]:** The `InstanceID` of the faction associated with the ended uprising.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
- `Bindings` **[Optional]:** Supports `Planet` and `Faction`.

```xml
<Triggers>
  <UprisingEnded PlanetInstanceID="NABOO" FactionInstanceID="FNALL1"/>
</Triggers>
```

### IntelligenceRevealed

Activates when intelligence is revealed to a faction.

**Optional options**

- `RecipientFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must have received the intelligence.
- `ObservationInstanceID` **[Optional]:** The `InstanceID` of a scene node that must be among the revealed observations.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
- `Bindings` **[Optional]:** Supports `Recipient` and `Observations`.

```xml
<Triggers>
  <IntelligenceRevealed RecipientFactionInstanceID="FNALL1"
                        ObservationInstanceID="CORUSCANT"/>
</Triggers>
```

### MaintenanceRequired

Activates when a faction cannot meet a maintenance obligation.

**Optional options**

- `FactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must require maintenance.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
- `Bindings` **[Optional]:** Supports `Faction` and `Amount`.

```xml
<Triggers>
  <MaintenanceRequired FactionInstanceID="FNALL1"/>
</Triggers>
```

## Faction

### ResearchAdvanced

Activates when a faction advances a research discipline.

**Optional options**

- `FactionInstanceID` **[Optional]:** The `InstanceID` of the faction whose research must have advanced.
- `Discipline` **[Optional]:** Accepts `None`, `ShipDesign`, `FacilityDesign`, or `TroopTraining`.
- `TechnologyTypeID` **[Optional]:** The `TypeID` of the technology that must have been researched.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
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

- `MissionTypeID` **[Optional]:** The `TypeID` of the mission that must have completed.
- `Outcome` **[Optional]:** Accepts `Success`, `Failed`, or `Foiled`.
- `CompletionReason` **[Optional]:** Accepts `None`, `Success`, `Failure`, `Foiled`, `TargetUnavailable`, `NoResearchFacilities`, `ResearchProgress`, or `ResearchBreakthrough`.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
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

- `OfficerInstanceID` **[Optional]:** The `InstanceID` of the officer whose capture state must have changed.
- `IsCaptured` **[Optional]:** Use `true` for capture or `false` for release.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
- `Bindings` **[Optional]:** Supports `Officer`, `IsCaptured`, `LinkedOfficer`, and `Context`.

```xml
<Triggers>
  <OfficerCaptureChanged OfficerInstanceID="HAN_SOLO" IsCaptured="true"/>
</Triggers>
```

### OfficerKilled

Activates when an officer is killed.

**Optional options**

- `OfficerInstanceID` **[Optional]:** The `InstanceID` of the officer who must have been killed.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
- `Bindings` **[Optional]:** Supports `Officer`, `Assassin`, and `Context`.

```xml
<Triggers>
  <OfficerKilled OfficerInstanceID="EMPEROR_PALPATINE"/>
</Triggers>
```

### OfficerInjured

Activates when an officer is injured.

**Optional options**

- `OfficerInstanceID` **[Optional]:** The `InstanceID` of the officer who must have been injured.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
- `Bindings` **[Optional]:** Supports `Officer` and `Severity`.

```xml
<Triggers>
  <OfficerInjured OfficerInstanceID="LUKE_SKYWALKER"/>
</Triggers>
```

### OfficerRecruited

Activates when an officer is recruited.

**Optional options**

- `OfficerInstanceID` **[Optional]:** The `InstanceID` of the officer who must have been recruited.
- `FactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must have recruited the officer.
- `PlanetInstanceID` **[Optional]:** The `InstanceID` of the planet where recruitment must have occurred.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
- `Bindings` **[Optional]:** Supports `Officer`, `Faction`, and `Planet`.

```xml
<Triggers>
  <OfficerRecruited OfficerInstanceID="HAN_SOLO" FactionInstanceID="FNALL1"/>
</Triggers>
```

### ForceDiscoveryChanged

Activates when an officer's Force discovery state changes.

**Optional options**

- `OfficerInstanceID` **[Optional]:** The `InstanceID` of the officer whose Force discovery state must have changed.
- `DiscovererInstanceID` **[Optional]:** The `InstanceID` of the officer who must have made the discovery.
- `EventType` **[Optional]:** Accepts `DiscoveringForceUser` or `ForceUserDiscovered`.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
- `Bindings` **[Optional]:** Supports `Officer`, `Discoverer`, `ForceRank`, and `EventType`.

```xml
<Triggers>
  <ForceDiscoveryChanged OfficerInstanceID="LEIA_ORGANA"
                         EventType="ForceUserDiscovered"/>
</Triggers>
```

## Unit lifecycle

### UnitOwnershipChanged

Activates after a unit changes owner.

**Optional options**

- `UnitInstanceID` **[Optional]:** The `InstanceID` of the unit whose ownership must have changed.
- `PreviousOwnerFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must have previously owned the unit.
- `NewOwnerFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must now own the unit.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
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

- `UnitInstanceID` **[Optional]:** The `InstanceID` of the unit that must have been created.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
- `Bindings` **[Optional]:** Supports `Unit`.

```xml
<Triggers>
  <UnitCreated UnitInstanceID="ROGUE_SQUADRON"/>
</Triggers>
```

### UnitDestroyed

Activates when a unit is destroyed by any supported destruction path.

**Optional options**

- `UnitInstanceID` **[Optional]:** The `InstanceID` of the unit that must have been destroyed.
- `Reason` **[Optional]:** Accepts `Direct`, `Arrival`, `Maintenance`, or `Sabotage`.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
- `Bindings` **[Optional]:** Supports `Unit`, `DestroyedBy`, `Context`, and `Reason`.

```xml
<Triggers>
  <UnitDestroyed UnitInstanceID="DEATH_STAR" Reason="Sabotage"/>
</Triggers>
```

### UnitArrived

Activates when a unit finishes movement.

**Optional options**

- `UnitInstanceID` **[Optional]:** The `InstanceID` of the unit that must have arrived.
- `DestinationInstanceID` **[Optional]:** The `InstanceID` of the destination where the unit must have arrived.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
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

- `PlanetInstanceID` **[Optional]:** The `InstanceID` of the planet where combat must have occurred.
- `AttackerFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must have attacked.
- `DefenderFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must have defended.
- `Winner` **[Optional]:** Accepts `Attacker`, `Defender`, or `Draw`.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
- `Bindings` **[Optional]:** Supports `AttackerFleet`, `DefenderFleet`, `Planet`, and `Winner`.

```xml
<Triggers>
  <SpaceCombatCompleted PlanetInstanceID="ENDOR" Winner="Attacker"/>
</Triggers>
```

### BombardmentCompleted

Activates after bombardment resolves.

**Optional options**

- `PlanetInstanceID` **[Optional]:** The `InstanceID` of the planet that must have been bombarded.
- `AttackerFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must have bombarded the planet.
- `DefenderFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must have defended the planet.
- `Type` **[Optional]:** Accepts `Military`, `Civilian`, `General`, or `DestroyPlanet`.
- `PlanetDestroyed` **[Optional]:** Limits the trigger by whether the planet was destroyed.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
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

- `PlanetInstanceID` **[Optional]:** The `InstanceID` of the planet where the assault must have occurred.
- `AttackerFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must have launched the assault.
- `DefenderFactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must have defended against the assault.
- `Success` **[Optional]:** Limits the trigger by assault success.
- `BlockedByShields` **[Optional]:** Limits the trigger by whether shields prevented the assault.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
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

- `FirstOfficerInstanceID` **[Optional]:** The `InstanceID` of the first officer who must have participated in the duel.
- `SecondOfficerInstanceID` **[Optional]:** The `InstanceID` of the second officer who must have participated in the duel.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
- `Bindings` **[Optional]:** Supports `FirstOfficer`, `SecondOfficer`, `FirstOfficerInstanceID`, `SecondOfficerInstanceID`, `Location`, `FirstOfficerCaptured`, `FirstOfficerInjury`, `SecondOfficerInjury`, `ImagePath`, and `AudioPath`.

```xml
<Triggers>
  <DuelCompleted FirstOfficerInstanceID="LUKE_SKYWALKER"
                 SecondOfficerInstanceID="DARTH_VADER">
    <Bindings>
      <Bind Argument="ImagePath" As="$imagePath"/>
      <Bind Argument="AudioPath" As="$audioPath"/>
    </Bindings>
  </DuelCompleted>
</Triggers>
```

## Manufacturing

### ManufacturingCompleted

Activates when a manufactured unit is deployed.

**Optional options**

- `FactionInstanceID` **[Optional]:** The `InstanceID` of the faction that must have completed manufacturing.
- `UnitInstanceID` **[Optional]:** The `InstanceID` of the unit that must have been deployed.
- `LocationInstanceID` **[Optional]:** The `InstanceID` of the location where the unit must have been deployed.
- `SourceEventInstanceID` **[Optional]:** The `InstanceID` of the authored event that must have produced the result.
- `Bindings` **[Optional]:** Supports `Faction`, `DeployedObject`, and `Location`.

```xml
<Triggers>
  <ManufacturingCompleted FactionInstanceID="FNALL1"
                          LocationInstanceID="YAVIN">
    <Bindings>
      <Bind Argument="DeployedObject" As="$unit"/>
      <Bind Argument="Location" As="$location"/>
    </Bindings>
  </ManufacturingCompleted>
</Triggers>
```

---

<p align="center"><a href="Schedules.md">← Schedules</a> · <a href="Index.md">Event guide</a> · <a href="Conditionals.md">Conditionals →</a></p>
