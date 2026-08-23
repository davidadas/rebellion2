# Triggers

Triggers activate events from typed gameplay results. An event uses either `Triggers` or `Schedule`, never both. Multiple triggers are alternatives: any matching result gives the event one activation opportunity.

If the event's top-level conditionals fail, that opportunity is discarded. The event waits for another matching result. Every trigger option is optional and narrows which results match.

All triggers support:

- `As` — exposes the complete matched result as a binding.
- `SourceEventInstanceID` — accepts only results produced by that authored event.

When multiple triggers use `As`, they must use the same alias and expose the same result type.

## Planet

### PlanetOwnershipChanged

Activates after a planet changes owner.

**Options:** `PlanetInstanceID`, `PreviousOwnerFactionInstanceID`, `NewOwnerFactionInstanceID`, `Reason` (`None` or `PopularSupport`), `SourceEventInstanceID`, and `As`.

```xml
<Triggers>
  <PlanetOwnershipChanged PlanetInstanceID="NABOO"
                          NewOwnerFactionInstanceID="FNALL1"
                          As="ownershipChange"/>
</Triggers>
```

### PlanetStatChanged

Activates after a recorded planet value changes.

**Options:** `PlanetInstanceID`, `FactionInstanceID`, `Category`, `SourceEventInstanceID`, and `As`. Categories are `Energy`, `EnergyAllocated`, `Loyalty`, `ProductionModifier`, `RawMaterial`, `RawMaterialAllocated`, `Smuggling`, `TroopWithdrawPercent`, `TroopSurplus`, `TroopRequired`, and `ControlUprising`.

### BlockadeChanged

Activates when a blockade begins or ends.

**Options:** `PlanetInstanceID`, `IsBlockaded`, `SourceEventInstanceID`, and `As`.

### UprisingStarted and UprisingEnded

Activate when an uprising starts or ends.

**Options:** Both accept `PlanetInstanceID`, `SourceEventInstanceID`, and `As`. `UprisingStarted` also accepts `InstigatorFactionInstanceID`; `UprisingEnded` accepts `FactionInstanceID`.

### PlanetIncident

Activates when an event records a planet incident.

**Options:** `PlanetInstanceID`, `Type` (`Uprising`, `Intelligence`, `Disaster`, or `Resource`), `SourceEventInstanceID`, and `As`.

## Faction

### ResearchAdvanced

Activates when a faction advances a research discipline.

**Options:** `FactionInstanceID`, `Discipline` (`None`, `ShipDesign`, `FacilityDesign`, or `TroopTraining`), `TechnologyTypeID`, `SourceEventInstanceID`, and `As`.

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

**Options:** `MissionTypeID`, `Outcome`, `CompletionReason`, `SourceEventInstanceID`, `As`, and an optional `Participants` filter with `Match="Any"` or `Match="All"`.

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

Mission outcomes are `Success`, `Failed`, and `Foiled`. Completion reasons are `None`, `Success`, `Failure`, `Foiled`, `TargetUnavailable`, `NoResearchFacilities`, `ResearchProgress`, and `ResearchBreakthrough`.

## Officer

### OfficerCaptureChanged

Activates when an officer becomes captured or is released.

**Options:** `OfficerInstanceID`, `IsCaptured`, `SourceEventInstanceID`, and `As`.

### OfficerKilled and OfficerInjured

Activate when an officer is killed or injured.

**Options:** `OfficerInstanceID`, `SourceEventInstanceID`, and `As`.

### OfficerRecruited

Activates when an officer is recruited.

**Options:** `OfficerInstanceID`, `FactionInstanceID`, `PlanetInstanceID`, `SourceEventInstanceID`, and `As`.

## Unit lifecycle

### UnitOwnershipChanged

Activates after a unit changes owner.

**Options:** `UnitInstanceID`, `PreviousOwnerFactionInstanceID`, `NewOwnerFactionInstanceID`, `SourceEventInstanceID`, and `As`.

### UnitCreated and UnitDestroyed

Activate when a unit is created or destroyed.

**Options:** `UnitInstanceID`, `SourceEventInstanceID`, and `As`.

### UnitArrived

Activates when a unit finishes movement.

**Options:** `UnitInstanceID`, `DestinationInstanceID`, `SourceEventInstanceID`, and `As`.

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

## Combat

### SpaceCombatCompleted

Activates after a space battle resolves.

**Options:** `PlanetInstanceID`, `AttackerFactionInstanceID`, `DefenderFactionInstanceID`, `Winner` (`Attacker`, `Defender`, or `Draw`), `SourceEventInstanceID`, and `As`.

### BombardmentCompleted

Activates after bombardment resolves.

**Options:** `PlanetInstanceID`, `AttackerFactionInstanceID`, `DefenderFactionInstanceID`, `Type` (`Military`, `Civilian`, `General`, or `DestroyPlanet`), `PlanetDestroyed`, `SourceEventInstanceID`, and `As`.

### PlanetaryAssaultCompleted

Activates after a planetary assault resolves.

**Options:** `PlanetInstanceID`, `AttackerFactionInstanceID`, `DefenderFactionInstanceID`, `Success`, `BlockedByShields`, `SourceEventInstanceID`, and `As`.

### DuelCompleted

Activates after a duel resolves.

**Options:** `FirstOfficerInstanceID`, `SecondOfficerInstanceID`, `SourceEventInstanceID`, and `As`.

## Manufacturing

### ManufacturingCompleted

Activates when a manufactured unit is deployed.

**Options:** `FactionInstanceID`, `UnitInstanceID`, `LocationInstanceID`, `SourceEventInstanceID`, and `As`.

```xml
<Triggers>
  <ManufacturingCompleted FactionInstanceID="FNALL1" As="production"/>
</Triggers>
```

## Trigger bindings

`As` binds the complete matched result. Prefix the alias with `$` and traverse its public properties from conditionals or actions. Use top-level [`Bindings`](Bindings.md#bindings) when an event must select and retain a scene node independently of a gameplay result.

---

<p align="center"><a href="Schedules.md">← Schedules</a> · <a href="Index.md">Event guide</a> · <a href="Conditions.md">Conditions →</a></p>
